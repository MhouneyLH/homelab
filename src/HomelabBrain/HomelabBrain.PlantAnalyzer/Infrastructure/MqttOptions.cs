using System.ComponentModel.DataAnnotations;

namespace HomelabBrain.PlantAnalyzer.Infrastructure;

internal sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    [Required]
    public string BrokerHost { get; init; } = default!;

    [Range(1, 65535)]
    public int BrokerPort { get; init; } = 1883;

    public string ClientId { get; init; } = "homelab-brain-plant-analyzer";
}
