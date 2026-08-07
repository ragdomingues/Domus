using Domus.Application.Abstractions;
using Domus.Application.Auth;
using Domus.Application.Devices;
using Domus.Application.Notifications;
using Domus.Application.Residences;
using Domus.Application.Security;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Domus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.TryAddSingleton<IDeviceRealtimeNotifier, NullDeviceRealtimeNotifier>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddSingleton<IActivateAbuseGuard, ActivateAbuseGuard>();
        services.AddScoped<ICommandIdempotencyService, CommandIdempotencyService>();
        services.AddScoped<IResidenceService, ResidenceService>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IProvisioningService, ProvisioningService>();
        services.AddScoped<ICommandService, CommandService>();
        services.AddScoped<IDeviceEventService, DeviceEventService>();
        services.AddScoped<IDeviceTelemetryService, DeviceTelemetryService>();
        services.AddScoped<IDevicePresenceService, DevicePresenceService>();
        services.AddScoped<IMqttAuthService, MqttAuthService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<INotificationInboxService, NotificationInboxService>();
        services.AddScoped<IGateNotificationService, GateNotificationService>();
        services.AddScoped<IPushTokenService, PushTokenService>();
        services.AddScoped<IDeviceSimulationService, DeviceSimulationService>();
        services.AddScoped<IHistoryRetentionService, HistoryRetentionService>();
        return services;
    }
}
