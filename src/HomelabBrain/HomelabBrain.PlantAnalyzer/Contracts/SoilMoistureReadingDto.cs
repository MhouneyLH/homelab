using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HomelabBrain.PlantAnalyzer.Contracts;

public sealed record SoilMoistureReadingDto(
    [property: JsonPropertyName("valueInPercent")]
    [property: Description("Soil moisture reading, expressed as a percentage (0-100).")]
    double ValueInPercent,

    [property: JsonPropertyName("measuredAt")]
    [property: Description("UTC timestamp when the reading was measured.")]
    DateTimeOffset MeasuredAt);
