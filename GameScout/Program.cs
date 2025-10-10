using DotNetEnv;
using GameScout.Components;
using GameScout.Services;
using GameScout.Services.Abstractions;
using GameScout.Services.Http;
using GameScout.State;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// HttpClient
builder.Services.AddHttpClient<RawgService>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Http:RAWG:BaseUrl"]!);
});
builder.Services.AddHttpClient<CheapSharkService>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Http:CheapShark:BaseUrl"]!);
});

// DI
builder.Services.AddScoped<IGameCatalogService, RawgService>();
builder.Services.AddScoped<IDealsService, CheapSharkService>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ILocalStorage, LocalStorage>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(GameScout.Client._Imports).Assembly);

app.Run();
