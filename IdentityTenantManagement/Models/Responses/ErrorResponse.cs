namespace IdentityTenantManagement.Models.Responses;

public class ErrorResponse
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string? TraceId { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
}