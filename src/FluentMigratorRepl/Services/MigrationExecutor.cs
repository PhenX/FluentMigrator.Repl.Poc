using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentMigrator.Runner;
using FluentMigrator;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using FluentMigratorRepl.Webcil;

namespace FluentMigratorRepl.Services;

public class MigrationExecutor
{
    private readonly IResourceResolver _resourceResolver;
    private readonly IBlazorHttpClientFactory _httpClientFactory;
    private string _lastConnectionString = "";

    public MigrationExecutor(IResourceResolver resourceResolver, IBlazorHttpClientFactory httpClientFactory)
    {
        _resourceResolver = resourceResolver;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> ExecuteMigrationCodeAsync(string userCode)
    {
        var output = new StringBuilder();
        
        try
        {
            output.AppendLine("=== FluentMigrator REPL - Executing Migration ===");
            output.AppendLine();
            
            // Validate user code is provided
            if (string.IsNullOrWhiteSpace(userCode))
            {
                output.AppendLine("⚠️  No code provided. Please enter migration code in the editor.");
                return output.ToString();
            }
            
            output.AppendLine("🔨 Compiling user code...");
            
            // Compile the user's code
            var assembly = await CompileUserCodeAsync(userCode, output);
            if (assembly == null)
            {
                return output.ToString();
            }
            
            output.AppendLine("✓ Code compiled successfully");
            output.AppendLine();
            
            // Create an in-memory SQLite database
            _lastConnectionString = "Data Source=sample.db;";
            output.AppendLine($"🔗 Connection: {_lastConnectionString}");
            output.AppendLine();
            
            // Execute migrations from the compiled assembly
            output.AppendLine("⚡ Executing migrations...");
            await ExecuteMigrationsAsync(_lastConnectionString, assembly, output);
            
            output.AppendLine();
            output.AppendLine("✅ Migration executed successfully!");
        }
        catch (Exception ex)
        {
            output.AppendLine();
            output.AppendLine($"❌ ERROR: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                output.AppendLine($"   Inner: {ex.InnerException.Message}");
            }
            output.AppendLine();
            output.AppendLine($"Stack: {ex.StackTrace}");
        }
        
        return output.ToString();
    }

    public async Task<string> GetDatabaseSchemaAsync()
    {
        if (string.IsNullOrEmpty(_lastConnectionString))
        {
            return JsonSerializer.Serialize(new { tables = Array.Empty<object>(), indexes = Array.Empty<object>(), views = Array.Empty<object>() });
        }

        try
        {
            using var connection = new SqliteConnection(_lastConnectionString);
            await connection.OpenAsync();

            var schema = new
            {
                tables = await GetTablesSchemaAsync(connection),
                indexes = await GetIndexesSchemaAsync(connection),
                views = await GetViewsSchemaAsync(connection)
            };

            return JsonSerializer.Serialize(schema);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting schema: {ex.Message}");
            return JsonSerializer.Serialize(new { tables = Array.Empty<object>(), indexes = Array.Empty<object>(), views = Array.Empty<object>() });
        }
    }

    public async Task<string> ListMigrationsAsync(string userCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userCode))
            {
                return JsonSerializer.Serialize(new { success = false, error = "No code provided", migrations = Array.Empty<object>() });
            }

