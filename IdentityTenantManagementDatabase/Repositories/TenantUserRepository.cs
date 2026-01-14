using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class TenantUserRepository : ITenantUserRepository
{
    private readonly IdentityTenantManagementContext _context;

    public TenantUserRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<TenantUser?> GetByIdAsync(Guid id)
    {
        return await _context.TenantUsers
            .Include(tu => tu.Tenant)
            .Include(tu => tu.User)
            .Include(tu => tu.TenantUserRoles)
                .ThenInclude(tur => tur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(tu => tu.Id == id);
    }

    public async Task<IEnumerable<TenantUser>> GetAllAsync()
    {
        return await _context.TenantUsers
            .Include(tu => tu.Tenant)
            .Include(tu => tu.User)
            .Include(tu => tu.TenantUserRoles)
                .ThenInclude(tur => tur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .ToListAsync();
    }

    public async Task AddAsync(TenantUser entity)
    {
        await _context.TenantUsers.AddAsync(entity);
    }

    public async Task UpdateAsync(TenantUser entity)
    {
        _context.TenantUsers.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.TenantUsers.Remove(entity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.TenantUsers.AnyAsync(tu => tu.Id == id);
    }

    public async Task<IEnumerable<TenantUser>> GetByTenantIdAsync(Guid tenantId)
    {
        return await _context.TenantUsers
            .Include(tu => tu.User)
            .Include(tu => tu.TenantUserRoles)
                .ThenInclude(tur => tur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Where(tu => tu.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenantUser>> GetByUserIdAsync(Guid userId)
    {
        return await _context.TenantUsers
            .Include(tu => tu.Tenant)
            .Include(tu => tu.TenantUserRoles)
                .ThenInclude(tur => tur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Where(tu => tu.UserId == userId)
            .ToListAsync();
    }

    public async Task<TenantUser?> GetByTenantAndUserIdAsync(Guid tenantId, Guid userId)
    {
        return await _context.TenantUsers
            .Include(tu => tu.Tenant)
            .Include(tu => tu.User)
            .Include(tu => tu.TenantUserRoles)
                .ThenInclude(tur => tur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId);
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid tenantId, Guid userId)
    {
        var tenantUser = await _context.TenantUsers
            .Include(tu => tu.TenantUserRoles)
                .ThenInclude(tur => tur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId);

        if (tenantUser == null)
            return new List<string>();

        // Get permissions from all roles
        var rolePermissions = tenantUser.TenantUserRoles
            .SelectMany(tur => tur.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .ToList();

        // Get user-specific permissions
        var userPermissions = await _context.UserPermissions
            .Where(up => up.TenantUserId == tenantUser.Id)
            .Include(up => up.Permission)
            .Select(up => up.Permission.Name)
            .ToListAsync();

        // Combine and return distinct permissions
        return rolePermissions.Union(userPermissions).Distinct().ToList();
    }

    public async Task<int> CountUsersWithRoleInTenantAsync(Guid tenantId, Guid roleId)
    {
        return await _context.TenantUsers
            .Where(tu => tu.TenantId == tenantId && tu.TenantUserRoles.Any(tur => tur.RoleId == roleId))
            .CountAsync();
    }

    public async Task<(IEnumerable<TenantUser> Users, int TotalCount)> GetUsersAsync(
        Guid tenantId,
        bool includeInactive = false,
        int page = 1,
        int pageSize = 50)
    {
        // Get the inactive status ID for filtering
        var inactiveStatus = await _context.UserStatusTypes
            .FirstOrDefaultAsync(ust => ust.Name == "inactive");

        var inactiveStatusId = inactiveStatus?.Id;

        // Build the base query
        var query = _context.TenantUsers
            .Include(tu => tu.User)
            .Include(tu => tu.TenantUserRoles)
                .ThenInclude(tur => tur.Role)
            .Where(tu => tu.TenantId == tenantId);

        // Join with TenantUserProfile and UserProfile to filter by status
        var usersQuery = query
            .Join(
                _context.TenantUserProfiles,
                tu => tu.Id,
                tup => tup.TenantUserId,
                (tu, tup) => new { TenantUser = tu, TenantUserProfile = tup })
            .Join(
                _context.UserProfiles,
                x => x.TenantUserProfile.UserProfileId,
                up => up.Id,
                (x, up) => new { x.TenantUser, x.TenantUserProfile, UserProfile = up });

        // Apply inactive filter if requested
        if (!includeInactive && inactiveStatusId.HasValue)
        {
            usersQuery = usersQuery.Where(x => x.UserProfile.StatusId != inactiveStatusId.Value);
        }

        // Get total count before pagination
        var totalCount = await usersQuery.CountAsync();

        // Apply pagination
        var skip = (page - 1) * pageSize;
        var users = await usersQuery
            .OrderBy(x => x.UserProfile.FirstName)
            .ThenBy(x => x.UserProfile.LastName)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => x.TenantUser)
            .ToListAsync();

        return (users, totalCount);
    }
}