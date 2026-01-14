using IdentityTenantManagementDatabase.DbContexts;
using Microsoft.EntityFrameworkCore.Storage;

namespace IdentityTenantManagementDatabase.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly IdentityTenantManagementContext _context;
    private IDbContextTransaction? _transaction;
    private IUserRepository? _userRepository;
    private ITenantRepository? _tenantRepository;
    private IExternalIdentityRepository? _externalIdentityRepository;
    private IIdentityProviderRepository? _identityProviderRepository;
    private ITenantUserRepository? _tenantUserRepository;
    private ITenantUserRoleRepository? _tenantUserRoleRepository;
    private IRoleRepository? _roleRepository;
    private IRolePermissionRepository? _rolePermissionRepository;
    private IUserPermissionRepository? _userPermissionRepository;
    private IPermissionRepository? _permissionRepository;
    private IUserStatusTypeRepository? _userStatusTypeRepository;
    private IRegistrationFailureLogRepository? _registrationFailureLogRepository;
    private IUserProfileRepository? _userProfileRepository;
    private ITenantUserProfileRepository? _tenantUserProfileRepository;
    private IAuditLogRepository? _auditLogRepository;
    private IGlobalSettingsRepository? _globalSettingsRepository;

    public UnitOfWork(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public IUserRepository Users => _userRepository ??= new UserRepository(_context);

    public ITenantRepository Tenants => _tenantRepository ??= new TenantRepository(_context);

    public IExternalIdentityRepository ExternalIdentities => _externalIdentityRepository ??= new ExternalIdentityRepository(_context);

    public IIdentityProviderRepository IdentityProviders => _identityProviderRepository ??= new IdentityProviderRepository(_context);

    public ITenantUserRepository TenantUsers => _tenantUserRepository ??= new TenantUserRepository(_context);

    public ITenantUserRoleRepository TenantUserRoles => _tenantUserRoleRepository ??= new TenantUserRoleRepository(_context);

    public IRoleRepository Roles => _roleRepository ??= new RoleRepository(_context);

    public IRolePermissionRepository RolePermissions => _rolePermissionRepository ??= new RolePermissionRepository(_context);

    public IUserPermissionRepository UserPermissions => _userPermissionRepository ??= new UserPermissionRepository(_context);

    public IPermissionRepository Permissions => _permissionRepository ??= new PermissionRepository(_context);

    public IUserStatusTypeRepository UserStatusTypes => _userStatusTypeRepository ??= new UserStatusTypeRepository(_context);

    public IRegistrationFailureLogRepository RegistrationFailureLogs => _registrationFailureLogRepository ??= new RegistrationFailureLogRepository(_context);

    public IUserProfileRepository UserProfiles => _userProfileRepository ??= new UserProfileRepository(_context);

    public ITenantUserProfileRepository TenantUserProfiles => _tenantUserProfileRepository ??= new TenantUserProfileRepository(_context);

    public IAuditLogRepository AuditLogs => _auditLogRepository ??= new AuditLogRepository(_context);

    public IGlobalSettingsRepository GlobalSettings => _globalSettingsRepository ??= new GlobalSettingsRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No active transaction to commit.");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }

        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}