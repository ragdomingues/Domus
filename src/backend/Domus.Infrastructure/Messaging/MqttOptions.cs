namespace Domus.Infrastructure.Messaging;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public bool UseTls { get; set; }
    public string ClientId { get; set; } = "domus-api";
    public string Username { get; set; } = "domus_api";
    public string Password { get; set; } = "domus_api_dev_password";
    public string HookSecret { get; set; } = "domus_mqtt_hook_dev_secret";
}
