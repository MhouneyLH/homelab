using System.Text.RegularExpressions;

namespace HomelabBrain.DeviceConfig.Domain;

// Wrapped after TryParse validates it, so it can't be swapped with a
// DeviceId at a call site by accident - both are otherwise plain strings
// with no compiler-visible distinction.
internal readonly partial record struct PlantId {
    // Becomes an MQTT topic segment ("plants/{plantId}/soil-moisture"), so
    // MQTT-special characters (/, +, #) and whitespace are rejected. Mirrors
    // the firmware's own isValidPlantId (DeviceConfig.cpp) - keep in sync.
    [GeneratedRegex(@"^[a-zA-Z0-9-]{1,32}$")]
    private static partial Regex Pattern();

    public string Value { get; }

    private PlantId(string value) => Value = value;

    public static bool TryParse(string? value, out PlantId result) {
        if (value is not null && Pattern().IsMatch(value)) {
            result = new PlantId(value);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value;
}
