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

    public UnitOfWork(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public IUserRepository Users => _userRepository ??= new UserRepository(_context);

    public ITenantRepository Tenants => _tenantRepository ??= new TenantRepository(_context);

    public IExternalIdentityRepository ExternalIdentities => _externalIdentityRepository ??= new ExternalIdentityRepository(_context);

    public IIdentityProviderRepository IdentityProviders => _identityProviderRepository ??= new IdentityProviderRepository(_context);

    public ITenantUserRepository TenantUsers => _tenantUserRepository ??= new TenantUserRepository(_context);

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