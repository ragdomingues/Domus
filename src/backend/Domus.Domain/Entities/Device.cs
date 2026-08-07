using Domus.Domain.Common;
using Domus.Domain.Enums;

namespace Domus.Domain.Entities;

public class Device : SoftDeletableEntity
{
    public Guid TenantId { get; private set; }
    public Guid ResidenceId { get; private set; }
    public DeviceType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? FirmwareVersion { get; private set; }
    public DeviceLifecycleStatus LifecycleStatus { get; private set; } = DeviceLifecycleStatus.Created;
    public DeviceConnectionStatus ConnectionStatus { get; private set; } = DeviceConnectionStatus.Unknown;
    public DateTimeOffset? LastSeenAt { get; private set; }
    public string? MqttUsername { get; private set; }
    public string? MqttSecretHash { get; private set; }
    public string? HardwareId { get; private set; }
    /// <summary>Device virtual para demo sem ESP32 — comandos são concluídos pelo backend.</summary>
    public bool IsSimulated { get; private set; }

    public Residence? Residence { get; private set; }
    public Gate? Gate { get; private set; }
    public DeviceConfiguration? Configuration { get; private set; }
    public ICollection<DeviceProvisioning> Provisionings { get; private set; } = new List<DeviceProvisioning>();
    public ICollection<Command> Commands { get; private set; } = new List<Command>();
    public ICollection<DeviceEvent> Events { get; private set; } = new List<DeviceEvent>();
    public ICollection<DevicePermission> Permissions { get; private set; } = new List<DevicePermission>();

    private Device()
    {
    }

    public static Device Create(Guid tenantId, Guid residenceId, DeviceType type, string name)
    {
        return new Device
        {
            TenantId = tenantId,
            ResidenceId = residenceId,
            Type = type,
            Name = name.Trim(),
            LifecycleStatus = DeviceLifecycleStatus.Created,
            ConnectionStatus = DeviceConnectionStatus.Unknown
        };
    }

    public bool HasMqttCredentials =>
        !string.IsNullOrWhiteSpace(MqttUsername) && !string.IsNullOrWhiteSpace(MqttSecretHash);

    public void MarkProvisioning()
    {
        if (LifecycleStatus is DeviceLifecycleStatus.Deleted or DeviceLifecycleStatus.Suspended)
        {
            throw new InvalidOperationException("Dispositivo não pode entrar em provisioning neste estado.");
        }

        LifecycleStatus = DeviceLifecycleStatus.Provisioning;
    }

    public void ActivateMqttCredentials(string mqttUsername, string mqttSecretHash)
    {
        if (HasMqttCredentials)
        {
            throw new InvalidOperationException("Dispositivo já possui credenciais MQTT.");
        }

        MqttUsername = mqttUsername;
        MqttSecretHash = mqttSecretHash;
        LifecycleStatus = DeviceLifecycleStatus.Active;
    }

    public void Suspend()
    {
        if (LifecycleStatus == DeviceLifecycleStatus.Deleted)
        {
            throw new InvalidOperationException("Dispositivo excluído.");
        }

        LifecycleStatus = DeviceLifecycleStatus.Suspended;
        ConnectionStatus = DeviceConnectionStatus.Offline;
    }

    public void Resume()
    {
        if (LifecycleStatus != DeviceLifecycleStatus.Suspended)
        {
            throw new InvalidOperationException("Somente dispositivos suspensos podem ser reativados.");
        }

        LifecycleStatus = HasMqttCredentials
            ? DeviceLifecycleStatus.Active
            : DeviceLifecycleStatus.Created;
        ConnectionStatus = DeviceConnectionStatus.Unknown;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(name));
        }

        Name = name.Trim();
    }

    public void SetHardwareId(string hardwareId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId))
        {
            throw new ArgumentException("HardwareId é obrigatório.", nameof(hardwareId));
        }

        HardwareId = hardwareId.Trim();
    }

    public void SetFirmwareVersion(string? firmwareVersion)
    {
        if (!string.IsNullOrWhiteSpace(firmwareVersion))
        {
            FirmwareVersion = firmwareVersion.Trim();
        }
    }

    public void MarkOnline(string? firmwareVersion, DateTimeOffset? at = null)
    {
        ConnectionStatus = DeviceConnectionStatus.Online;
        LastSeenAt = at ?? DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(firmwareVersion))
        {
            FirmwareVersion = firmwareVersion;
        }
    }

    public void MarkOffline()
    {
        if (IsSimulated)
        {
            return;
        }

        ConnectionStatus = DeviceConnectionStatus.Offline;
    }

    /// <summary>
    /// Ativa o dispositivo como simulado (sem firmware). Idempotente.
    /// </summary>
    public void EnableSimulation(string mqttUsername, string mqttSecretHash)
    {
        if (LifecycleStatus is DeviceLifecycleStatus.Deleted or DeviceLifecycleStatus.Suspended)
        {
            throw new InvalidOperationException("Dispositivo não pode ser simulado neste estado.");
        }

        if (HasMqttCredentials && !IsSimulated)
        {
            throw new InvalidOperationException(
                "Dispositivo já ativado com hardware. Não é possível ativar demonstração.");
        }

        IsSimulated = true;

        if (!HasMqttCredentials)
        {
            MqttUsername = mqttUsername;
            MqttSecretHash = mqttSecretHash;
        }

        LifecycleStatus = DeviceLifecycleStatus.Active;
        MarkOnline("sim-1.0.0");
    }

    /// <summary>
    /// Sai do modo demonstração e libera o dispositivo para novo código de instalação.
    /// </summary>
    public void DisableSimulation()
    {
        if (!IsSimulated)
        {
            throw new InvalidOperationException("Dispositivo não está em modo demonstração.");
        }

        IsSimulated = false;
        MqttUsername = null;
        MqttSecretHash = null;

        if (HardwareId is not null &&
            HardwareId.StartsWith("sim-", StringComparison.OrdinalIgnoreCase))
        {
            HardwareId = null;
        }

        if (string.Equals(FirmwareVersion, "sim-1.0.0", StringComparison.Ordinal))
        {
            FirmwareVersion = null;
        }

        LifecycleStatus = DeviceLifecycleStatus.Created;
        ConnectionStatus = DeviceConnectionStatus.Offline;
    }

    public override void SoftDelete(Guid deletedByUserId, DateTimeOffset? at = null)
    {
        base.SoftDelete(deletedByUserId, at);
        LifecycleStatus = DeviceLifecycleStatus.Deleted;
        ConnectionStatus = DeviceConnectionStatus.Offline;
    }
}
