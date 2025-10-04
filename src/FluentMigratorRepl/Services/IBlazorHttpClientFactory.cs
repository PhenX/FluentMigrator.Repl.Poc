namespace FluentMigratorRepl.Services;

public interface IBlazorHttpClientFactory
{
    Task<HttpClient> CreateHttpClient();
}