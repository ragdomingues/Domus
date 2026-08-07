using System.Text.Json;
using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Application.Security;
using Domus.Domain.Commands;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domus.Application.Devices;

public sealed record CreateCommandRequest(
    Guid DeviceId,
    CommandAction Action,
    string? IdempotencyKey = null,
    int? TimeoutSeconds = null,
    CommandSource Source = CommandSource.API);

public sealed record CommandResponse(
    Guid Id,
    Guid TenantId,
    Guid DeviceId,
    Guid? UserId,
    string? UserName,
    CommandAction Action,
    CommandSource Source,
    CommandStatus Status,
    string? IdempotencyKey,
    Guid CorrelationId,
    int AttemptCount,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ExecutedAt,
    DateTimeOffset? NextRetryAt,
    string? FailureReason,
    DateTimeOffset CreatedAt);

public interface ICommandService
{
    Task<Result<CommandResponse>> CreateAsync(CreateCommandRequest request, DeviceActorContext context, CancellationToken cancellationToken = default);
    Task<Result<CommandResponse>> GetAsync(Guid commandId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CommandResponse>>> ListByDeviceAsync(Guid deviceId, Guid userId, int take = 50, CancellationToken cancellationToken = default);
    Task ProcessDueCommandsAsync(CancellationToken cancellationToken = default);
}

public sealed class CreateCommandRequestValidator : AbstractValidator<CreateCommandRequest>
{
    public CreateCommandRequestValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.IdempotencyKey).MaximumLength(128);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(5, 300).When(x => x.TimeoutSeconds.HasValue);
    }
}

