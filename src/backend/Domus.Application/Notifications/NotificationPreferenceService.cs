using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Application.Security;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Notifications;

public interface INotificationPreferenceService
{
    Task<Result<DeviceNotificationPreferenceResponse>> GetForDeviceAsync(
        Guid deviceId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<DeviceNotificationPreferenceResponse>> UpdateForDeviceAsync(
        Guid deviceId,
        UpdateDeviceNotificationPreferenceRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly IDomusDbContext _db;
    private readonly IAccessControlService _access;

    public NotificationPreferenceService(IDomusDbContext db, IAccessControlService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<Result<DeviceNotificationPreferenceResponse>> GetForDeviceAsync(
        Guid deviceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.EnsureCanAccessDeviceAsync(userId, deviceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<DeviceNotificationPreferenceResponse>.Failure(access.Error!, access.ErrorCode);
        }

        var device = await _db.Devices.AsNoTracking()
            .FirstAsync(d => d.Id == deviceId, cancellationToken);

        if (device.Type != DeviceType.Gate)
        {
            return Result<DeviceNotificationPreferenceResponse>.Failure(
                "Preferências de notificação disponíveis apenas para portões.",
                "invalid_device_type");
        }

        var pref = await _db.UserDeviceNotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.DeviceId == deviceId, cancellationToken);

        return Result<DeviceNotificationPreferenceResponse>.Success(Map(deviceId, pref));
    }

    public async Task<Result<DeviceNotificationPreferenceResponse>> UpdateForDeviceAsync(
        Guid deviceId,
        UpdateDeviceNotificationPreferenceRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.EnsureCanAccessDeviceAsync(userId, deviceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<DeviceNotificationPreferenceResponse>.Failure(access.Error!, access.ErrorCode);
        }

        var device = await _db.Devices.AsNoTracking()
            .FirstAsync(d => d.Id == deviceId, cancellationToken);

        if (device.Type != DeviceType.Gate)
        {
            return Result<DeviceNotificationPreferenceResponse>.Failure(
                "Preferências de notificação disponíveis apenas para portões.",
                "invalid_device_type");
        }

        var pref = await _db.UserDeviceNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.DeviceId == deviceId, cancellationToken);

        if (pref is null)
        {
            pref = UserDeviceNotificationPreference.CreateDefault(userId, deviceId, device.TenantId);
            _db.UserDeviceNotificationPreferences.Add(pref);
        }

        pref.Update(
            request.NotifyOnOpen,
            request.NotifyOnClose,
            request.NotifyWhenOpenTooLong,
            request.OpenAlertMinutes);

        await _db.SaveChangesAsync(cancellationToken);
        return Result<DeviceNotificationPreferenceResponse>.Success(Map(deviceId, pref));
    }

    private static DeviceNotificationPreferenceResponse Map(
        Guid deviceId,
        UserDeviceNotificationPreference? pref) =>
        new(
            deviceId,
            pref?.NotifyOnOpen ?? false,
            pref?.NotifyOnClose ?? false,
            pref?.NotifyWhenOpenTooLong ?? false,
            pref?.OpenAlertMinutes ?? 15,
            pref?.UpdatedAt);
}
