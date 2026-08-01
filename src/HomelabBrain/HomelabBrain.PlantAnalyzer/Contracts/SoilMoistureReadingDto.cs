using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HomelabBrain.PlantAnalyzer.Contracts;

public sealed record SoilMoistureReadingDto(
    [property: JsonPropertyName("rawValue")]
    [property: Description("Raw ADC reading from the sensor (0-1023), uncalibrated. Converted " +
        "to a percentage here rather than on-device, so recalibration never requires reflashing.")]
    int RawValue,

    [property: JsonPropertyName("measuredAt")]
    [property: Description("UTC timestamp when the reading was measured.")]
    DateTimeOffset MeasuredAt);
