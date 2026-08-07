using System.Security.Claims;
using Domus.Application.Abstractions;
using Domus.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Domus.Api.Hubs;

/// <summary>
/// Hub autenticado. Clientes autorizados entram em grupos residence/tenant para receber push MQTT→SignalR.
/// </summary>
[Authorize]
public sealed class DevicesHub : Hub
{
    private readonly IAccessControlService _accessControl;

    public DevicesHub(IAccessControlService accessControl)
    {
        _accessControl = accessControl;
    }

    public static string ResidenceGroup(Guid residenceId) => $"residence:{residenceId}";
    public static string TenantGroup(Guid tenantId) => $"tenant:{tenantId}";
    public static string UserGroup(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        if (TryGetUserId(out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinResidence(Guid residenceId)
    {
        var userId = RequireUserId();
        var access = await _accessControl.EnsureCanAccessResidenceAsync(userId, residenceId);
        if (!access.Succeeded)
        {
            throw new HubException(access.Error ?? "Acesso negado.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ResidenceGroup(residenceId));
    }

    public async Task LeaveResidence(Guid residenceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ResidenceGroup(residenceId));
    }

    public async Task JoinTenant(Guid tenantId)
    {
        var userId = RequireUserId();
        var access = await _accessControl.EnsureCanAccessTenantAsync(userId, tenantId);
        if (!access.Succeeded)
        {
            throw new HubException(access.Error ?? "Acesso negado.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId));
    }

    private Guid RequireUserId()
    {
        if (!TryGetUserId(out var userId))
        {
            throw new HubException("Usuário não autenticado.");
        }

        return userId;
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }
}

/// <summary>Aliases estáveis para clientes; nomes canônicos em <see cref="DeviceRealtimeEventNames"/>.</summary>
public static class DeviceRealtimeEvents
{
    public const string DeviceStatusChanged = DeviceRealtimeEventNames.DeviceStatusChanged;
    public const string GateStateChanged = DeviceRealtimeEventNames.GateStateChanged;
    public const string CommandUpdated = DeviceRealtimeEventNames.CommandUpdated;
    public const string DeviceOffline = DeviceRealtimeEventNames.DeviceOffline;
    public const string NotificationCreated = DeviceRealtimeEventNames.NotificationCreated;
}
