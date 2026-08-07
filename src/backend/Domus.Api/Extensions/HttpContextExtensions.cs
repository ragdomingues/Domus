using System.Security.Claims;

namespace Domus.Api.Extensions;

public static class HttpContextExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (!Guid.TryParse(raw, out var userId))
        {
            throw new UnauthorizedAccessException("Usuário não autenticado.");
        }

        return userId;
    }

    public static string? GetIpAddress(this HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString();

    public static string? GetUserAgent(this HttpContext httpContext) =>
        httpContext.Request.Headers.UserAgent.ToString();
}
