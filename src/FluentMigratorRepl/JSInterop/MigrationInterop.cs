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
    public async Task<string> ExecuteMigrationAsync(string code)
    {
        return await _executor.ExecuteMigrationCodeAsync(code);
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
