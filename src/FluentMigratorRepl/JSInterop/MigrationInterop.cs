using FluentMigratorRepl.Enums;
using Microsoft.JSInterop;
using FluentMigratorRepl.Services;

namespace FluentMigratorRepl.JSInterop;

public class MigrationInterop
{
    private readonly MigrationExecutor _executor;

    public MigrationInterop(MigrationExecutor executor)
    {
        _executor = executor;
    }

    [JSInvokable]
    public async Task<string> ExecuteMigrationAsync(string dbName, string code, MigrationRunType runType = MigrationRunType.Up)
    {
        await _executor.ExecuteMigrationCodeAsync(dbName, code, runType);
        
        return OutputLogger.GetOutput();
    }

    [JSInvokable]
    public async Task<string> PreloadAsync()
    {
        await _executor.PreloadAsync();
        
        return OutputLogger.GetOutput();
    }

    [JSInvokable]
    public async Task<string> GetDatabaseSchemaAsync()
    {
        return await _executor.GetDatabaseSchemaAsync();
    }

    [JSInvokable]
    public async Task<string> GetTableDataAsync(string tableName)
    {
        return await _executor.GetTableDataAsync(tableName);
    }
}
