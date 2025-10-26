using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class RegistrationFailureLogRepository : IRegistrationFailureLogRepository
{
    private readonly IdentityTenantManagementContext _context;

    public RegistrationFailureLogRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<RegistrationFailureLog?> GetByIdAsync(Guid id)
    {
        return await _context.RegistrationFailureLogs.FindAsync(id);
    }

    public async Task<IEnumerable<RegistrationFailureLog>> GetAllAsync()
    {
        return await _context.RegistrationFailureLogs.ToListAsync();
    }

    public async Task<IEnumerable<RegistrationFailureLog>> GetByKeycloakUserIdAsync(string keycloakUserId)
    {
        return await _context.RegistrationFailureLogs
            .Where(r => r.KeycloakUserId == keycloakUserId)
            .ToListAsync();
    }

    public async Task<IEnumerable<RegistrationFailureLog>> GetByKeycloakTenantIdAsync(string keycloakTenantId)
    {
        return await _context.RegistrationFailureLogs
            .Where(r => r.KeycloakTenantId == keycloakTenantId)
            .ToListAsync();
    }

    public async Task AddAsync(RegistrationFailureLog entity)
    {
        await _context.RegistrationFailureLogs.AddAsync(entity);
    }

    public async Task UpdateAsync(RegistrationFailureLog entity)
    {
        _context.RegistrationFailureLogs.Update(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var log = await GetByIdAsync(id);
        if (log == null)
        {
            throw new KeyNotFoundException($"RegistrationFailureLog with ID {id} was not found.");
        }

        _context.RegistrationFailureLogs.Remove(log);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.RegistrationFailureLogs.AnyAsync(r => r.Id == id);
    }
}