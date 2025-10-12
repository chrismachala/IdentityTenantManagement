using System.Net;

namespace IdentityTenantManagement.Exceptions;

public class KeycloakException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ResponseContent { get; }

    public KeycloakException(string message, HttpStatusCode statusCode, string? responseContent = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }

    public KeycloakException(string message, HttpStatusCode statusCode, string? responseContent, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }
}