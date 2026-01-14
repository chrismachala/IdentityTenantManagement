using IdentityTenantManagement.BlazorApp.Components;
using IdentityTenantManagement.BlazorApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure HttpClient for API calls
var apiBaseUrl = builder.Configuration.GetValue<string>("ApiSettings:BaseUrl") ?? "https://localhost:5280";

builder.Services.AddHttpClient<OnboardingApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Register named HttpClient for authentication (used by AuthenticationService)
builder.Services.AddHttpClient("AuthenticationApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Register authentication service as Singleton to maintain state across the Blazor circuit
// Note: For multi-user production, consider using AuthenticationStateProvider instead
builder.Services.AddSingleton<AuthenticationService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("AuthenticationApi");
    return new AuthenticationService(httpClient);
});

// Register the HttpClient that pages will use via @inject HttpClient
// This HttpClient includes the AuthenticatedHttpClientHandler which adds auth headers
builder.Services.AddScoped<HttpClient>(sp =>
{
    var authService = sp.GetRequiredService<AuthenticationService>();
    var handler = new AuthenticatedHttpClientHandler(authService)
    {
        InnerHandler = new HttpClientHandler()
    };
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
