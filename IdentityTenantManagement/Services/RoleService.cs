using IdentityTenantManagementDatabase.Models;
using IdentityTenantManagementDatabase.Repositories;

namespace IdentityTenantManagement.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RoleService(IRoleRepository roleRepository, IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        return await _roleRepository.GetAllWithPermissionsAsync();
    }

    public async Task<Role?> GetRoleByIdAsync(Guid id)
    {
        return await _roleRepository.GetByIdWithPermissionsAsync(id);
    }

    public async Task<Role?> GetRoleByNameAsync(string name)
    {
        return await _roleRepository.GetByNameAsync(name);
    }

    public async Task<Guid> GetDefaultUserRoleIdAsync()
    {
        var role = await _roleRepository.GetByNameAsync("org-user");
        return role?.Id ?? throw new InvalidOperationException("Default org-user role not found");
    }
}