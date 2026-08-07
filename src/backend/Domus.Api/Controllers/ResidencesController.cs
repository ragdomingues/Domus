using Domus.Api.Extensions;
using Domus.Application.Residences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Domus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class ResidencesController : ControllerBase
{
    private readonly IResidenceService _residences;

    public ResidencesController(IResidenceService residences)
    {
        _residences = residences;
    }

    [HttpPost("tenants/{tenantId:guid}/residences")]
    public async Task<IActionResult> Create(Guid tenantId, [FromBody] CreateResidenceBody body, CancellationToken cancellationToken)
    {
        var result = await _residences.CreateAsync(
            new CreateResidenceRequest(tenantId, body.Name, body.Timezone, body.Address),
            Actor(),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("tenants/{tenantId:guid}/residences")]
    public async Task<IActionResult> List(Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await _residences.ListByTenantAsync(tenantId, User.GetUserId(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("residences/{residenceId:guid}")]
    public async Task<IActionResult> Get(Guid residenceId, CancellationToken cancellationToken)
    {
        var result = await _residences.GetAsync(residenceId, User.GetUserId(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("residences/{residenceId:guid}")]
    public async Task<IActionResult> Update(Guid residenceId, [FromBody] UpdateResidenceRequest request, CancellationToken cancellationToken)
    {
        var result = await _residences.UpdateAsync(residenceId, request, Actor(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("residences/{residenceId:guid}")]
    public async Task<IActionResult> Delete(Guid residenceId, CancellationToken cancellationToken)
    {
        var result = await _residences.SoftDeleteAsync(residenceId, Actor(), cancellationToken);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode, result.Error);
        }

        return NoContent();
    }

    private ResidenceMembershipContext Actor() =>
        new(User.GetUserId(), HttpContext.GetIpAddress(), HttpContext.GetUserAgent());

    private IActionResult ToActionResult<T>(Application.Common.Result<T> result)
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
            "validation_error" => BadRequest(new { error, code }),
            _ => BadRequest(new { error, code })
        };
}

public sealed record CreateResidenceBody(string Name, string? Timezone, string? Address);
