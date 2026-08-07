using Domus.Api.Extensions;
using Domus.Application.Common;
using Domus.Application.Residences;
using Domus.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Domus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class MembersController : ControllerBase
{
    private readonly IMembershipService _members;

    public MembersController(IMembershipService members)
    {
        _members = members;
    }

    [HttpGet("residences/{residenceId:guid}/members")]
    public async Task<IActionResult> List(Guid residenceId, CancellationToken cancellationToken)
    {
        var result = await _members.ListAsync(residenceId, User.GetUserId(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("residences/{residenceId:guid}/members")]
    public async Task<IActionResult> Invite(
        Guid residenceId,
        [FromBody] InviteMemberBody body,
        CancellationToken cancellationToken)
    {
        var result = await _members.InviteAsync(
            new InviteMemberRequest(residenceId, body.Email, body.Name, body.Role, body.ValidUntilDays),
            Actor(),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPatch("residences/{residenceId:guid}/members/{membershipId:guid}")]
    public async Task<IActionResult> UpdateRole(
        Guid residenceId,
        Guid membershipId,
        [FromBody] UpdateMemberRoleBody body,
        CancellationToken cancellationToken)
    {
        var result = await _members.UpdateRoleAsync(
            residenceId,
            membershipId,
            new UpdateMemberRoleRequest(body.Role, body.ValidUntilDays),
            Actor(),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("residences/{residenceId:guid}/members/{membershipId:guid}")]
    public async Task<IActionResult> Revoke(
        Guid residenceId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        var result = await _members.RevokeAsync(residenceId, membershipId, Actor(), cancellationToken);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode, result.Error);
        }

        return NoContent();
    }

    private ResidenceMembershipContext Actor() =>
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

public sealed record InviteMemberBody(
    string Email,
    string? Name,
    ResidenceRole Role,
    int? ValidUntilDays);

public sealed record UpdateMemberRoleBody(ResidenceRole Role, int? ValidUntilDays);
