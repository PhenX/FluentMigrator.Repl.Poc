using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FluentMigratorRepl.Services;
using FluentMigratorRepl.JSInterop;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// No UI components needed - this is headless WASM for Vue
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register services for Webcil assembly loading
builder.Services.AddSingleton<IResourceResolver, ResourceResolver>();
builder.Services.AddSingleton<IBlazorHttpClientFactory, BlazorHttpClientFactory>();

// Register migration services
builder.Services.AddScoped<MigrationExecutor>();
builder.Services.AddScoped<MigrationInterop>();

var host = builder.Build();

// Export the migration interop to JavaScript
var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
var migrationInterop = host.Services.GetRequiredService<MigrationInterop>();

// Make the interop available to JavaScript
await jsRuntime.InvokeVoidAsync("window.setMigrationInterop", DotNetObjectReference.Create(migrationInterop));

await host.RunAsync();
