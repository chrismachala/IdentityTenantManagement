using KeycloakAdapter.Helpers;
using KeycloakAdapter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeycloakAdapter.Services;

public interface IKCEventsService
{
    Task<List<RegistrationEventResult>> GetRecentRegistrationEventsAsync();
}

public class KCEventsService : KeycloakServiceBase, IKCEventsService
{
    public KCEventsService(
        IOptions<KeycloakConfig> config,
        IKCRequestHelper requestHelper,
        ILogger<KCEventsService> logger)
        : base(config, requestHelper, logger)
    {
    }

    public async Task<List<RegistrationEventResult>> GetRecentRegistrationEventsAsync()
    {
        Logger.LogInformation("Getting recent registration events from the last 70 minutes");

        // Calculate time range: last hour + 10 minutes
        var now = DateTimeOffset.UtcNow;
        var dateFrom = now.AddMinutes(-70); // 1 hour + 10 minutes ago
        var dateTo = now;

        // Convert to Unix timestamp in milliseconds (Keycloak expects milliseconds)
        var dateFromTimestamp = dateFrom.ToUnixTimeMilliseconds().ToString();
        var dateToTimestamp = dateTo.ToUnixTimeMilliseconds().ToString();

        var query = HttpQueryCreator.BuildQueryForEvents(
            client: "account",
            dateFrom: dateFromTimestamp,
            dateTo: dateToTimestamp,
            type: "REGISTER"
        );

        var endpoint = BuildEndpoint($"events{query}");

        Logger.LogInformation("Querying events endpoint: {Endpoint}", endpoint);

        var events = await GetAsync<List<EventModel>>(endpoint);

        if (events == null || events.Count == 0)
        {
            Logger.LogInformation("No registration events found in the specified time range");
            return new List<RegistrationEventResult>();
        }

        Logger.LogInformation("Found {Count} registration events", events.Count);

        var results = new List<RegistrationEventResult>();

        foreach (var eventModel in events)
        {
            // Extract email and organization ID from event details
            if (eventModel.Details == null)
            {
                Logger.LogWarning("Event {EventType} for user {UserId} has no details", eventModel.Type, eventModel.UserId);
                continue;
            }

            // Try to get email from details
            var email = eventModel.Details.TryGetValue("email", out var emailValue) ? emailValue : null;

            // Try to get first and last names from details
            var firstName = eventModel.Details.TryGetValue("first_name", out var firstNameValue) ? firstNameValue : string.Empty;
            var lastName = eventModel.Details.TryGetValue("last_name", out var lastNameValue) ? lastNameValue : string.Empty;

            // Try to get organization ID from details (Keycloak might store it as 'organization_id' or similar)
            // The actual key depends on how Keycloak stores organization context
            var orgId = eventModel.Details.TryGetValue("redirect_uri", out var redirectUri)
                ? ExtractOrgIdFromRedirectUri(redirectUri)
                : null;

            // If we can't find org ID in redirect_uri, try other common fields
            if (string.IsNullOrEmpty(orgId))
            {
                orgId = eventModel.Details.TryGetValue("organization_id", out var orgIdValue) ? orgIdValue : null;
            }

            if (string.IsNullOrEmpty(orgId))
            {
                orgId = eventModel.Details.TryGetValue("org_id", out var orgValue) ? orgValue : null;
            }

            if (string.IsNullOrEmpty(email))
            {
                Logger.LogWarning("Registration event for user {UserId} missing email", eventModel.UserId);
                continue;
            }

            if (string.IsNullOrEmpty(orgId))
            {
                Logger.LogWarning("Registration event for user {UserId} ({Email}) missing organization ID",
                    eventModel.UserId, email);
                // You might still want to include this, or skip it - depends on your requirements
                // For now, we'll log but still include with empty org ID
            }

            results.Add(new RegistrationEventResult
            {
                UserId = eventModel.UserId,
                OrganizationId = orgId ?? string.Empty,
                Email = email,
                FirstName = firstName,
                LastName = lastName
            });

            Logger.LogInformation("Found registration: UserId={UserId}, Email={Email}, OrgId={OrgId}",
                eventModel.UserId, email, orgId ?? "Unknown");
        }

        Logger.LogInformation("Processed {Count} registration events successfully", results.Count);
        return results;
    }

    private string? ExtractOrgIdFromRedirectUri(string redirectUri)
    {
        // This is a helper method to extract organization ID from redirect URI
        // The implementation depends on your redirect URI format
        // Example: if redirect URI is like "https://domain.com/org/org-123/callback"
        // you would parse it to extract "org-123"

        // For now, returning null - you'll need to implement based on your URI structure
        return null;
    }
}