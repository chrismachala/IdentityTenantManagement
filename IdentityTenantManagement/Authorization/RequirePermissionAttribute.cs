using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using IdentityTenantManagement.Services;
using System.Security.Claims;

namespace IdentityTenantManagement.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _permissions;
    private readonly bool _requireAll;

    public RequirePermissionAttribute(params string[] permissions)
    {
        _permissions = permissions;
        _requireAll = false;
    }

    public RequirePermissionAttribute(bool requireAll, params string[] permissions)
    {
        _permissions = permissions;
        _requireAll = requireAll;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var permissionService = context.HttpContext.RequestServices.GetService<PermissionService>();

        if (permissionService == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        // Get user ID from claims first, then fall back to custom header
        var userIdValue = context.HttpContext.User.FindFirst("user_id")?.Value
            ?? context.HttpContext.User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdValue))
        {
            userIdValue = context.HttpContext.Request.Headers["X-User-Id"].FirstOrDefault();
        }

        // Get tenant ID from claims first, then fall back to custom header
        var tenantIdValue = context.HttpContext.User.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrEmpty(tenantIdValue))
        {
            tenantIdValue = context.HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        }

        if (string.IsNullOrEmpty(userIdValue) || string.IsNullOrEmpty(tenantIdValue))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!Guid.TryParse(userIdValue, out var userId) || !Guid.TryParse(tenantIdValue, out var tenantId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Check permissions
        bool hasPermission;
        if (_requireAll)
        {
            hasPermission = await permissionService.UserHasAllPermissionsAsync(tenantId, userId, _permissions);
        }
        else
        {
            hasPermission = await permissionService.UserHasAnyPermissionAsync(tenantId, userId, _permissions);
        }

        if (!hasPermission)
        {
            context.Result = new ForbidResult();
        }
    }
}