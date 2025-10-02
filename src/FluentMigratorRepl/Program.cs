using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FluentMigratorRepl;
using FluentMigratorRepl.Services;
using FluentMigratorRepl.JSInterop;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// No UI components needed - this is headless WASM for Vue
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<MigrationExecutor>();
builder.Services.AddScoped<MigrationInterop>();

var host = builder.Build();

// Export the migration interop to JavaScript
var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
var migrationInterop = host.Services.GetRequiredService<MigrationInterop>();

// Make the interop available to JavaScript
await jsRuntime.InvokeVoidAsync("window.setMigrationInterop", DotNetObjectReference.Create(migrationInterop));

await host.RunAsync();
