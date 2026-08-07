using Domus.Api.Extensions;
using Domus.Application.Common;
using Domus.Application.Devices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Domus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class DevicesController : ControllerBase
{
    private readonly IDeviceService _devices;
    private readonly IDeviceEventService _events;
    private readonly IDeviceSimulationService _simulation;

    public DevicesController(
        IDeviceService devices,
        IDeviceEventService events,
        IDeviceSimulationService simulation)
    {
        _devices = devices;
        _events = events;
        _simulation = simulation;
    }

    [HttpPost("devices/{deviceId:guid}/simulate")]
    public async Task<IActionResult> EnableSimulation(Guid deviceId, CancellationToken cancellationToken)
    {
        var result = await _simulation.EnableAsync(deviceId, Actor(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("devices/{deviceId:guid}/simulate")]
    public async Task<IActionResult> DisableSimulation(
        Guid deviceId,
        [FromQuery] int? expiresInMinutes,
        CancellationToken cancellationToken)
    {
        var result = await _simulation.DisableAsync(
            deviceId,
            Actor(),
            expiresInMinutes,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("residences/{residenceId:guid}/devices")]
    public async Task<IActionResult> Create(Guid residenceId, [FromBody] CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        var payload = request with { ResidenceId = residenceId };
        var result = await _devices.CreateAsync(payload, Actor(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("residences/{residenceId:guid}/devices")]
    public async Task<IActionResult> List(Guid residenceId, CancellationToken cancellationToken)
    {
        var result = await _devices.ListByResidenceAsync(residenceId, User.GetUserId(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("devices/{deviceId:guid}")]
    public async Task<IActionResult> Get(Guid deviceId, CancellationToken cancellationToken)
    {
        var result = await _devices.GetAsync(deviceId, User.GetUserId(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("devices/{deviceId:guid}")]
    public async Task<IActionResult> Update(Guid deviceId, [FromBody] UpdateDeviceRequest request, CancellationToken cancellationToken)
    {
        var result = await _devices.UpdateAsync(deviceId, request, Actor(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("devices/{deviceId:guid}/configuration")]
    public async Task<IActionResult> UpdateConfiguration(
        Guid deviceId,
        [FromBody] DeviceConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _devices.UpdateConfigurationAsync(deviceId, request, Actor(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("devices/{deviceId:guid}")]
    public async Task<IActionResult> Delete(Guid deviceId, CancellationToken cancellationToken)
    {
        var result = await _devices.SoftDeleteAsync(deviceId, Actor(), cancellationToken);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode, result.Error);
        }

        return NoContent();
    }

    [HttpGet("devices/{deviceId:guid}/events")]
    public async Task<IActionResult> ListEvents(
        Guid deviceId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _events.ListByDeviceAsync(deviceId, User.GetUserId(), take, cancellationToken);
        return ToActionResult(result);
    }

    private DeviceActorContext Actor() =>
        new(User.GetUserId(), HttpContext.GetIpAddress(), HttpContext.GetUserAgent());

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
