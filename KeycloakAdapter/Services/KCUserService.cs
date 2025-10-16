using KeycloakAdapter.Helpers;
using KeycloakAdapter.Models;
using IO.Swagger.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeycloakAdapter.Services;

public interface IKCUserService
{
    Task CreateUserAsync(CreateUserModel model);
    Task<UserRepresentation> GetUserByEmailAsync(string email);
    Task DeleteUserAsync(string userId);
}

public class KCUserService : KeycloakServiceBase, IKCUserService
{
    public KCUserService(
        IOptions<KeycloakConfig> config,
        IKCRequestHelper requestHelper,
        ILogger<KCUserService> logger)
        : base(config, requestHelper, logger)
    {
    }

    public async Task<UserRepresentation> GetUserByEmailAsync(string emailAddress)
    {
        Logger.LogInformation("Getting user by email: {Email}", emailAddress);

        var query = HttpQueryCreator.BuildQueryForUserSearchByEmail(emailAddress);
        var endpoint = BuildEndpoint($"users{query}");

        var users = await GetAsync<List<UserRepresentation>>(endpoint);

        return GetFirstOrThrow(users, "User", $"email={emailAddress}");
    }

    public async Task CreateUserAsync(CreateUserModel userModel)
    {
        Logger.LogInformation("Creating user: {Email}", userModel.Email);

        var endpoint = BuildEndpoint("users");

        var user = new UserRepresentation
        {
            Username = userModel.UserName,
            Email = userModel.Email,
            FirstName = userModel.FirstName,
            LastName = userModel.LastName,
            Enabled = true,
            EmailVerified = true,
            Credentials = new List<CredentialRepresentation>
            {
                new CredentialRepresentation
                {
                    Type = "password",
                    Value = userModel.Password,
                    Temporary = false
                }
            },
            RequiredActions = new List<string>()
        };

        await PostJsonAsync(endpoint, user);
        Logger.LogInformation("Successfully created user: {Email}", userModel.Email);
    }

    public async Task DeleteUserAsync(string userId)
    {
        Logger.LogWarning("Deleting user: {UserId}", userId);

        var endpoint = BuildEndpoint($"users/{userId}");
        await DeleteAsync(endpoint);

        Logger.LogInformation("Successfully deleted user: {UserId}", userId);
    }
}