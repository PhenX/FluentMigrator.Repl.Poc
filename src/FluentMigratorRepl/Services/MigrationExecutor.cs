using System.Text;
using System.Text.RegularExpressions;

namespace FluentMigratorRepl.Services;

public class MigrationExecutor
{
    public async Task<string> ExecuteMigrationCodeAsync(string userCode)
    {
        var output = new StringBuilder();
        
        try
        {
            output.AppendLine("=== FluentMigrator REPL - Migration Analysis ===");
            output.AppendLine();
            output.AppendLine("📝 Analyzing your migration code...");
            output.AppendLine();
            
            // Simulate a small delay for realism
            await Task.Delay(500);
            
            // Parse and analyze the migration code
            var analysis = AnalyzeMigrationCode(userCode);
            
            output.AppendLine(analysis);
            output.AppendLine();
            output.AppendLine("✅ Migration code analysis complete!");
            output.AppendLine();
            output.AppendLine("💡 NOTE: This is a demonstration environment showing what your migration would do.");
            output.AppendLine("   In a full production environment with proper SQLite WASM support,");
            output.AppendLine("   the migration would execute against an in-memory database.");
        }
        catch (Exception ex)
        {
            output.AppendLine();
            output.AppendLine($"❌ ERROR: {ex.GetType().Name}: {ex.Message}");
        }
        
        return output.ToString();
    }
    
    private string AnalyzeMigrationCode(string code)
    {
        var output = new StringBuilder();
        
        // Extract migration version
        var versionMatch = Regex.Match(code, @"\[Migration\((\d+)\)\]");
        if (versionMatch.Success)
        {
            output.AppendLine($"📌 Migration Version: {versionMatch.Groups[1].Value}");
            output.AppendLine();
        }
        
        // Extract class name
        var classMatch = Regex.Match(code, @"class\s+(\w+)\s*:");
        if (classMatch.Success)
        {
            output.AppendLine($"📦 Migration Class: {classMatch.Groups[1].Value}");
            output.AppendLine();
        }
        
        output.AppendLine("🔨 UP Migration Actions:");
        output.AppendLine("─────────────────────────");
        
        // Analyze Create.Table statements
        var tableMatches = Regex.Matches(code, @"Create\.Table\(""([^""]+)""\)");
        foreach (Match match in tableMatches)
        {
            var tableName = match.Groups[1].Value;
            output.AppendLine($"  ✓ CREATE TABLE: {tableName}");
            
            // Find columns for this table
            var tableContext = code.Substring(match.Index);
            var nextTableOrDown = Regex.Match(tableContext.Substring(1), @"(Create\.Table|public override void Down)");
            var tableSection = nextTableOrDown.Success 
                ? tableContext.Substring(0, nextTableOrDown.Index + 1)
                : tableContext;
            
            // Extract column definitions
            var columnMatches = Regex.Matches(tableSection, @"\.WithColumn\(""([^""]+)""\)\.As(\w+)");
            foreach (Match colMatch in columnMatches)
            {
                var columnName = colMatch.Groups[1].Value;
                var columnType = colMatch.Groups[2].Value;
                
                var constraints = new List<string>();
                
                // Check for constraints in the line
                var columnLine = GetLineContaining(tableSection, colMatch.Index);
                if (columnLine.Contains(".PrimaryKey()")) constraints.Add("PRIMARY KEY");
                if (columnLine.Contains(".Identity()")) constraints.Add("IDENTITY");
                if (columnLine.Contains(".NotNullable()")) constraints.Add("NOT NULL");
                if (columnLine.Contains(".Nullable()")) constraints.Add("NULLABLE");
                if (columnLine.Contains(".Unique()")) constraints.Add("UNIQUE");
                if (columnLine.Contains(".ForeignKey(")) constraints.Add("FOREIGN KEY");
                if (columnLine.Contains(".WithDefault(")) constraints.Add("DEFAULT");
                
                var constraintStr = constraints.Count > 0 ? $" [{string.Join(", ", constraints)}]" : "";
                output.AppendLine($"    • {columnName}: {columnType}{constraintStr}");
            }
            output.AppendLine();
        }
        
        // Analyze Create.Index statements
        var indexMatches = Regex.Matches(code, @"Create\.Index\(""([^""]+)""\)");
        if (indexMatches.Count > 0)
        {
            foreach (Match match in indexMatches)
            {
                var indexName = match.Groups[1].Value;
                var indexContext = code.Substring(match.Index);
                var tableMatch = Regex.Match(indexContext, @"\.OnTable\(""([^""]+)""\)");
                var columnMatch = Regex.Match(indexContext, @"\.OnColumn\(""([^""]+)""\)");
                
                var tableName = tableMatch.Success ? tableMatch.Groups[1].Value : "?";
                var columnName = columnMatch.Success ? columnMatch.Groups[1].Value : "?";
                var unique = indexContext.Contains(".Unique()") ? " [UNIQUE]" : "";
                var descending = indexContext.Contains(".Descending()") ? " [DESC]" : "";
                
                output.AppendLine($"  ✓ CREATE INDEX: {indexName} ON {tableName}({columnName}){unique}{descending}");
            }
            output.AppendLine();
        }
        
        // Check for Down migration
        if (code.Contains("public override void Down()"))
        {
            output.AppendLine();
            output.AppendLine("🔄 DOWN Migration Actions:");
            output.AppendLine("─────────────────────────");
            
            var deleteMatches = Regex.Matches(code, @"Delete\.Table\(""([^""]+)""\)");
            foreach (Match match in deleteMatches)
            {
                output.AppendLine($"  ✓ DROP TABLE: {match.Groups[1].Value}");
            }
        }
        
        return output.ToString();
    }
    
    private string GetLineContaining(string text, int index)
    {
        var start = text.LastIndexOf('\n', index) + 1;
        var end = text.IndexOf('\n', index);
        if (end == -1) end = text.Length;
        return text.Substring(start, end - start);
    }
}
