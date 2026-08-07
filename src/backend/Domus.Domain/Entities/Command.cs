using Domus.Domain.Common;
using Domus.Domain.Enums;

namespace Domus.Domain.Entities;

public class Command : Entity
{
    public const int MaxAttempts = 3;

    public Guid TenantId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid? UserId { get; private set; }
    public CommandAction Action { get; private set; }
    public CommandSource Source { get; private set; } = CommandSource.API;
    public CommandStatus Status { get; private set; } = CommandStatus.Pending;
    public string? IdempotencyKey { get; private set; }
    public string? Payload { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? ExecutedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid CorrelationId { get; private set; }

    public Device? Device { get; private set; }
    public User? User { get; private set; }

    private Command()
    {
    }

    public static Command Create(
        Guid tenantId,
        Guid deviceId,
        Guid? userId,
        CommandAction action,
        DateTimeOffset expiresAt,
        CommandSource source = CommandSource.API,
        string? idempotencyKey = null,
        string? payload = null)
    {
        return new Command
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            UserId = userId,
            Action = action,
            Source = source,
            Status = CommandStatus.Pending,
            IdempotencyKey = idempotencyKey,
            Payload = payload,
            ExpiresAt = expiresAt,
            CorrelationId = Guid.NewGuid(),
            AttemptCount = 0
        };
    }

    public void MarkSent(DateTimeOffset? at = null)
    {
        EnsureNotTerminal();
        EnsureNotExpired(at ?? DateTimeOffset.UtcNow);
        Status = CommandStatus.Sent;
        SentAt = at ?? DateTimeOffset.UtcNow;
        AttemptCount++;
        NextRetryAt = null;
    }

    public void MarkDelivered(DateTimeOffset? at = null)
    {
        if (Status is not (CommandStatus.Sent or CommandStatus.Pending))
        {
            throw new InvalidOperationException($"Não é possível marcar Delivered a partir de {Status}.");
        }

        EnsureNotExpired(at ?? DateTimeOffset.UtcNow);
        Status = CommandStatus.Delivered;
        DeliveredAt = at ?? DateTimeOffset.UtcNow;
    }

    public void MarkExecuted(DateTimeOffset? at = null)
    {
        if (Status is not (CommandStatus.Delivered or CommandStatus.Sent))
        {
            throw new InvalidOperationException($"Não é possível marcar Executed a partir de {Status}.");
        }

        Status = CommandStatus.Executed;
        ExecutedAt = at ?? DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        EnsureNotTerminal();
        Status = CommandStatus.Failed;
        FailureReason = reason;
    }

    public void MarkExpired()
    {
        if (Status is CommandStatus.Executed or CommandStatus.Failed)
        {
            return;
        }

        Status = CommandStatus.Expired;
        FailureReason ??= "Command expired";
    }

    public bool TryScheduleRetry(DateTimeOffset utcNow, TimeSpan delay)
    {
        if (Status is CommandStatus.Executed or CommandStatus.Failed or CommandStatus.Expired or CommandStatus.Delivered)
        {
            return false;
        }

        if (AttemptCount >= MaxAttempts || utcNow >= ExpiresAt)
        {
            MarkExpired();
            return false;
        }

        Status = CommandStatus.Pending;
        NextRetryAt = utcNow.Add(delay);
        return true;
    }

    /// <summary>
    /// Records a failed publish attempt without transitioning to Sent.
    /// </summary>
    public bool RegisterFailedSendAttempt(string reason, DateTimeOffset utcNow, TimeSpan retryDelay)
    {
        if (Status is CommandStatus.Executed or CommandStatus.Failed or CommandStatus.Expired or CommandStatus.Delivered)
        {
            return false;
        }

        AttemptCount++;
        FailureReason = reason;

        if (AttemptCount >= MaxAttempts || utcNow >= ExpiresAt)
        {
            if (utcNow >= ExpiresAt)
            {
                MarkExpired();
            }
            else
            {
                MarkFailed(reason);
            }

            return false;
        }

        Status = CommandStatus.Pending;
        NextRetryAt = utcNow.Add(retryDelay);
        return true;
    }

    public bool IsExpired(DateTimeOffset utcNow) => utcNow > ExpiresAt &&
        Status is not (CommandStatus.Executed or CommandStatus.Failed or CommandStatus.Expired);

    private void EnsureNotTerminal()
    {
        if (Status is CommandStatus.Executed or CommandStatus.Failed or CommandStatus.Expired)
        {
            throw new InvalidOperationException($"Comando já está em estado terminal: {Status}.");
        }
    }

    private void EnsureNotExpired(DateTimeOffset utcNow)
    {
        if (utcNow > ExpiresAt)
        {
            MarkExpired();
            throw new InvalidOperationException("Comando expirado.");
        }
    }
}
