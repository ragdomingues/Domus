namespace Domus.Application.Devices;

public static class MqttTopics
{
    public static string Command(Guid tenantId, Guid deviceId) => $"domus/{tenantId:D}/{deviceId:D}/command";
    public static string Status(Guid tenantId, Guid deviceId) => $"domus/{tenantId:D}/{deviceId:D}/status";
    public static string Heartbeat(Guid tenantId, Guid deviceId) => $"domus/{tenantId:D}/{deviceId:D}/heartbeat";
    public static string Config(Guid tenantId, Guid deviceId) => $"domus/{tenantId:D}/{deviceId:D}/config";

    public const string StatusWildcard = "domus/+/+/status";
    public const string HeartbeatWildcard = "domus/+/+/heartbeat";

    public static bool TryParse(string topic, out Guid tenantId, out Guid deviceId, out string leaf)
    {
        tenantId = Guid.Empty;
        deviceId = Guid.Empty;
        leaf = string.Empty;

        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], "domus", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Guid.TryParse(parts[1], out tenantId) || !Guid.TryParse(parts[2], out deviceId))
        {
            return false;
        }

        leaf = parts[3];
        return true;
    }
}
