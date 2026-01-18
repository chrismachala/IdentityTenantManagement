using Asp.Versioning;
using IdentityTenantManagement.Authorization;
using IdentityTenantManagement.Models.Settings;
using IdentityTenantManagementDatabase.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IdentityTenantManagement.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenants/{tenantId}/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(IUnitOfWork unitOfWork, ILogger<SettingsController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Gets the actor user ID from X-User-Id header (preferred) or falls back to JWT claims
    /// </summary>
    private string? GetActorUserId()
    {
        // Check X-User-Id header first (internal user ID sent by Blazor app)
        var actorUserId = Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(actorUserId))
        {
            _logger.LogInformation("GetActorUserId - From X-User-Id header: {ActorUserId}", actorUserId);
            return actorUserId;
        }

        // Fall back to JWT claims
        actorUserId = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;
        _logger.LogInformation("GetActorUserId - From JWT claims: {ActorUserId}, IsAuthenticated: {IsAuth}",
            actorUserId ?? "(null)", User.Identity?.IsAuthenticated ?? false);

        return actorUserId;
    }

    /// <summary>
    /// Gets all global settings
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<GlobalSettingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllSettings(Guid tenantId)
    {
        try
        {
            var settings = await _unitOfWork.GlobalSettings.GetAllSettingsAsync();
            var dtos = settings.Select(s => new GlobalSettingDto
            {
                Id = s.Id,
                Key = s.Key,
                Value = s.Value,
                Description = s.Description,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all settings");
            return StatusCode(500, new { message = "Failed to get settings", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a specific setting by key
    /// </summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(GlobalSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSetting(Guid tenantId, string key)
    {
        try
        {
            var setting = await _unitOfWork.GlobalSettings.GetByKeyAsync(key);
            if (setting == null)
            {
                return NotFound(new { message = $"Setting with key '{key}' not found" });
            }

            var dto = new GlobalSettingDto
            {
                Id = setting.Id,
                Key = setting.Key,
                Value = setting.Value,
                Description = setting.Description,
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting setting {Key}", key);
            return StatusCode(500, new { message = "Failed to get setting", error = ex.Message });
        }
    }

    /// <summary>
    /// Updates a setting value. Requires 'update-org-settings' permission.
    /// </summary>
    [HttpPut("{key}")]
    [RequirePermission("update-org-settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateSetting(Guid tenantId, string key, [FromBody] UpdateSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
        {
            return BadRequest(new { message = "Value is required" });
        }

        try
        {
            var actorUserId = GetActorUserId();

            // Get the old value for audit logging
            var existingSetting = await _unitOfWork.GlobalSettings.GetByKeyAsync(key);
            var oldValue = existingSetting?.Value;

            await _unitOfWork.GlobalSettings.UpsertAsync(key, request.Value, request.Description);
            await _unitOfWork.SaveChangesAsync();

            // Log the change
            if (actorUserId != null && Guid.TryParse(actorUserId, out var actorGuid))
            {
                await _unitOfWork.AuditLogs.LogAsync(
                    action: existingSetting != null ? "setting.updated" : "setting.created",
                    resourceType: "GlobalSetting",
                    resourceId: key,
                    actorUserId: actorGuid,
                    tenantId: tenantId,
                    oldValues: oldValue != null ? $"{{\"value\":\"{oldValue}\"}}" : null,
                    newValues: $"{{\"value\":\"{request.Value}\"}}"
                );
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Setting {Key} updated by user {UserId}", key, actorUserId);
            return Ok(new { message = "Setting updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating setting {Key}", key);
            return StatusCode(500, new { message = "Failed to update setting", error = ex.Message });
        }
    }

    /// <summary>
    /// Updates multiple settings at once. Requires 'update-org-settings' permission.
    /// </summary>
    [HttpPut]
    [RequirePermission("update-org-settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateSettings(Guid tenantId, [FromBody] UpdateSettingsRequest request)
    {
        if (request.Settings == null || !request.Settings.Any())
        {
            return BadRequest(new { message = "At least one setting is required" });
        }

        try
        {
            var actorUserId = GetActorUserId();

            await _unitOfWork.BeginTransactionAsync();

            foreach (var setting in request.Settings)
            {
                if (string.IsNullOrWhiteSpace(setting.Key) || string.IsNullOrWhiteSpace(setting.Value))
                {
                    continue;
                }

                var existingSetting = await _unitOfWork.GlobalSettings.GetByKeyAsync(setting.Key);
                var oldValue = existingSetting?.Value;

                await _unitOfWork.GlobalSettings.UpsertAsync(setting.Key, setting.Value, setting.Description);

                // Log each change
                if (actorUserId != null && Guid.TryParse(actorUserId, out var actorGuid))
                {
                    await _unitOfWork.AuditLogs.LogAsync(
                        action: existingSetting != null ? "setting.updated" : "setting.created",
                        resourceType: "GlobalSetting",
                        resourceId: setting.Key,
                        actorUserId: actorGuid,
                        tenantId: tenantId,
                        oldValues: oldValue != null ? $"{{\"value\":\"{oldValue}\"}}" : null,
                        newValues: $"{{\"value\":\"{setting.Value}\"}}"
                    );
                }
            }

            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Updated {Count} settings by user {UserId}", request.Settings.Count, actorUserId);
            return Ok(new { message = $"Updated {request.Settings.Count} settings successfully" });
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            _logger.LogError(ex, "Error updating settings");
            return StatusCode(500, new { message = "Failed to update settings", error = ex.Message });
        }
    }
}