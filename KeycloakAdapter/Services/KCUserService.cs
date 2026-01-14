using KeycloakAdapter.Helpers;
using KeycloakAdapter.Models;
using IO.Swagger.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeycloakAdapter.Services;

public interface IKCUserService
{
    Task CreateUserAsync(CreateUserModel model);
    Task SendCreatePasswordResetEmailAsync(string userId);
    Task<UserRepresentation> GetUserByIdAsync(string userId);
    Task<UserRepresentation> GetUserByEmailAsync(string email);
    Task<UserRepresentation?> TryGetUserByEmailAsync(string email);
    Task<UserRepresentation> GetOrCreateUserAsync(CreateUserModel model);
    Task UpdateUserAsync(string userId, CreateUserModel model);
    Task DeleteUserAsync(string userId);
    Task<List<UserRepresentation>> GetAllUsersAsync();
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

    public async Task<UserRepresentation> GetUserByIdAsync(string userId)
    {
        Logger.LogInformation("Getting user by ID: {UserId}", userId);

        var endpoint = BuildEndpoint($"users/{userId}");
        var user = await GetAsync<UserRepresentation>(endpoint);

        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found in Keycloak");
        }

        Logger.LogInformation("Successfully retrieved user: {UserId}", userId);
        return user;
    }

    public async Task<UserRepresentation> GetUserByEmailAsync(string emailAddress)
    {
        Logger.LogInformation("Getting user by email: {Email}", emailAddress);

        var query = HttpQueryCreator.BuildQueryForUserSearchByEmail(emailAddress);
        var endpoint = BuildEndpoint($"users{query}");

        var users = await GetAsync<List<UserRepresentation>>(endpoint);

        return GetFirstOrThrow(users, "User", $"email={emailAddress}");
    }

    public async Task<UserRepresentation?> TryGetUserByEmailAsync(string emailAddress)
    {
        Logger.LogInformation("Attempting to get user by email: {Email}", emailAddress);

        try
        {
            var query = HttpQueryCreator.BuildQueryForUserSearchByEmail(emailAddress);
            var endpoint = BuildEndpoint($"users{query}");

            var users = await GetAsync<List<UserRepresentation>>(endpoint);

            if (users == null || users.Count == 0)
            {
                Logger.LogInformation("User with email {Email} not found", emailAddress);
                return null;
            }

            Logger.LogInformation("Found user with email: {Email}", emailAddress);
            return users.First();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error while searching for user by email: {Email}", emailAddress);
            return null;
        }
    }

    public async Task<UserRepresentation> GetOrCreateUserAsync(CreateUserModel userModel)
    {
        Logger.LogInformation("Getting or creating user: {Email}", userModel.Email);

        var existingUser = await TryGetUserByEmailAsync(userModel.Email);

        if (existingUser != null)
        {
            Logger.LogInformation("User already exists with email: {Email}, using existing user", userModel.Email);
            return existingUser;
        }

        Logger.LogInformation("User does not exist, creating new user: {Email}", userModel.Email);
        await CreateUserAsync(userModel);

        var createdUser = await GetUserByEmailAsync(userModel.Email);
        Logger.LogInformation("Successfully created and retrieved user: {Email}", userModel.Email);

        return createdUser;
    }
    
    public async Task SendCreatePasswordResetEmailAsync(string userId)
    {
        Logger.LogInformation("Sending password reset email to user: {UserId}", userId);

        var endpoint = BuildEndpoint($"users/{userId}/execute-actions-email");
        var actions = new List<string> { "UPDATE_PASSWORD" };

        await PutJsonAsync(endpoint, actions);
        Logger.LogInformation("Successfully sent password reset email to user: {UserId}", userId);
    }
    

    public async Task CreateUserAsync(CreateUserModel userModel)
    {
        Logger.LogInformation("Creating user: {Email}", userModel.Email);

        var endpoint = BuildEndpoint("users");
        UserRepresentation user;

        if (!string.IsNullOrWhiteSpace(userModel.Password))
        {
             user = new UserRepresentation
            {
                Username = userModel.UserName,
                Email = userModel.Email,
                FirstName = userModel.FirstName,
                LastName = userModel.LastName,
                Enabled = true,
                EmailVerified = false,
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
        }
        else
        {   
             user = new UserRepresentation
            {
                Username = userModel.UserName,
                Email = userModel.Email,
                FirstName = userModel.FirstName,
                LastName = userModel.LastName,
                Enabled = true,
                EmailVerified = false, 
                RequiredActions = new List<string>()
            };
            
        }
        
        await PostJsonAsync(endpoint, user);
        Logger.LogInformation("Successfully created user: {Email}", userModel.Email);
    }

    public async Task UpdateUserAsync(string userId, CreateUserModel userModel)
    {
        Logger.LogInformation("Updating user: {UserId}", userId);

        var endpoint = BuildEndpoint($"users/{userId}");

        var user = new UserRepresentation
        {
            FirstName = userModel.FirstName,
            LastName = userModel.LastName,
            Email = userModel.Email,
            EmailVerified = true
        };

        await PutJsonAsync(endpoint, user);
        Logger.LogInformation("Successfully updated user: {UserId}", userId);
    }

    public async Task DeleteUserAsync(string userId)
    {
        Logger.LogWarning("Deleting user: {UserId}", userId);

        var endpoint = BuildEndpoint($"users/{userId}");
        await DeleteAsync(endpoint);

        Logger.LogInformation("Successfully deleted user: {UserId}", userId);
    }

    public async Task<List<UserRepresentation>> GetAllUsersAsync()
    {
        Logger.LogInformation("Getting all users");

        var endpoint = BuildEndpoint("users");
        var users = await GetAsync<List<UserRepresentation>>(endpoint);

        Logger.LogInformation("Found {Count} users", users?.Count ?? 0);
        return users ?? new List<UserRepresentation>();
    }
}