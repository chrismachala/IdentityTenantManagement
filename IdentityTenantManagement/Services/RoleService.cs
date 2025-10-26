using IdentityTenantManagementDatabase.Models;
using IdentityTenantManagementDatabase.Repositories;

namespace IdentityTenantManagement.Services;

public class RoleService : IRoleService
{
    private readonly IUnitOfWork _unitOfWork;

    public RoleService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        return await _unitOfWork.Roles.GetAllWithPermissionsAsync();
    }

    public async Task<Role?> GetRoleByIdAsync(Guid id)
    {
        return await _unitOfWork.Roles.GetByIdWithPermissionsAsync(id);
    }

    public async Task<Role?> GetRoleByNameAsync(string name)
    {
        return await _unitOfWork.Roles.GetByNameAsync(name);
    }

    public async Task<Guid> GetDefaultUserRoleIdAsync()
    {
        var role = await _unitOfWork.Roles.GetByNameAsync("org-user");
        return role?.Id ?? throw new InvalidOperationException("Default org-user role not found");
    }
}