using IdentityTenantManagement.EFCore;
using IdentityTenantManagement.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagement.Repositories;

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
            .FirstOrDefaultAsync(u => u.SEmail == email);
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
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User entity)
    {
        _context.Users.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await GetByIdAsync(id);
        if (user == null)
        {
            throw new NotFoundException(nameof(User), id.ToString());
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Users.AnyAsync(u => u.GUserId == id);
    }
}