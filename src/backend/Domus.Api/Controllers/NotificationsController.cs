using Domus.Api.Extensions;
using Domus.Application.Common;
using Domus.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Domus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationPreferenceService _preferences;
    private readonly INotificationInboxService _inbox;
    private readonly IPushTokenService _pushTokens;

    public NotificationsController(
        INotificationPreferenceService preferences,
        INotificationInboxService inbox,
        IPushTokenService pushTokens)
    {
        _preferences = preferences;
        _inbox = inbox;
        _pushTokens = pushTokens;
    }

    [HttpPost("me/push-tokens")]
    public async Task<IActionResult> RegisterPushToken(
        [FromBody] RegisterPushTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _pushTokens.RegisterAsync(User.GetUserId(), request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("me/push-tokens")]
    public async Task<IActionResult> UnregisterPushToken(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var result = await _pushTokens.UnregisterAsync(User.GetUserId(), token, cancellationToken);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode, result.Error);
        }

        return NoContent();
    }

    [HttpGet("devices/{deviceId:guid}/notification-preferences")]
    public async Task<IActionResult> GetPreferences(Guid deviceId, CancellationToken cancellationToken)
    {
        var result = await _preferences.GetForDeviceAsync(deviceId, User.GetUserId(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("devices/{deviceId:guid}/notification-preferences")]
    public async Task<IActionResult> UpdatePreferences(
        Guid deviceId,
        [FromBody] UpdateDeviceNotificationPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _preferences.UpdateForDeviceAsync(
            deviceId,
            request,
            User.GetUserId(),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> List([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await _inbox.ListAsync(User.GetUserId(), take, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var result = await _inbox.MarkReadAsync(notificationId, User.GetUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode, result.Error);
        }

        return NoContent();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var result = await _inbox.MarkAllReadAsync(User.GetUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode, result.Error);
        }

        return NoContent();
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode, result.Error);
        }

        return Ok(result.Value);
    }

    private IActionResult MapError(string? code, string? error) =>
        code switch
        {
            "access_denied" or "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { error, code }),
            "not_found" => NotFound(new { error, code }),
            _ => BadRequest(new { error, code })
        };
}
