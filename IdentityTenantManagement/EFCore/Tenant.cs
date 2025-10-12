using System;
using System.Collections.Generic;

namespace IdentityTenantManagement.EFCore;

public partial class Tenant
{
    public Guid GTenantId { get; set; }

    public string SName { get; set; } = null!;

    public string? SDomain { get; set; }
}
