using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class UserStatusTypeRepository : IUserStatusTypeRepository
{
    private readonly IdentityTenantManagementContext _context;

    public UserStatusTypeRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<UserStatusType?> GetByIdAsync(Guid id)
    {
        return await _context.UserStatusTypes.FindAsync(id);
    }

    public async Task<IEnumerable<UserStatusType>> GetAllAsync()
    {
        return await _context.UserStatusTypes.ToListAsync();
    }

    public async Task AddAsync(UserStatusType entity)
    {
        await _context.UserStatusTypes.AddAsync(entity);
    }

    public async Task UpdateAsync(UserStatusType entity)
    {
        _context.UserStatusTypes.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var userStatusType = await GetByIdAsync(id);
        if (userStatusType != null)
        {
            _context.UserStatusTypes.Remove(userStatusType);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.UserStatusTypes.AnyAsync(ust => ust.Id == id);
    }

    public async Task<UserStatusType?> GetByNameAsync(string name)
    {
        return await _context.UserStatusTypes
            .FirstOrDefaultAsync(ust => ust.Name == name);
    }
}