public sealed class CommandService : ICommandService
{
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);

    private readonly IDomusDbContext _db;
    private readonly IAccessControlService _access;
    private readonly IDeviceMessenger _messenger;
    private readonly ICommandIdempotencyService _idempotency;
    private readonly IDeviceRealtimeNotifier _realtime;
    private readonly IDeviceSimulationService _simulation;
    private readonly IDateTimeProvider _clock;
    private readonly HistoryRetentionOptions _retention;
    private readonly IValidator<CreateCommandRequest> _validator;
    private readonly ILogger<CommandService> _logger;

    public CommandService(
        IDomusDbContext db,
        IAccessControlService access,
        IDeviceMessenger messenger,
        ICommandIdempotencyService idempotency,
        IDeviceRealtimeNotifier realtime,
        IDeviceSimulationService simulation,
        IDateTimeProvider clock,
        IOptions<HistoryRetentionOptions> retention,
        IValidator<CreateCommandRequest> validator,
        ILogger<CommandService> logger)
    {
        _db = db;
        _access = access;
        _messenger = messenger;
        _idempotency = idempotency;
        _realtime = realtime;
        _simulation = simulation;
        _clock = clock;
        _retention = retention.Value;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<CommandResponse>> CreateAsync(
        CreateCommandRequest request,
        DeviceActorContext context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CommandResponse>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), "validation_error");
        }

        var access = await _access.EnsureCanAccessDeviceAsync(context.UserId, request.DeviceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<CommandResponse>.Failure(access.Error!, access.ErrorCode);
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _idempotency.FindExistingAsync(request.DeviceId, request.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return Result<CommandResponse>.Success(Map(existing));
            }
        }

        var device = await _db.Devices.FirstAsync(d => d.Id == request.DeviceId, cancellationToken);
        if (device.LifecycleStatus != DeviceLifecycleStatus.Active)
        {
            return Result<CommandResponse>.Failure("Dispositivo não está ativo.", "device_not_active");
        }

        if (device.Type == DeviceType.Gate)
        {
            var config = await _db.DeviceConfigurations.AsNoTracking()
                .FirstOrDefaultAsync(c => c.DeviceId == device.Id, cancellationToken);
            if (request.Action == CommandAction.Close && config is { SupportsClose: false })
            {
                return Result<CommandResponse>.Failure("Dispositivo não suporta CLOSE.", "action_not_supported");
            }

            if (request.Action == CommandAction.Stop && config is { SupportsStop: false })
            {
                return Result<CommandResponse>.Failure("Dispositivo não suporta STOP.", "action_not_supported");
            }

            var gate = await _db.Gates.AsNoTracking()
                .FirstOrDefaultAsync(g => g.DeviceId == device.Id, cancellationToken);
            if (gate is not null && GateCommandRules.IsRedundant(request.Action, gate.GateState))
            {
                var code = request.Action == CommandAction.Open ? "gate_already_open" : "gate_already_closed";
                return Result<CommandResponse>.Failure(
                    request.Action == CommandAction.Open
                        ? "Portão já está aberto."
                        : "Portão já está fechado.",
                    code);
            }
        }

        var now = _clock.UtcNow;
        var inFlight = await _db.Commands
            .Where(c => c.DeviceId == device.Id &&
                        c.ExpiresAt >= now &&
                        (c.Status == CommandStatus.Pending ||
                         c.Status == CommandStatus.Sent ||
                         c.Status == CommandStatus.Delivered))
            .Select(c => new { c.Id, c.Action })
            .ToListAsync(cancellationToken);

        var conflict = inFlight.FirstOrDefault(c => CommandConflictRules.Conflicts(c.Action, request.Action));
        if (conflict is not null)
        {
            return Result<CommandResponse>.Failure(
                $"Comando conflitante em andamento ({conflict.Action}).",
                "command_conflict");
        }

        var timeoutSeconds = request.TimeoutSeconds
            ?? (await _db.DeviceConfigurations.AsNoTracking()
                .Where(c => c.DeviceId == device.Id)
                .Select(c => (int?)c.CommandTimeoutSeconds)
                .FirstOrDefaultAsync(cancellationToken))
            ?? 30;

        var expiresAt = now.AddSeconds(timeoutSeconds);
        var command = Command.Create(
            device.TenantId,
            device.Id,
            context.UserId,
            request.Action,
            expiresAt,
            request.Source,
            request.IdempotencyKey,
            payload: JsonSerializer.Serialize(new { action = request.Action.ToString().ToUpperInvariant() }));

        _db.Commands.Add(command);
        _db.DeviceEvents.Add(DeviceEvent.Create(
            device.TenantId,
            device.Id,
            action: request.Action.ToString().ToUpperInvariant(),
            result: EventResult.Pending,
            origin: MapOrigin(request.Source),
            userId: context.UserId,
            commandId: command.Id,
            ipAddress: context.IpAddress,
            userAgent: context.UserAgent));

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.CommandCreated,
            true,
            context.UserId,
            device.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"command:{command.Id};device:{device.Id};action:{request.Action};source:{request.Source}"));

        await _db.SaveChangesAsync(cancellationToken);

        await TryPublishAsync(command, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyCommandAsync(command, device.ResidenceId, cancellationToken);

        if (device.IsSimulated)
        {
            await _simulation.CompleteCommandAsync(command.Id, cancellationToken);
            var refreshed = await _db.Commands.AsNoTracking()
                .FirstAsync(c => c.Id == command.Id, cancellationToken);
            return Result<CommandResponse>.Success(Map(refreshed));
        }

        return Result<CommandResponse>.Success(Map(command));
    }

    public async Task<Result<CommandResponse>> GetAsync(
        Guid commandId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var command = await _db.Commands.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commandId, cancellationToken);
        if (command is null)
        {
            return Result<CommandResponse>.Failure("Comando não encontrado.", "not_found");
        }

        var access = await _access.EnsureCanAccessDeviceAsync(userId, command.DeviceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<CommandResponse>.Failure(access.Error!, access.ErrorCode);
        }

        return Result<CommandResponse>.Success(Map(command));
    }

    public async Task<Result<IReadOnlyList<CommandResponse>>> ListByDeviceAsync(
        Guid deviceId,
        Guid userId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.EnsureCanAccessDeviceAsync(userId, deviceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<IReadOnlyList<CommandResponse>>.Failure(access.Error!, access.ErrorCode);
        }

        take = Math.Clamp(take, 1, 200);
        var cutoff = _clock.UtcNow.AddDays(-Math.Clamp(_retention.RetentionDays, 1, 3650));
        var items = await (
            from c in _db.Commands.AsNoTracking()
            where c.DeviceId == deviceId && c.CreatedAt >= cutoff
            orderby c.CreatedAt descending
            join u in _db.Users.AsNoTracking() on c.UserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            select Map(c, u != null ? u.Name : null)
        ).Take(take).ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CommandResponse>>.Success(items);
    }

    public async Task ProcessDueCommandsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var changed = new List<(Command Command, Guid ResidenceId)>();

        var expired = await _db.Commands
            .Where(c => c.ExpiresAt < now &&
                        (c.Status == CommandStatus.Pending || c.Status == CommandStatus.Sent || c.Status == CommandStatus.Delivered))
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var command in expired)
        {
            command.MarkExpired();
            changed.Add((command, Guid.Empty));
        }

        var retries = await _db.Commands
            .Where(c => c.Status == CommandStatus.Pending &&
                        c.NextRetryAt != null &&
                        c.NextRetryAt <= now &&
                        c.ExpiresAt >= now)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var command in retries)
        {
            var before = command.Status;
            var attempts = command.AttemptCount;
            await TryPublishAsync(command, cancellationToken);
            if (command.Status != before || command.AttemptCount != attempts)
            {
                changed.Add((command, Guid.Empty));
            }
        }

        if (expired.Count > 0 || retries.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (changed.Count == 0)
        {
            return;
        }

        var deviceIds = changed.Select(c => c.Command.DeviceId).Distinct().ToList();
        var residences = await _db.Devices.AsNoTracking()
            .Where(d => deviceIds.Contains(d.Id))
            .Select(d => new { d.Id, d.ResidenceId })
            .ToDictionaryAsync(d => d.Id, d => d.ResidenceId, cancellationToken);

        foreach (var (command, _) in changed.DistinctBy(c => c.Command.Id))
        {
            if (residences.TryGetValue(command.DeviceId, out var residenceId))
            {
                await NotifyCommandAsync(command, residenceId, cancellationToken);
            }
        }
    }

    private async Task TryPublishAsync(Command command, CancellationToken cancellationToken)
    {
        if (command.IsExpired(_clock.UtcNow))
        {
            command.MarkExpired();
            return;
        }

        try
        {
            await _messenger.PublishCommandAsync(
                command.TenantId,
                command.DeviceId,
                command.Id,
                command.CorrelationId,
                command.Action.ToString().ToUpperInvariant(),
                _clock.UtcNow,
                command.ExpiresAt,
                cancellationToken);

            command.MarkSent(_clock.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao publicar comando {CommandId}", command.Id);
            var retried = command.RegisterFailedSendAttempt(ex.Message, _clock.UtcNow, DefaultRetryDelay);
            if (!retried)
            {
                _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                    SecurityAuditAction.CommandFailed,
                    false,
                    command.UserId,
                    command.TenantId,
                    details: $"command:{command.Id};reason:{ex.Message}"));
            }
        }
    }

    private Task NotifyCommandAsync(Command command, Guid residenceId, CancellationToken cancellationToken) =>
        _realtime.NotifyCommandUpdatedAsync(
            command.TenantId,
            residenceId,
            command.Id,
            command.DeviceId,
            command.Status,
            command.Action,
            command.FailureReason,
            cancellationToken);

    private static EventOrigin MapOrigin(CommandSource source) =>
        source switch
        {
            CommandSource.MobileApp => EventOrigin.App,
            CommandSource.WebAdmin => EventOrigin.Admin,
            CommandSource.Automation => EventOrigin.Automation,
            CommandSource.System => EventOrigin.System,
            _ => EventOrigin.App
        };

    private static CommandResponse Map(Command command, string? userName = null) =>
        new(
            command.Id,
            command.TenantId,
            command.DeviceId,
            command.UserId,
            userName,
            command.Action,
            command.Source,
            command.Status,
            command.IdempotencyKey,
            command.CorrelationId,
            command.AttemptCount,
            command.ExpiresAt,
            command.SentAt,
            command.DeliveredAt,
            command.ExecutedAt,
            command.NextRetryAt,
            command.FailureReason,
            command.CreatedAt);
}
