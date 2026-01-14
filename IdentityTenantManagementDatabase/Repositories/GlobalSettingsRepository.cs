namespace IdentityTenantManagementDatabase.Repositories;

using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

public class GlobalSettingsRepository : IGlobalSettingsRepository
{
    private readonly IdentityTenantManagementContext _context;

    public GlobalSettingsRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<GlobalSettings?> GetByIdAsync(Guid id)
    {
        return await _context.GlobalSettings.FindAsync(id);
    }

    public async Task<GlobalSettings?> GetByKeyAsync(string key)
    {
        return await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
    }

    public async Task<IEnumerable<GlobalSettings>> GetAllAsync()
    {
        return await _context.GlobalSettings.OrderBy(s => s.Key).ToListAsync();
    }

    public async Task<IEnumerable<GlobalSettings>> GetAllSettingsAsync()
    {
        return await GetAllAsync();
    }

    public async Task AddAsync(GlobalSettings entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        await _context.GlobalSettings.AddAsync(entity);
    }

    public async Task UpdateAsync(GlobalSettings entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _context.GlobalSettings.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.GlobalSettings.Remove(entity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.GlobalSettings.AnyAsync(s => s.Id == id);
    }

    public async Task UpsertAsync(string key, string value, string? description = null)
    {
        var existing = await GetByKeyAsync(key);
        if (existing != null)
        {
            existing.Value = value;
            if (description != null)
            {
                existing.Description = description;
            }
            existing.UpdatedAt = DateTime.UtcNow;
            _context.GlobalSettings.Update(existing);
        }
        else
        {
            var newSetting = new GlobalSettings
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };
            await _context.GlobalSettings.AddAsync(newSetting);
        }
    }
}