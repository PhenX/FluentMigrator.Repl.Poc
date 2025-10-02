using System.Reflection;
using System.Text;
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
            var connectionString = "Data Source=:memory:";
            output.AppendLine($"📊 Database: In-Memory SQLite");
            output.AppendLine($"🔗 Connection: {connectionString}");
            output.AppendLine();
            
            // Execute migrations from the compiled assembly
            output.AppendLine("⚡ Executing migrations...");
            await ExecuteMigrationsAsync(connectionString, assembly, output);
            
            output.AppendLine();
            output.AppendLine("✅ Migration executed successfully!");
            output.AppendLine();
            
            // Show the database schema
            output.AppendLine("=== Database Schema ===");
            await ShowDatabaseSchemaAsync(connectionString, output);
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
    
    private async Task ShowDatabaseSchemaAsync(string connectionString, StringBuilder output)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT type, name, sql 
            FROM sqlite_master 
            WHERE type IN ('table', 'index') 
              AND name NOT LIKE 'sqlite_%'
              AND name NOT LIKE 'VersionInfo'
            ORDER BY type DESC, name";
        
        using var reader = await command.ExecuteReaderAsync();
        
        var hasResults = false;
        while (await reader.ReadAsync())
        {
            hasResults = true;
            var type = reader.GetString(0);
            var name = reader.GetString(1);
            var sql = reader.IsDBNull(2) ? "" : reader.GetString(2);
            
            if (type == "table")
            {
                output.AppendLine();
                output.AppendLine($"📋 TABLE: {name}");
                output.AppendLine("─────────────────────────");
                if (!string.IsNullOrWhiteSpace(sql))
                {
                    output.AppendLine(sql);
                }
            }
            else if (type == "index")
            {
                output.AppendLine();
                output.AppendLine($"🔍 INDEX: {name}");
                if (!string.IsNullOrWhiteSpace(sql))
                {
                    output.AppendLine($"   {sql}");
                }
            }
        }
        
        if (!hasResults)
        {
            output.AppendLine();
            output.AppendLine("(No tables or indexes created)");
        }
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
