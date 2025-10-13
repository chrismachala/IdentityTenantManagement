using IdentityTenantManagement.EFCore;
using IdentityTenantManagement.Models.Onboarding;
using IdentityTenantManagement.Models.Organisations;
using IdentityTenantManagement.Repositories;
using IdentityTenantManagement.Services.KeycloakServices;
using IO.Swagger.Model;

namespace IdentityTenantManagement.Services;

public interface IOnboardingService
{
    Task OnboardOrganisationAsync(TenantUserOnboardingModel model);
}

public class OnboardingService : IOnboardingService
{
    private readonly IKCOrganisationService _kcOrganisationService;
    private readonly IKCUserService _kcUserService;
    private readonly IUnitOfWork _unitOfWork;

    public OnboardingService(
        IKCOrganisationService kcOrganisationService,
        IKCUserService kcUserService,
        IUnitOfWork unitOfWork)
    {
        _kcOrganisationService = kcOrganisationService;
        _kcUserService = kcUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task OnboardOrganisationAsync(TenantUserOnboardingModel model)
    { 
        await _unitOfWork.BeginTransactionAsync();

        try
        { 
            await _kcUserService.CreateUserAsync(model.CreateUserModel);
 
            UserRepresentation userRepresentation = await _kcUserService.GetUserByEmailAsync(model.CreateUserModel.Email);
 
            await _kcOrganisationService.CreateOrgAsync(model.CreateTenantModel);
 
            OrganizationRepresentation organizationRepresentation = await _kcOrganisationService.GetOrganisationByDomain(model.CreateTenantModel.Domain);
 
            UserTenantModel orgUser = new UserTenantModel
            {
                UserId = userRepresentation.Id,
                TenantId = organizationRepresentation.Id
            };
            await _kcOrganisationService.AddUserToOrganisationAsync(orgUser);
 
            await AddUserAndTenantAsync(userRepresentation, organizationRepresentation);
 
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            // Rollback transaction on error
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    private async Task AddUserAndTenantAsync(UserRepresentation userRepresentation, OrganizationRepresentation organizationRepresentation)
    {
        var user = new User
        {
            GUserId = Guid.Parse(userRepresentation.Id),
            SEmail = userRepresentation.Email,
            SFirstName = userRepresentation.FirstName,
            SLastName = userRepresentation.LastName
        };

        await _unitOfWork.Users.AddAsync(user);

        var tenant = new Tenant
        {
            GTenantId = Guid.Parse(organizationRepresentation.Id),
            SDomain = organizationRepresentation.Domains.First().Name,
            SName = organizationRepresentation.Name
        };

        await _unitOfWork.Tenants.AddAsync(tenant);
    }
}