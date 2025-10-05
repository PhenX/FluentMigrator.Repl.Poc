using System.Reflection;
using System.Text.Json;
using FluentMigrator.Runner;
using FluentMigratorRepl.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using FluentMigratorRepl.Webcil;

namespace FluentMigratorRepl.Services;

public class MigrationExecutor
{
    private readonly IResourceResolver _resourceResolver;
    private readonly IBlazorHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private string _lastConnectionString = "";
    
    private static IDictionary<string, PortableExecutableReference> _wasmReferenceCache = new Dictionary<string, PortableExecutableReference>();

    public MigrationExecutor(IResourceResolver resourceResolver, IBlazorHttpClientFactory httpClientFactory, ILogger logger)
    {
        _resourceResolver = resourceResolver;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ExecuteMigrationCodeAsync(string dbName, string userCode, MigrationRunType runType = MigrationRunType.Up)
    {
        try
        {
            // Validate user code is provided
            if (string.IsNullOrWhiteSpace(userCode))
            {
                _logger.LogWarning("No code provided. Please enter migration code in the editor.");
                return;
            }
            
            _logger.LogInformation("🔨 Compiling user code...");
            
            // Compile the user's code
            var assembly = await CompileUserCodeAsync(userCode);
            if (assembly == null)
            {
                return;
            }
            
            _logger.LogInformation("✅ Code compiled successfully");
            
            // Create an in-memory SQLite database
            _lastConnectionString = $"Data Source={dbName}.db;";
            _logger.LogInformation("🔗 Connection: {LastConnectionString}", _lastConnectionString);
            
            // Execute migrations from the compiled assembly
            await ExecuteMigrationsAsync(_lastConnectionString, assembly, runType);
            
            _logger.LogInformation("✅ Migration executed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured during execution");
        }
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

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_info(\"{indexName}\")";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(2));
        }

        return columns;
    }

    private async Task<List<object>> GetViewsSchemaAsync(SqliteConnection connection)
    {
        var views = new List<object>();

        await using var command = connection.CreateCommand();
        
        command.CommandText = @"
            SELECT name, sql 
            FROM sqlite_master 
            WHERE type = 'view'
            ORDER BY name";

        await using var reader = await command.ExecuteReaderAsync();
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
    
    private async Task<Assembly?> CompileUserCodeAsync(string userCode)
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
                _logger.LogError("Compilation failed");
                
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);
                
                foreach (var diagnostic in failures)
                {
                    var lineSpan = diagnostic.Location.GetLineSpan();
                    _logger.LogInformation("{DiagnosticId}: {GetMessage} at line {Line}", diagnostic.Id, diagnostic.GetMessage(), lineSpan.StartLinePosition.Line + 1);
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
            _logger.LogError("Compilation error: {ExMessage}", ex.Message);
            return null;
        }
    }
    
    private async Task ExecuteMigrationsAsync(string connectionString, Assembly migrationAssembly, MigrationRunType runType)
    {
        // Set up FluentMigrator services WITHOUT console logging (causes WASM issues)
        var serviceProvider = new ServiceCollection()
            .AddLogging(lb => lb
                .SetMinimumLevel(LogLevel.Information)
                .AddProvider(OutputLoggerProvider.Instance))
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSQLite()
                .WithGlobalConnectionString(connectionString)
                .AsGlobalPreview(MigrationRunType.Preview == runType)
                .ScanIn(migrationAssembly).For.Migrations())
            .BuildServiceProvider(false);
        
        // Run the migrations
        using var scope = serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        
        switch (runType)
        {
            case MigrationRunType.List:
                _logger.LogInformation("Listing migrations...");
                runner.ListMigrations();
                return;
            
            case MigrationRunType.Preview:
                _logger.LogInformation("Previewing migrations...");
                runner.MigrateUp();
                return;
            
            case MigrationRunType.Up:
                _logger.LogInformation("Running MigrateUp()...");
                runner.MigrateUp();
                return;
            default:
                await Task.CompletedTask;
                break;
        }
    }
    
    private async Task<string> ResolveResourceStreamUri(string resource)
    {
        var resolved = await _resourceResolver.ResolveResource(resource);
        return $"./_framework/{resolved}";
    }

    private async Task<PortableExecutableReference> GetMetadataReferenceAsync(string wasmModule)
    {
        if (_wasmReferenceCache.TryGetValue(wasmModule, out var cachedReference))
        {
            return cachedReference;
        }
        
        var httpClient = await _httpClientFactory.CreateHttpClient();
        await using var stream = await httpClient.GetStreamAsync(await ResolveResourceStreamUri(wasmModule));
        var peBytes = WebcilConverterUtil.ConvertFromWebcil(stream);
        
        using var peStream = new MemoryStream(peBytes);
        
        return _wasmReferenceCache[wasmModule] = MetadataReference.CreateFromStream(peStream);
    }
}