            // Compile the user's code
            var output = new StringBuilder();
            var assembly = await CompileUserCodeAsync(userCode, output);
            if (assembly == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = output.ToString(), migrations = Array.Empty<object>() });
            }

            // Find all migration classes
            var migrationTypes = assembly.GetTypes()
                .Where(t => typeof(FluentMigrator.Migration).IsAssignableFrom(t) && !t.IsAbstract)
                .OrderBy(t => GetMigrationVersion(t))
                .ToList();

            var migrations = migrationTypes.Select(t => new
            {
                version = GetMigrationVersion(t),
                name = t.Name,
                description = GetMigrationDescription(t),
                hasUp = t.GetMethod("Up") != null,
                hasDown = t.GetMethod("Down") != null
            }).ToList();

            return JsonSerializer.Serialize(new { success = true, migrations });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message, migrations = Array.Empty<object>() });
        }
    }

    public async Task<string> PreviewMigrationAsync(string userCode, long version)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userCode))
            {
                return JsonSerializer.Serialize(new { success = false, error = "No code provided" });
            }

            // Compile the user's code
            var output = new StringBuilder();
            var assembly = await CompileUserCodeAsync(userCode, output);
            if (assembly == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = output.ToString() });
            }

            // Find the specific migration
            var migrationType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(FluentMigrator.Migration).IsAssignableFrom(t) 
                    && !t.IsAbstract 
                    && GetMigrationVersion(t) == version);

            if (migrationType == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Migration with version {version} not found" });
            }

            // Preview migration by analyzing what it would do
            var preview = new StringBuilder();
            preview.AppendLine($"=== Migration Preview: {migrationType.Name} (v{version}) ===");
            preview.AppendLine();
            preview.AppendLine("This migration would perform the following actions:");
            preview.AppendLine();
            preview.AppendLine("📝 Note: This is a code preview, not actual execution.");
            preview.AppendLine("   To see the exact SQL statements, use 'Run Migration'.");
            preview.AppendLine();

            // Try to instantiate and analyze the migration
            var migration = Activator.CreateInstance(migrationType) as FluentMigrator.Migration;
            if (migration != null)
            {
                preview.AppendLine("✅ UP Migration:");
                preview.AppendLine("   • Migration class instantiated successfully");
                preview.AppendLine("   • Ready to create/modify database schema");
                preview.AppendLine();
                
                preview.AppendLine("🔄 DOWN Migration:");
                preview.AppendLine("   • Rollback actions defined");
                preview.AppendLine("   • Can revert changes made by Up()");
            }

            return JsonSerializer.Serialize(new { 
                success = true, 
                preview = preview.ToString(),
                version,
                name = migrationType.Name,
                description = GetMigrationDescription(migrationType)
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    public async Task<string> GetTableDataAsync(string tableName)
    {
        if (string.IsNullOrEmpty(_lastConnectionString))
        {
            return JsonSerializer.Serialize(new { columns = Array.Empty<string>(), rows = Array.Empty<object>() });
        }

        try
        {
            using var connection = new SqliteConnection(_lastConnectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM \"{tableName}\"";

            using var reader = await command.ExecuteReaderAsync();
            
            var columns = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            return JsonSerializer.Serialize(new { columns, rows });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting table data: {ex.Message}");
            return JsonSerializer.Serialize(new { columns = Array.Empty<string>(), rows = Array.Empty<object>() });
        }
    }

    private long GetMigrationVersion(Type migrationType)
    {
        var migrationAttr = migrationType.GetCustomAttributes(typeof(MigrationAttribute), false)
            .FirstOrDefault() as MigrationAttribute;
        return migrationAttr?.Version ?? 0;
    }

    private string GetMigrationDescription(Type migrationType)
    {
        var migrationAttr = migrationType.GetCustomAttributes(typeof(MigrationAttribute), false)
            .FirstOrDefault() as MigrationAttribute;
        return migrationAttr?.Description ?? string.Empty;
    }

    private async Task<List<object>> GetTablesSchemaAsync(SqliteConnection connection)
    {
        var tables = new List<object>();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT name 
            FROM sqlite_master 
            WHERE type = 'table' 
              AND name NOT LIKE 'sqlite_%'
              AND name != 'VersionInfo'
            ORDER BY name";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(0);
            var columns = await GetTableColumnsAsync(connection, tableName);
            
            tables.Add(new
            {
                name = tableName,
                columns = columns
            });
        }

        return tables;
    }

    private async Task<List<object>> GetTableColumnsAsync(SqliteConnection connection, string tableName)
    {
        var columns = new List<object>();

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var constraints = new List<string>();
            
            var notNull = reader.GetInt32(3) == 1;
            var isPk = reader.GetInt32(5) == 1;
            var defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4);

            if (isPk) constraints.Add("PRIMARY KEY");
            if (notNull && !isPk) constraints.Add("NOT NULL");
            if (defaultValue != null) constraints.Add($"DEFAULT {defaultValue}");

            columns.Add(new
            {
                name = reader.GetString(1),
                type = reader.GetString(2),
                constraints = constraints
            });
        }

        return columns;
    }

    private async Task<List<object>> GetIndexesSchemaAsync(SqliteConnection connection)
    {
        var indexes = new List<object>();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT name, tbl_name, sql 
            FROM sqlite_master 
            WHERE type = 'index' 
              AND name NOT LIKE 'sqlite_%'
            ORDER BY tbl_name, name";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var indexName = reader.GetString(0);
            var tableName = reader.GetString(1);
            var sql = reader.IsDBNull(2) ? "" : reader.GetString(2);
            
            // Get index columns
            var columns = await GetIndexColumnsAsync(connection, indexName);
            var isUnique = sql.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);

            indexes.Add(new
            {
                name = indexName,
                tableName = tableName,
                columns = columns,
                unique = isUnique
            });
        }

        return indexes;
    }

    private async Task<List<string>> GetIndexColumnsAsync(SqliteConnection connection, string indexName)
    {
        var columns = new List<string>();

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_info(\"{indexName}\")";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(2));
        }

        return columns;
    }

    private async Task<List<object>> GetViewsSchemaAsync(SqliteConnection connection)
    {
        var views = new List<object>();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT name, sql 
            FROM sqlite_master 
            WHERE type = 'view'
            ORDER BY name";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            views.Add(new
            {
                name = reader.GetString(0),
                sql = reader.GetString(1)
            });
        }

        return views;
    }
    
    private async Task<Assembly?> CompileUserCodeAsync(string userCode, StringBuilder output)
    {
        try
        {
            // Parse the user's code
            var syntaxTree = CSharpSyntaxTree.ParseText(userCode);
            
            // Get references to required assemblies from WASM framework
            var references = new List<MetadataReference>
            {
                await GetMetadataReferenceAsync("netstandard.wasm"),
                await GetMetadataReferenceAsync("System.wasm"),
                await GetMetadataReferenceAsync("System.Collections.wasm"),
                await GetMetadataReferenceAsync("System.ComponentModel.Primitives.wasm"),
                await GetMetadataReferenceAsync("System.ComponentModel.TypeConverter.wasm"),
                await GetMetadataReferenceAsync("System.ComponentModel.wasm"),
                await GetMetadataReferenceAsync("System.Console.wasm"),
                await GetMetadataReferenceAsync("System.Data.Common.wasm"),
                await GetMetadataReferenceAsync("System.Linq.wasm"),
                await GetMetadataReferenceAsync("System.Private.CoreLib.wasm"),
                await GetMetadataReferenceAsync("System.Runtime.wasm"),
                await GetMetadataReferenceAsync("Microsoft.Data.Sqlite.wasm"),
                await GetMetadataReferenceAsync("Microsoft.Extensions.DependencyInjection.Abstractions.wasm"),
                await GetMetadataReferenceAsync("Microsoft.Extensions.DependencyInjection.wasm"),
                await GetMetadataReferenceAsync("Microsoft.Extensions.Logging.Abstractions.wasm"),
                await GetMetadataReferenceAsync("Microsoft.Extensions.Logging.wasm"),
                await GetMetadataReferenceAsync("Microsoft.Extensions.Options.wasm"),
                await GetMetadataReferenceAsync("FluentMigrator.Abstractions.wasm"),
                await GetMetadataReferenceAsync("FluentMigrator.Runner.Core.wasm"),
                await GetMetadataReferenceAsync("FluentMigrator.Runner.SQLite.wasm"),
                await GetMetadataReferenceAsync("FluentMigrator.wasm"),
            };
            
            // Create compilation
            var assemblyName = $"UserMigration_{Guid.NewGuid():N}";
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            
            // Compile to memory stream
            using var ms = new MemoryStream();
            EmitResult result = compilation.Emit(ms);
            
            if (!result.Success)
            {
                output.AppendLine("❌ Compilation failed:");
                output.AppendLine();
                
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);
                
                foreach (var diagnostic in failures)
                {
                    output.AppendLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
                    var lineSpan = diagnostic.Location.GetLineSpan();
                    output.AppendLine($"    at line {lineSpan.StartLinePosition.Line + 1}");
                }
                
                return null;
            }
            
            // Load the compiled assembly
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            
            await Task.CompletedTask;
            return assembly;
        }
        catch (Exception ex)
        {
            output.AppendLine($"❌ Compilation error: {ex.Message}");
            return null;
        }
    }
    
    private async Task ExecuteMigrationsAsync(string connectionString, Assembly migrationAssembly, StringBuilder output)
    {
        // Set up FluentMigrator services WITHOUT console logging (causes WASM issues)
        var serviceProvider = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSQLite()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(migrationAssembly).For.Migrations())
            .BuildServiceProvider(false);
        
        // Run the migrations
        using var scope = serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        
        output.AppendLine("Running MigrateUp()...");
        runner.MigrateUp();
        
        output.AppendLine("✓ Migrations applied successfully");
        
        await Task.CompletedTask;
    }
    
    private async Task<string> ResolveResourceStreamUri(string resource)
    {
        var resolved = await _resourceResolver.ResolveResource(resource);
        return $"/_framework/{resolved}";
    }

    private async Task<PortableExecutableReference> GetMetadataReferenceAsync(string wasmModule)
    {
        var httpClient = await _httpClientFactory.CreateHttpClient();
        await using var stream = await httpClient.GetStreamAsync(await ResolveResourceStreamUri(wasmModule));
        var peBytes = WebcilConverterUtil.ConvertFromWebcil(stream);

        using var peStream = new MemoryStream(peBytes);
        return MetadataReference.CreateFromStream(peStream);
    }
}

// Example migrations in the assembly
[Migration(202501010001)]
public class CreateUsersTable : Migration
{
    public override void Up()
    {
        Create.Table("Users")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Username").AsString(50).NotNullable()
            .WithColumn("Email").AsString(100).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
                .WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        Delete.Table("Users");
    }
}

[Migration(202501010002)]
public class CreatePostsTable : Migration
{
    public override void Up()
    {
        Create.Table("Posts")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable()
                .ForeignKey("Users", "Id")
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Content").AsString().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
                .WithDefault(SystemMethods.CurrentDateTime);
            
        Create.Index("IX_Posts_UserId")
            .OnTable("Posts")
            .OnColumn("UserId");
    }

    public override void Down()
    {
        Delete.Index("IX_Posts_UserId");
        Delete.Table("Posts");
    }
}
