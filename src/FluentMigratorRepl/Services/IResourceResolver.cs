using System.Threading.Tasks;

namespace FluentMigratorRepl.Services;

public interface IResourceResolver
{
    public Task<string> ResolveResource(string resource);
}