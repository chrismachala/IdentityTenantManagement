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
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;

    public OnboardingService(
        IKCOrganisationService kcOrganisationService,
        IKCUserService kcUserService,
        IUserRepository userRepository,
        ITenantRepository tenantRepository)
    {
        _kcOrganisationService = kcOrganisationService;
        _kcUserService = kcUserService;
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
    }

    public async Task OnboardOrganisationAsync(TenantUserOnboardingModel model)
    {
        // Create user in Keycloak
        await _kcUserService.CreateUserAsync(model.CreateUserModel);

        // Retrieve the created user
        UserRepresentation userRepresentation = await _kcUserService.GetUserByEmailAsync(model.CreateUserModel.Email);

        // Create organization in Keycloak
        await _kcOrganisationService.CreateOrgAsync(model.CreateTenantModel);

        // Retrieve the created organization
        OrganizationRepresentation organizationRepresentation = await _kcOrganisationService.GetOrganisationByDomain(model.CreateTenantModel.Domain);

        // Link user to organization
        UserTenantModel orgUser = new UserTenantModel
        {
            UserId = userRepresentation.Id,
            TenantId = organizationRepresentation.Id
        };
        await _kcOrganisationService.AddUserToOrganisationAsync(orgUser);

        // Persist user and tenant to database
        await SaveUserAndTenantAsync(userRepresentation, organizationRepresentation);
    }

    private async Task SaveUserAndTenantAsync(UserRepresentation userRepresentation, OrganizationRepresentation organizationRepresentation)
    {
        var user = new User
        {
            GUserId = Guid.Parse(userRepresentation.Id),
            SEmail = userRepresentation.Email,
            SFirstName = userRepresentation.FirstName,
            SLastName = userRepresentation.LastName
        };

        await _userRepository.AddAsync(user);

        var tenant = new Tenant
        {
            GTenantId = Guid.Parse(organizationRepresentation.Id),
            SDomain = organizationRepresentation.Domains.First().Name,
            SName = organizationRepresentation.Name
        };

        await _tenantRepository.AddAsync(tenant);
    }
}