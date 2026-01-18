using IdentityTenantManagement.Models.Roles;
using IdentityTenantManagementDatabase.Models;
using IdentityTenantManagementDatabase.Repositories;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagement.Services;

public interface IUserRoleService
{
    Task<UserRolesResponse> GetUserRolesAsync(Guid tenantId, Guid userId);
    Task<List<RoleDto>> GetAvailableRolesAsync();
    Task<RoleWithPermissionsDto> GetRoleWithPermissionsAsync(Guid roleId);
    Task<List<PermissionDto>> GetAllPermissionsAsync();
    Task<Guid> CreateRoleAsync(CreateRoleRequest request, string? actorUserId = null);
    Task UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, string? actorUserId = null);
    Task AssignRoleToUserAsync(Guid tenantId, Guid userId, Guid roleId, string? actorUserId = null);
    Task RemoveRoleFromUserAsync(Guid tenantId, Guid userId, Guid roleId, string? actorUserId = null);
    Task AssignPermissionToRoleAsync(Guid roleId, Guid permissionId, string? actorUserId = null);
    Task RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId, string? actorUserId = null);
    Task AssignPermissionToUserAsync(Guid tenantId, Guid userId, Guid permissionId, string? actorUserId = null);
    Task RemovePermissionFromUserAsync(Guid tenantId, Guid userId, Guid permissionId, string? actorUserId = null);
}

