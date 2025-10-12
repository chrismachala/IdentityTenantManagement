using System;
using System.Collections.Generic;

namespace IdentityTenantManagement.EFCore;

public partial class User
{
    public Guid GUserId { get; set; }

    public string SEmail { get; set; } = null!;

    public string? SFirstName { get; set; }

    public string? SLastName { get; set; }
}
