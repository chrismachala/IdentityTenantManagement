using KeycloakAdapter.Services;

namespace IdentityTenantManagement.Services;

public class RegistrationProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RegistrationProcessorService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public RegistrationProcessorService(
        IServiceProvider serviceProvider,
        ILogger<RegistrationProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Registration Processor Service started");

        // Wait a bit before starting the first execution (optional - avoids immediate execution on startup)
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRegistrationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing registrations");
            }

            // Wait for the next interval
            _logger.LogInformation("Next registration processing scheduled in {Interval}", _interval);
            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Registration Processor Service stopped");
    }

    private async Task ProcessRegistrationsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting registration processing at {Time}", DateTime.UtcNow);

        // Create a scope to resolve scoped services
        using var scope = _serviceProvider.CreateScope();

        var eventsService = scope.ServiceProvider.GetRequiredService<IKCEventsService>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        try
        {
            // Get recent registration events from Keycloak
            var registrations = await eventsService.GetRecentRegistrationEventsAsync();

            if (registrations == null || registrations.Count == 0)
            {
                _logger.LogInformation("No new registrations found in the last 70 minutes");
                return;
            }

            _logger.LogInformation("Found {Count} new registration(s) to process", registrations.Count);

            // Process each registration
            var successCount = 0;
            var failureCount = 0;

            foreach (var registration in registrations)
            {
                try
                {
                    if (string.IsNullOrEmpty(registration.Email))
                    {
                        _logger.LogWarning("Skipping registration with missing email");
                        failureCount++;
                        continue;
                    }

                    if (string.IsNullOrEmpty(registration.OrganizationId))
                    {
                        _logger.LogWarning("Skipping registration for {Email} - missing organization ID", registration.Email);
                        failureCount++;
                        continue;
                    }

                    if (string.IsNullOrEmpty(registration.UserId))
                    {
                        _logger.LogWarning("Skipping registration for {Email} - missing user ID", registration.Email);
                        failureCount++;
                        continue;
                    }

                    _logger.LogInformation("Processing registration: UserId={UserId}, Email={Email}, OrgId={OrgId}",
                        registration.UserId, registration.Email, registration.OrganizationId);

                    // Add the user to the database
                    await userService.AddInvitedUserToDatabaseAsync(
                        keycloakUserId: registration.UserId,
                        keycloakTenantId: registration.OrganizationId,
                        email: registration.Email,
                        firstName: registration.FirstName ?? string.Empty,
                        lastName: registration.LastName ?? string.Empty
                    );

                    successCount++;
                    _logger.LogInformation("Successfully processed registration for {Email} (UserId: {UserId})",
                        registration.Email, registration.UserId);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, "Failed to process registration for {Email}", registration.Email);
                }
            }

            _logger.LogInformation(
                "Registration processing completed: {SuccessCount} succeeded, {FailureCount} failed",
                successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration processing");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registration Processor Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}