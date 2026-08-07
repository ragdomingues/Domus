using Domus.Api.Extensions;
using Domus.Application.Common;
using Domus.Application.Devices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Domus.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ProvisioningController : ControllerBase
{
    private readonly IProvisioningService _provisioning;

    public ProvisioningController(IProvisioningService provisioning)
    {
        _provisioning = provisioning;
    }

    /// <summary>
    /// Issues a one-time provisioning code. Code plaintext is returned only here.
    /// </summary>
    [Authorize]
    [HttpPost("devices/{deviceId:guid}/provisioning")]
    public async Task<IActionResult> Issue(Guid deviceId, [FromBody] IssueProvisioningBody? body, CancellationToken cancellationToken)
    {
        var result = await _provisioning.IssueAsync(
            new IssueProvisioningRequest(deviceId, body?.ExpiresInMinutes),
            new DeviceActorContext(User.GetUserId(), HttpContext.GetIpAddress(), HttpContext.GetUserAgent()),
            cancellationToken);

        return ToActionResult(result);
    }

    [Authorize]
    [HttpGet("provisioning/{provisioningId:guid}")]
    public async Task<IActionResult> Status(Guid provisioningId, CancellationToken cancellationToken)
    {
        var result = await _provisioning.GetStatusAsync(provisioningId, User.GetUserId(), cancellationToken);
        // Never includes secrets or provisioning code.
        return ToActionResult(result);
    }

    /// <summary>
    /// Device activation endpoint. Returns MQTT credentials once.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("devices/activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateProvisioningRequest request, CancellationToken cancellationToken)
    {
        var result = await _provisioning.ActivateAsync(request, HttpContext.GetIpAddress(), cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (!result.Succeeded)
        {
            return result.ErrorCode switch
            {
                "access_denied" or "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, code = result.ErrorCode }),
                "not_found" => NotFound(new { error = result.Error, code = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, code = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }
}

public sealed record IssueProvisioningBody(int? ExpiresInMinutes);
