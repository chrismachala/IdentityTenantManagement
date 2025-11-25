using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly IdentityTenantManagementContext _context;

    public UserProfileRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<UserProfile?> GetByIdAsync(Guid id)
    {
        return await _context.UserProfiles.FindAsync(id);
    }

    public async Task<IEnumerable<UserProfile>> GetAllAsync()
    {
        return await _context.UserProfiles.ToListAsync();
    }

    public async Task AddAsync(UserProfile entity)
    {
        await _context.UserProfiles.AddAsync(entity);
    }

    public async Task UpdateAsync(UserProfile entity)
    {
        _context.UserProfiles.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.UserProfiles.Remove(entity);
        }
    }

    public Task<bool> ExistsAsync(Guid id)
    {
        _context.UserProfiles.Any(u=>u.Id == id);
        return Task.FromResult(true);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}