using Domus.Api.Extensions;
using Domus.Application.Common;
using Domus.Application.Devices;
using Domus.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Domus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class CommandsController : ControllerBase
{
    private readonly ICommandService _commands;

    public CommandsController(ICommandService commands)
    {
        _commands = commands;
    }

    [HttpPost("devices/{deviceId:guid}/commands")]
    public async Task<IActionResult> Create(
        Guid deviceId,
        [FromBody] CreateCommandBody body,
        CancellationToken cancellationToken)
    {
        var result = await _commands.CreateAsync(
            new CreateCommandRequest(
                deviceId,
                body.Action,
                body.IdempotencyKey,
                body.TimeoutSeconds,
                body.Source ?? CommandSource.MobileApp),
            new DeviceActorContext(User.GetUserId(), HttpContext.GetIpAddress(), HttpContext.GetUserAgent()),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("commands/{commandId:guid}")]
    public async Task<IActionResult> Get(Guid commandId, CancellationToken cancellationToken)
    {
        var result = await _commands.GetAsync(commandId, User.GetUserId(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("devices/{deviceId:guid}/commands")]
    public async Task<IActionResult> List(Guid deviceId, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await _commands.ListByDeviceAsync(deviceId, User.GetUserId(), take, cancellationToken);
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

public sealed record CreateCommandBody(
    CommandAction Action,
    string? IdempotencyKey,
    int? TimeoutSeconds,
    CommandSource? Source);
