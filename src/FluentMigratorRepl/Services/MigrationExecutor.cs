using System.Reflection;
using System.Text;
using FluentMigrator.Runner;
using FluentMigrator;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace FluentMigratorRepl.Services;

public class MigrationExecutor
{
    public async Task<string> ExecuteMigrationCodeAsync(string userCode)
    {
        var output = new StringBuilder();
        
        try
        {
            output.AppendLine("=== FluentMigrator REPL - Executing Migration ===");
            output.AppendLine();
            
            // Create an in-memory SQLite database
            var connectionString = "Data Source=:memory:";
            output.AppendLine($"📊 Database: In-Memory SQLite");
            output.AppendLine($"🔗 Connection: {connectionString}");
            output.AppendLine();
            
            // Execute migrations
            output.AppendLine("⚡ Executing migrations...");
            await ExecuteMigrationsAsync(connectionString, output);
            
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
        }
        
        return output.ToString();
    }
    
    private async Task ExecuteMigrationsAsync(string connectionString, StringBuilder output)
    {
        // Set up FluentMigrator services WITHOUT console logging (causes WASM issues)
        var serviceProvider = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSQLite()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(Assembly.GetExecutingAssembly()).For.Migrations())
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
