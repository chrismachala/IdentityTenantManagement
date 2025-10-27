using Microsoft.EntityFrameworkCore.Storage;

namespace IdentityTenantManagementDatabase.Repositories;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    ITenantRepository Tenants { get; }
    IExternalIdentityRepository ExternalIdentities { get; }
    IIdentityProviderRepository IdentityProviders { get; }
    ITenantUserRepository TenantUsers { get; }
    IRoleRepository Roles { get; }
    IPermissionRepository Permissions { get; }
    IUserStatusTypeRepository UserStatusTypes { get; }
    IRegistrationFailureLogRepository RegistrationFailureLogs { get; }

    /// <summary>
    /// Saves all changes made in this unit of work to the database.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a database transaction.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}