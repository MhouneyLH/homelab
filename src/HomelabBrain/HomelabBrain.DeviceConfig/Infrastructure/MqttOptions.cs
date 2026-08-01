using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace HomelabBrain.DeviceConfig.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI (AddOptions)")]
internal sealed class MqttOptions {
    public const string SectionName = "DeviceConfigMqtt";

    [Required]
    public string BrokerHost { get; set; } = default!;

    [Range(1, 65535)]
    public int BrokerPort { get; set; } = 1883;

    public string ClientId { get; set; } = "homelab-brain-device-config";

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 10;
}
