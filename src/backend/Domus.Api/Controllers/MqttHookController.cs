using Domus.Application.Devices;
using Domus.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Domus.Api.Controllers;

/// <summary>
/// HTTP hooks for EMQX authentication and ACL.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("internal/mqtt")]
public sealed class MqttHookController : ControllerBase
{
    private readonly IMqttAuthService _auth;
    private readonly MqttOptions _options;

    public MqttHookController(IMqttAuthService auth, IOptions<MqttOptions> options)
    {
        _auth = auth;
        _options = options.Value;
    }

    [HttpPost("auth")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Authenticate([FromBody] EmqxAuthBody body, CancellationToken cancellationToken)
    {
        if (!IsHookAuthorized())
        {
            return Unauthorized();
        }

        var allowed = await _auth.AuthenticateAsync(
            new MqttAuthRequest(body.Username ?? string.Empty, body.Password ?? string.Empty),
            cancellationToken);

        return Ok(new { result = allowed ? "allow" : "deny", is_superuser = false });
    }

    [HttpPost("acl")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Authorize([FromBody] EmqxAclBody body, CancellationToken cancellationToken)
    {
        if (!IsHookAuthorized())
        {
            return Unauthorized();
        }

        var allowed = await _auth.AuthorizeAsync(
            new MqttAclRequest(
                body.Username ?? string.Empty,
                body.Topic ?? string.Empty,
                body.Action ?? body.Access ?? string.Empty),
            cancellationToken);

        return Ok(new { result = allowed ? "allow" : "deny" });
    }

    private bool IsHookAuthorized()
    {
        if (!Request.Headers.TryGetValue("X-Domus-Mqtt-Hook", out var secret))
        {
            return false;
        }

        return string.Equals(secret.ToString(), _options.HookSecret, StringComparison.Ordinal);
    }
}

public sealed record EmqxAuthBody(string? Username, string? Password);
public sealed record EmqxAclBody(string? Username, string? Topic, string? Action, string? Access);
