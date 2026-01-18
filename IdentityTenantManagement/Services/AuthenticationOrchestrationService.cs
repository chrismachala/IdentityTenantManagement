using IdentityTenantManagementDatabase.Repositories;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;

namespace IdentityTenantManagement.Services;

public interface IAuthenticationOrchestrationService
{
    Task<AuthenticationResponse> AuthenticateAsync(LoginModel loginModel);
    Task<AuthenticationResponse> SelectOrganizationAsync(OrganizationSelectionModel selectionModel);
    Task<List<OrganizationInfo>> GetOrganizationsAsync(string accessToken);
}

public class AuthenticationOrchestrationService : IAuthenticationOrchestrationService
{
    private readonly IKCAuthenticationService _kcAuthenticationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthenticationOrchestrationService> _logger;

    public AuthenticationOrchestrationService(
        IKCAuthenticationService kcAuthenticationService,
        IUnitOfWork unitOfWork,
        ILogger<AuthenticationOrchestrationService> logger)
    {
        _kcAuthenticationService = kcAuthenticationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and verifies they are not inactive in any tenant they're trying to access.
    /// Implements FR-004: Inactive users must be denied authentication.
    /// </summary>
    public async Task<AuthenticationResponse> AuthenticateAsync(LoginModel loginModel)
    {
        // First, authenticate with Keycloak
        var authResponse = await _kcAuthenticationService.AuthenticateAsync(loginModel);

        if (!authResponse.Success || authResponse.UserInfo == null)
        {
            return authResponse;
        }

        if (authResponse.RequiresOrganizationSelection)
        {
            return authResponse;
        }

        return await ValidateUserStatusAsync(authResponse, authResponse.UserInfo.OrganizationId);
    }

    public async Task<AuthenticationResponse> SelectOrganizationAsync(OrganizationSelectionModel selectionModel)
    {
        try
        {
            var userInfo = await _kcAuthenticationService.GetUserInfoForOrganizationAsync(
                selectionModel.AccessToken,
                selectionModel.OrganizationId);

            if (userInfo == null)
            {
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "User does not have access to the selected organization"
                };
            }

            var authResponse = new AuthenticationResponse
            {
                Success = true,
                AccessToken = selectionModel.AccessToken,
                ExpiresIn = selectionModel.ExpiresIn,
                UserInfo = userInfo
            };

            return await ValidateUserStatusAsync(authResponse, selectionModel.OrganizationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user status during authentication");
            // On error, allow authentication to proceed (fail open for availability)
            return new AuthenticationResponse
            {
                Success = false,
                ErrorMessage = "An error occurred during authentication"
            };
        }
    }

    public async Task<List<OrganizationInfo>> GetOrganizationsAsync(string accessToken)
    {
        return await _kcAuthenticationService.GetOrganizationsForAccessTokenAsync(accessToken);
    }

    private async Task<AuthenticationResponse> ValidateUserStatusAsync(AuthenticationResponse authResponse, string organizationId)
    {
        try
        {
            var keycloakProviderId = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719");

            // Resolve Keycloak user ID to internal user ID
            var userExternalIdentity = await _unitOfWork.ExternalIdentities
                .GetByExternalIdentifierAsync(authResponse.UserInfo!.UserId, keycloakProviderId);

            if (userExternalIdentity == null)
            {
                _logger.LogWarning("User {UserId} authenticated with Keycloak but not found in database",
                    authResponse.UserInfo.UserId);
                return authResponse; // Allow authentication - user may be newly created
            }

            // Resolve Keycloak organization ID to internal tenant ID
            var tenantExternalIdentity = await _unitOfWork.ExternalIdentities
                .GetByExternalIdentifierAsync(organizationId, keycloakProviderId);

            if (tenantExternalIdentity == null)
            {
                _logger.LogWarning("Organization {OrgId} not found in database for user {UserId}",
                    organizationId, authResponse.UserInfo.UserId);
                return authResponse; // Allow authentication - org may be newly created
            }

            var internalUserId = userExternalIdentity.EntityId;
            var internalTenantId = tenantExternalIdentity.EntityId;

            // Check if user has an active profile in this tenant
            var tenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(internalTenantId, internalUserId);

            if (tenantUser == null)
            {
                _logger.LogWarning("User {UserId} is not a member of tenant {TenantId}",
                    internalUserId, internalTenantId);

                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "User is not a member of this organization"
                };
            }

            // Get the user's profile for this tenant
            var tenantUserProfile = await _unitOfWork.TenantUserProfiles.GetByTenantUserIdAsync(tenantUser.Id);

            if (tenantUserProfile == null)
            {
                _logger.LogWarning("No profile found for user {UserId} in tenant {TenantId}",
                    internalUserId, internalTenantId);
                return authResponse; // Allow if no profile yet - may be in setup
            }

            // Get the UserProfile to check status
            var userProfile = await _unitOfWork.UserProfiles.GetByIdAsync(tenantUserProfile.UserProfileId);

            if (userProfile == null)
            {
                _logger.LogWarning("UserProfile {ProfileId} not found", tenantUserProfile.UserProfileId);
                return authResponse;
            }

            // Get inactive status ID
            var inactiveStatus = await _unitOfWork.UserStatusTypes.GetByNameAsync("inactive");

            if (inactiveStatus != null && userProfile.StatusId == inactiveStatus.Id)
            {
                _logger.LogWarning("Authentication denied for inactive user {UserId} in tenant {TenantId}",
                    internalUserId, internalTenantId);

                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Your account has been deactivated. Please contact your administrator."
                };
            }

            _logger.LogInformation("User {UserId} authenticated successfully and is active in tenant {TenantId}",
                internalUserId, internalTenantId);

            // Return response with internal IDs instead of external Keycloak IDs
            return new AuthenticationResponse
            {
                Success = true,
                AccessToken = authResponse.AccessToken,
                RefreshToken = authResponse.RefreshToken,
                ExpiresIn = authResponse.ExpiresIn,
                UserInfo = new UserInfo
                {
                    UserId = internalUserId.ToString(),
                    Username = authResponse.UserInfo.Username,
                    Email = authResponse.UserInfo.Email,
                    FirstName = authResponse.UserInfo.FirstName,
                    LastName = authResponse.UserInfo.LastName,
                    Organization = authResponse.UserInfo.Organization,
                    OrganizationId = internalTenantId.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user status during authentication");
            return authResponse;
        }
    }
}