public class UserRoleService : IUserRoleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserRoleService> _logger;

    public UserRoleService(
        IUnitOfWork unitOfWork,
        ILogger<UserRoleService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Gets all roles assigned to a user in a specific tenant
    /// </summary>
    public async Task<UserRolesResponse> GetUserRolesAsync(Guid tenantId, Guid userId)
    {
        _logger.LogInformation("Getting roles for user {UserId} in tenant {TenantId}", userId, tenantId);

        // Get TenantUser relationship
        var tenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(tenantId, userId);
        if (tenantUser == null)
        {
            throw new InvalidOperationException($"User {userId} is not a member of tenant {tenantId}.");
        }

        // Get user details
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found.");
        }

        // Get user profile
        var tenantUserProfile = await _unitOfWork.TenantUserProfiles.GetByTenantUserIdAsync(tenantUser.Id);
        var userProfile = tenantUserProfile != null
            ? await _unitOfWork.UserProfiles.GetByIdAsync(tenantUserProfile.UserProfileId)
            : null;

        // Get all roles for this user in this tenant using repository
        var tenantUserRoles = await _unitOfWork.TenantUserRoles.GetByTenantUserIdAsync(tenantUser.Id);
        var roles = tenantUserRoles
            .Select(tur => new UserRoleDto
            {
                Id = tur.Id,
                RoleId = tur.RoleId,
                RoleName = tur.Role.Name,
                RoleDisplayName = tur.Role.DisplayName,
                RoleDescription = tur.Role.Description,
                AssignedAt = tur.AssignedAt
            })
            .ToList();

        // Get direct permissions for this user
        var userPermissions = await _unitOfWork.UserPermissions.GetByTenantUserIdAsync(tenantUser.Id);
        var directPermissions = userPermissions
            .Select(up => new PermissionDto
            {
                Id = up.Permission.Id,
                Name = up.Permission.Name,
                DisplayName = up.Permission.DisplayName,
                Description = up.Permission.Description,
                PermissionGroup = up.Permission.PermissionGroup?.Name ?? string.Empty,
                CreatedAt = up.Permission.CreatedAt
            })
            .ToList();

        return new UserRolesResponse
        {
            UserId = userId,
            Email = user.Email,
            FirstName = userProfile?.FirstName ?? string.Empty,
            LastName = userProfile?.LastName ?? string.Empty,
            Roles = roles,
            DirectPermissions = directPermissions
        };
    }

    /// <summary>
    /// Gets all available roles that can be assigned
    /// </summary>
    public async Task<List<RoleDto>> GetAvailableRolesAsync()
    {
        _logger.LogInformation("Getting all available roles");

        var roles = await _unitOfWork.Roles.GetAllAsync();

        return roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            DisplayName = r.DisplayName,
            Description = r.Description,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    /// <summary>
    /// Assigns a role to a user in a specific tenant
    /// </summary>
    public async Task AssignRoleToUserAsync(Guid tenantId, Guid userId, Guid roleId, string? actorUserId = null)
    {
        _logger.LogInformation("Assigning role {RoleId} to user {UserId} in tenant {TenantId}", roleId, userId, tenantId);

        try
        {
            // Validate role exists
            var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found.");
            }

            // Get TenantUser relationship
            var tenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(tenantId, userId);
            if (tenantUser == null)
            {
                throw new InvalidOperationException($"User {userId} is not a member of tenant {tenantId}.");
            }

            // Check if user already has this role using repository
            var hasRole = await _unitOfWork.TenantUserRoles.HasRoleAsync(tenantUser.Id, roleId);
            if (hasRole)
            {
                _logger.LogWarning("User {UserId} already has role {RoleId} in tenant {TenantId}", userId, roleId, tenantId);
                throw new InvalidOperationException($"User already has the role '{role.DisplayName}'.");
            }

            // Begin transaction
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Assign role using repository
                var tenantUserRole = new TenantUserRole
                {
                    TenantUserId = tenantUser.Id,
                    RoleId = roleId,
                    AssignedAt = DateTime.UtcNow
                };

                await _unitOfWork.TenantUserRoles.AddAsync(tenantUserRole);
                await _unitOfWork.SaveChangesAsync();

                // Audit log
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "role.assigned",
                    resourceType: "TenantUser",
                    resourceId: tenantUser.Id.ToString(),
                    actorUserId: actorUserId != null ? Guid.Parse(actorUserId) : null,
                    tenantId: tenantId,
                    oldValues: null,
                    newValues: $"{{\"roleId\":\"{roleId}\",\"roleName\":\"{role.Name}\"}}");

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully assigned role {RoleId} to user {UserId} in tenant {TenantId}", roleId, userId, tenantId);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleId} to user {UserId} in tenant {TenantId}", roleId, userId, tenantId);
            throw;
        }
    }

    /// <summary>
    /// Removes a role from a user in a specific tenant
    /// </summary>
    public async Task RemoveRoleFromUserAsync(Guid tenantId, Guid userId, Guid roleId, string? actorUserId = null)
    {
        _logger.LogInformation("Removing role {RoleId} from user {UserId} in tenant {TenantId}", roleId, userId, tenantId);

        try
        {
            // Validate role exists
            var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found.");
            }

            // Get TenantUser relationship
            var tenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(tenantId, userId);
            if (tenantUser == null)
            {
                throw new InvalidOperationException($"User {userId} is not a member of tenant {tenantId}.");
            }

            // Find the role assignment using repository
            var tenantUserRole = await _unitOfWork.TenantUserRoles.GetByTenantUserIdAndRoleIdAsync(tenantUser.Id, roleId);
            if (tenantUserRole == null)
            {
                throw new InvalidOperationException($"User does not have the role '{role.DisplayName}'.");
            }

            // Prevent removing the last admin role
            if (role.Name == "org-admin")
            {
                var adminCount = await _unitOfWork.TenantUsers.CountUsersWithRoleInTenantAsync(tenantId, roleId);
                if (adminCount <= 1)
                {
                    throw new InvalidOperationException("Cannot remove the last administrator role from the organization. Assign another administrator first.");
                }
            }

            // Begin transaction
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Remove role using repository
                await _unitOfWork.TenantUserRoles.DeleteAsync(tenantUserRole.Id);
                await _unitOfWork.SaveChangesAsync();

                // Audit log
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "role.removed",
                    resourceType: "TenantUser",
                    resourceId: tenantUser.Id.ToString(),
                    actorUserId: actorUserId != null ? Guid.Parse(actorUserId) : null,
                    tenantId: tenantId,
                    oldValues: $"{{\"roleId\":\"{roleId}\",\"roleName\":\"{role.Name}\"}}",
                    newValues: null);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully removed role {RoleId} from user {UserId} in tenant {TenantId}", roleId, userId, tenantId);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from user {UserId} in tenant {TenantId}", roleId, userId, tenantId);
            throw;
        }
    }

    /// <summary>
    /// Gets a role with all its assigned permissions
    /// </summary>
    public async Task<RoleWithPermissionsDto> GetRoleWithPermissionsAsync(Guid roleId)
    {
        _logger.LogInformation("Getting role {RoleId} with permissions", roleId);

        var role = await _unitOfWork.Roles.GetByIdWithPermissionsAsync(roleId);
        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found.");
        }

        var rolePermissions = await _unitOfWork.RolePermissions.GetByRoleIdAsync(roleId);

        return new RoleWithPermissionsDto
        {
            Id = role.Id,
            Name = role.Name,
            DisplayName = role.DisplayName,
            Description = role.Description,
            CreatedAt = role.CreatedAt,
            Permissions = rolePermissions.Select(rp => new PermissionDto
            {
                Id = rp.Permission.Id,
                Name = rp.Permission.Name,
                DisplayName = rp.Permission.DisplayName,
                Description = rp.Permission.Description,
                PermissionGroup = rp.Permission.PermissionGroup?.Name ?? string.Empty,
                CreatedAt = rp.Permission.CreatedAt
            }).ToList()
        };
    }

    /// <summary>
    /// Gets all available permissions in the system
    /// </summary>
    public async Task<List<PermissionDto>> GetAllPermissionsAsync()
    {
        _logger.LogInformation("Getting all permissions");

        var permissions = await _unitOfWork.Permissions.GetAllAsync();

        return permissions.Select(p => new PermissionDto
        {
            Id = p.Id,
            Name = p.Name,
            DisplayName = p.DisplayName,
            Description = p.Description,
            PermissionGroup = p.PermissionGroup?.Name ?? string.Empty,
            CreatedAt = p.CreatedAt
        }).ToList();
    }

    /// <summary>
    /// Creates a new role with optional permissions
    /// </summary>
    public async Task<Guid> CreateRoleAsync(CreateRoleRequest request, string? actorUserId = null)
    {
        _logger.LogInformation("Creating new role: {RoleName}", request.Name);

        try
        {
            // Check if role name already exists
            var existingRole = await _unitOfWork.Roles.GetByNameAsync(request.Name);
            if (existingRole != null)
            {
                throw new InvalidOperationException($"A role with the name '{request.Name}' already exists.");
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Create the role
                var role = new Role
                {
                    Name = request.Name,
                    DisplayName = request.DisplayName,
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Roles.AddAsync(role);
                await _unitOfWork.SaveChangesAsync();

                // Assign permissions if provided
                if (request.PermissionIds != null && request.PermissionIds.Any())
                {
                    foreach (var permissionId in request.PermissionIds)
                    {
                        var permission = await _unitOfWork.Permissions.GetByIdAsync(permissionId);
                        if (permission == null)
                        {
                            throw new InvalidOperationException($"Permission {permissionId} not found.");
                        }

                        var rolePermission = new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = permissionId,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _unitOfWork.RolePermissions.AddAsync(rolePermission);
                    }

                    await _unitOfWork.SaveChangesAsync();
                }

                // Audit log
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "role.created",
                    resourceType: "Role",
                    resourceId: role.Id.ToString(),
                    actorUserId: actorUserId != null ? Guid.Parse(actorUserId) : null,
                    tenantId: null,
                    oldValues: null,
                    newValues: $"{{\"name\":\"{role.Name}\",\"displayName\":\"{role.DisplayName}\"}}");

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully created role {RoleId}: {RoleName}", role.Id, role.Name);
                return role.Id;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role {RoleName}", request.Name);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing role's basic information
    /// </summary>
    public async Task UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, string? actorUserId = null)
    {
        _logger.LogInformation("Updating role {RoleId}", roleId);

        try
        {
            var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found.");
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var oldValues = $"{{\"displayName\":\"{role.DisplayName}\",\"description\":\"{role.Description}\"}}";

                // Update role properties
                role.DisplayName = request.DisplayName;
                role.Description = request.Description;

                await _unitOfWork.Roles.UpdateAsync(role);
                await _unitOfWork.SaveChangesAsync();

                // Audit log
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "role.updated",
                    resourceType: "Role",
                    resourceId: role.Id.ToString(),
                    actorUserId: actorUserId != null ? Guid.Parse(actorUserId) : null,
                    tenantId: null,
                    oldValues: oldValues,
                    newValues: $"{{\"displayName\":\"{role.DisplayName}\",\"description\":\"{role.Description}\"}}");

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully updated role {RoleId}", roleId);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", roleId);
            throw;
        }
    }

    /// <summary>
    /// Assigns a permission to a role
    /// </summary>
    public async Task AssignPermissionToRoleAsync(Guid roleId, Guid permissionId, string? actorUserId = null)
    {
        _logger.LogInformation("Assigning permission {PermissionId} to role {RoleId}", permissionId, roleId);

        try
        {
            // Validate role exists
            var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found.");
            }

            // Validate permission exists
            var permission = await _unitOfWork.Permissions.GetByIdAsync(permissionId);
            if (permission == null)
            {
                throw new InvalidOperationException($"Permission {permissionId} not found.");
            }

            // Check if role already has this permission
            var hasPermission = await _unitOfWork.RolePermissions.HasPermissionAsync(roleId, permissionId);
            if (hasPermission)
            {
                throw new InvalidOperationException($"Role '{role.DisplayName}' already has permission '{permission.DisplayName}'.");
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var rolePermission = new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.RolePermissions.AddAsync(rolePermission);
                await _unitOfWork.SaveChangesAsync();

                // Audit log
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "permission.assigned",
                    resourceType: "Role",
                    resourceId: roleId.ToString(),
                    actorUserId: actorUserId != null ? Guid.Parse(actorUserId) : null,
                    tenantId: null,
                    oldValues: null,
                    newValues: $"{{\"permissionId\":\"{permissionId}\",\"permissionName\":\"{permission.Name}\"}}");

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully assigned permission {PermissionId} to role {RoleId}", permissionId, roleId);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning permission {PermissionId} to role {RoleId}", permissionId, roleId);
            throw;
        }
    }

    /// <summary>
    /// Removes a permission from a role
    /// </summary>
    public async Task RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId, string? actorUserId = null)
    {
        _logger.LogInformation("Removing permission {PermissionId} from role {RoleId}", permissionId, roleId);

        try
        {
            // Validate role exists
            var role = await _unitOfWork.Roles.GetByIdAsync(roleId);
            if (role == null)
            {
                throw new InvalidOperationException($"Role {roleId} not found.");
            }

            // Validate permission exists
            var permission = await _unitOfWork.Permissions.GetByIdAsync(permissionId);
            if (permission == null)
            {
                throw new InvalidOperationException($"Permission {permissionId} not found.");
            }

            // Find the permission assignment
            var rolePermission = await _unitOfWork.RolePermissions.GetByRoleIdAndPermissionIdAsync(roleId, permissionId);
            if (rolePermission == null)
            {
                throw new InvalidOperationException($"Role '{role.DisplayName}' does not have permission '{permission.DisplayName}'.");
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                await _unitOfWork.RolePermissions.DeleteAsync(rolePermission.Id);
                await _unitOfWork.SaveChangesAsync();

                // Audit log
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "permission.removed",
                    resourceType: "Role",
                    resourceId: roleId.ToString(),
                    actorUserId: actorUserId != null ? Guid.Parse(actorUserId) : null,
                    tenantId: null,
                    oldValues: $"{{\"permissionId\":\"{permissionId}\",\"permissionName\":\"{permission.Name}\"}}",
                    newValues: null);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully removed permission {PermissionId} from role {RoleId}", permissionId, roleId);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission {PermissionId} from role {RoleId}", permissionId, roleId);
            throw;
        }
    }

    /// <summary>
    /// Assigns a direct permission to a user
    /// </summary>
    public async Task AssignPermissionToUserAsync(Guid tenantId, Guid userId, Guid permissionId, string? actorUserId = null)
    {
        _logger.LogInformation("Assigning permission {PermissionId} to user {UserId} in tenant {TenantId}", permissionId, userId, tenantId);

        try
        {
            // Validate permission exists
            var permission = await _unitOfWork.Permissions.GetByIdAsync(permissionId);
            if (permission == null)
            {
                throw new InvalidOperationException($"Permission {permissionId} not found.");
            }

            // Get TenantUser relationship
            var tenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(tenantId, userId);
            if (tenantUser == null)
            {
                throw new InvalidOperationException($"User {userId} is not a member of tenant {tenantId}.");
            }

            // Check if user already has this permission
            var hasPermission = await _unitOfWork.UserPermissions.HasPermissionAsync(tenantUser.Id, permissionId);
            if (hasPermission)
            {
                throw new InvalidOperationException($"User already has the permission '{permission.DisplayName}'.");
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var userPermission = new UserPermission
                {
                    TenantUserId = tenantUser.Id,
                    PermissionId = permissionId,
                    GrantedByUserId = actorUserId != null ? Guid.Parse(actorUserId) : null,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.UserPermissions.AddAsync(userPermission);
                await _unitOfWork.SaveChangesAsync();

                // Audit log
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "permission.assigned.user",
                    resourceType: "TenantUser",
                    resourceId: tenantUser.Id.ToString(),
                    actorUserId: actorUserId != null ? Guid.Parse(actorUserId) : null,
                    tenantId: tenantId,
                    oldValues: null,
                    newValues: $"{{\"permissionId\":\"{permissionId}\",\"permissionName\":\"{permission.Name}\"}}");

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully assigned permission {PermissionId} to user {UserId} in tenant {TenantId}", permissionId, userId, tenantId);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning permission {PermissionId} to user {UserId} in tenant {TenantId}", permissionId, userId, tenantId);
            throw;
        }
    }

    /// <summary>
    /// Removes a direct permission from a user
    /// </summary>
    public async Task RemovePermissionFromUserAsync(Guid tenantId, Guid userId, Guid permissionId, string? actorUserId = null)
    {
        _logger.LogInformation("Removing permission {PermissionId} from user {UserId} in tenant {TenantId}", permissionId, userId, tenantId);

        try
        {
            // Validate permission exists
            var permission = await _unitOfWork.Permissions.GetByIdAsync(permissionId);
            if (permission == null)
            {
                throw new InvalidOperationException($"Permission {permissionId} not found.");
            }

            // Get TenantUser relationship
            var tenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(tenantId, userId);
            if (tenantUser == null)
            {
                throw new InvalidOperationException($"User {userId} is not a member of tenant {tenantId}.");
            }

            // Find the permission assignment
            var userPermission = await _unitOfWork.UserPermissions.GetByTenantUserIdAndPermissionIdAsync(tenantUser.Id, permissionId);
            if (userPermission == null)
            {
                throw new InvalidOperationException($"User does not have the permission '{permission.DisplayName}'.");
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                await _unitOfWork.UserPermissions.DeleteAsync(userPermission.Id);
                await _unitOfWork.SaveChangesAsync();

                // Audit log
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "permission.removed.user",
                    resourceType: "TenantUser",
                    resourceId: tenantUser.Id.ToString(),
                    actorUserId: actorUserId != null ? Guid.Parse(actorUserId) : null,
                    tenantId: tenantId,
                    oldValues: $"{{\"permissionId\":\"{permissionId}\",\"permissionName\":\"{permission.Name}\"}}",
                    newValues: null);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully removed permission {PermissionId} from user {UserId} in tenant {TenantId}", permissionId, userId, tenantId);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission {PermissionId} from user {UserId} in tenant {TenantId}", permissionId, userId, tenantId);
            throw;
        }
    }
}