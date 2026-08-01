using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace HomelabBrain.DeviceConfig.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI (AddOptions)")]
internal sealed class MqttOptions {
    public const string SectionName = "DeviceConfigMqtt";

    // Overridden by AddDeviceConfig's Aspire endpoint lookup (or a real
    // appsettings override) outside local dev; "localhost" is a working
    // default so the app - and build-time OpenAPI doc generation, which
    // boots the real host - never fails on missing config.
    [Required]
    public string BrokerHost { get; set; } = "localhost";

    [Range(1, 65535)]
    public int BrokerPort { get; set; } = 1883;

    public string ClientId { get; set; } = "homelab-brain-device-config";

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 10;
}
