using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace HomelabBrain.PlantAnalyzer.Infrastructure;

// Raw ADC readings, not percentages - see SoilMoistureCalibrator. Defaults are the
// manufacturer-ish ballpark from the sensor's README (air ~1023, submerged ~300-400), not a
// calibrated value for any specific physical unit - override per deployment.
[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI (AddOptions)")]
internal sealed class CalibrationOptions {
    public const string SectionName = "SoilMoistureCalibration";

    [Range(0, 1023)]
    public int DryRaw { get; set; } = 1023;

    [Range(0, 1023)]
    public int WetRaw { get; set; } = 400;
}
