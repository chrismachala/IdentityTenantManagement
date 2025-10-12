using IdentityTenantManagement.EFCore;
using IdentityTenantManagement.Helpers;
using IdentityTenantManagement.Middleware;
using IdentityTenantManagement.Models.Keycloak;
using IdentityTenantManagement.Services;
using IdentityTenantManagement.Services.KeycloakServices;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
// dev env adds user secrets
//builder.Configuration.AddUserSecrets<Program>(true);

builder.Services.Configure<KeycloakConfig>(builder.Configuration.GetSection("KeycloakConfig"));
builder.Services.AddHttpClient();  
builder.Services.AddScoped<IKCRequestHelper, KCRequestHelper>();

builder.Services.AddKeycloakIntegration(builder.Configuration);
builder.Services.AddApplicationServices();

var conString = builder.Configuration.GetConnectionString("OnboardingDatabase") ??
                throw new InvalidOperationException("Connection string 'OnboardingDatabase'" +
                                                    " not found.");
builder.Services.AddDbContext<IdentityTenantManagementContext>(options =>
    options.UseSqlServer(conString));
  
var app = builder.Build();
 

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
}); 

// Configure the HTTP request pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();