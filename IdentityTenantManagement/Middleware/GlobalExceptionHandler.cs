using System.Net;
using System.Text.Json;
using IdentityTenantManagement.Exceptions;
using IdentityTenantManagement.Models.Responses;
using KeycloakAdapter.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using NotFoundException = IdentityTenantManagement.Exceptions.NotFoundException;

namespace IdentityTenantManagement.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Exception occurred: {Message}",
            exception.Message);

        var errorResponse = CreateErrorResponse(exception, httpContext);

        httpContext.Response.StatusCode = errorResponse.Status;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }),
            cancellationToken);

        return true;
    }

    private ErrorResponse CreateErrorResponse(Exception exception, HttpContext context)
    {
        var traceId = context.TraceIdentifier;

        return exception switch
        {
            NotFoundException notFoundEx => new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Title = "Resource Not Found",
                Status = (int)HttpStatusCode.NotFound,
                Detail = notFoundEx.Message,
                TraceId = traceId
            },

            ValidationException validationEx => new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Validation Error",
                Status = (int)HttpStatusCode.BadRequest,
                Detail = validationEx.Message,
                Errors = validationEx.Errors,
                TraceId = traceId
            },

            KeycloakException keycloakEx => new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Keycloak Service Error",
                Status = (int)keycloakEx.StatusCode,
                Detail = keycloakEx.Message,
                TraceId = traceId
            },

            HttpRequestException httpEx => new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "External Service Error",
                Status = (int)HttpStatusCode.BadGateway,
                Detail = "An error occurred while communicating with an external service.",
                TraceId = traceId
            },

            _ => new ErrorResponse
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal Server Error",
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = "An unexpected error occurred. Please contact support if the problem persists.",
                TraceId = traceId
            }
        };
    }
}