using System.Threading.RateLimiting;
using IdentityTenantManagement.Middleware;
using IdentityTenantManagement.Services;
using IdentityTenantManagementDatabase.DbContexts;
using KeycloakAdapter.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Identity Tenant Management API",
        Version = "v1",
        Description = "B2B SaaS multi-tenant identity and access management platform. " +
                      "Supports tenant onboarding, user management, role-based permissions, and identity provider integration.",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Identity Tenant Management Team"
        }
    });
});

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Add a fixed window rate limiter for login endpoint
    options.AddFixedWindowLimiter("login", options =>
    {
        options.PermitLimit = 5;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 0; // No queueing
    });
});

// Add CORS for Blazor app
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp", policy =>
    {
        policy.WithOrigins("https://localhost:5280", "http://localhost:5104")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
// dev env adds user secrets
//builder.Configuration.AddUserSecrets<Program>(true);

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

// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

    // Add Strict-Transport-Security only in production
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    await next();
});

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors("AllowBlazorApp");
app.UseAuthorization();
app.MapControllers();

app.Run();