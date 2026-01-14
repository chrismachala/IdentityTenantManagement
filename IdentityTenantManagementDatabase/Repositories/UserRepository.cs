using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityTenantManagementContext _context;

    public UserRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<IEnumerable<User>> GetByTenantIdAsync(Guid tenantId)
    {
        // This would require a junction table for User-Tenant relationships
        // For now, returning empty collection as the relationship isn't modeled yet
        return await Task.FromResult(new List<User>());
    }

    public async Task AddAsync(User entity)
    {
        await _context.Users.AddAsync(entity);
    }

    public async Task UpdateAsync(User entity)
    {
        _context.Users.Update(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await GetByIdAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {id} was not found.");
        }

        _context.Users.Remove(user);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Users.AnyAsync(u => u.Id == id);
    }

    // Soft-delete methods
    public async Task<IEnumerable<Guid>> GetGloballyInactiveUserIdsAsync()
    {
        var inactiveStatusId = Guid.Parse("7F313E08-F83E-43DF-B550-C11014592AB7");

        // Find users where ALL their UserProfiles (via TenantUserProfile) are inactive
        var usersWithAllInactiveProfiles = await _context.Users
            .Where(u => _context.TenantUserProfiles
                .Any(tup => tup.TenantUser.UserId == u.Id)) // User has at least one profile
            .Where(u => !_context.TenantUserProfiles
                .Any(tup => tup.TenantUser.UserId == u.Id &&
                           tup.UserProfile.StatusId != inactiveStatusId)) // NO active profiles exist
            .Where(u => u.GloballyInactiveAt == null) // Not already marked
            .Select(u => u.Id)
            .ToListAsync();

        return usersWithAllInactiveProfiles;
    }

    public async Task MarkAsGloballyInactiveAsync(Guid userId)
    {
        var user = await GetByIdAsync(userId);
        if (user != null)
        {
            user.GloballyInactiveAt = DateTime.UtcNow;
            await UpdateAsync(user);
        }
    }

    public async Task ClearGloballyInactiveStatusAsync(Guid userId)
    {
        var user = await GetByIdAsync(userId);
        if (user != null)
        {
            user.GloballyInactiveAt = null;
            await UpdateAsync(user);
        }
    }

    // Permanent deletion methods
    public async Task<IEnumerable<User>> GetDeletionFailedUsersAsync()
    {
        return await _context.Users
            .Where(u => u.DeletionFailedAt != null)
            .ToListAsync();
    }

    public async Task MarkDeletionFailedAsync(Guid userId, string reason, int retryCount)
    {
        var user = await GetByIdAsync(userId);
        if (user != null)
        {
            user.DeletionFailedAt = DateTime.UtcNow;
            user.DeletionFailedReason = reason;
            user.DeletionRetryCount = retryCount;
            await UpdateAsync(user);
        }
    }

    public async Task ResetDeletionRetryAsync(Guid userId)
    {
        var user = await GetByIdAsync(userId);
        if (user != null)
        {
            user.DeletionFailedAt = null;
            user.DeletionFailedReason = null;
            user.DeletionRetryCount = 0;
            await UpdateAsync(user);
        }
    }
}