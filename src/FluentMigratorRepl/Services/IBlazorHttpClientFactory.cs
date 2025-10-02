using System.Net.Http;
using System.Threading.Tasks;

namespace FluentMigratorRepl.Services;

public interface IBlazorHttpClientFactory
{
    Task<HttpClient> CreateHttpClient();
}