namespace IdentityTenantManagement.Models.Users;

/// <summary>
/// Summary information about a user in a tenant, including their inactive status.
/// </summary>
public class UserSummaryDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? InactiveAt { get; set; }
    public DateTime JoinedAt { get; set; }
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// Paginated response for user lists
/// </summary>
public class PaginatedUsersResponse
{
    public List<UserSummaryDto> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
