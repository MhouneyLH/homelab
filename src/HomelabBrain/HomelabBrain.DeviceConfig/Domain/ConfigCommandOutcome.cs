using System.Text.Json;

namespace HomelabBrain.DeviceConfig.Domain;

internal abstract record ConfigCommandOutcome {
    internal sealed record Success(JsonElement Payload) : ConfigCommandOutcome;

    internal sealed record TimedOut : ConfigCommandOutcome;
}
