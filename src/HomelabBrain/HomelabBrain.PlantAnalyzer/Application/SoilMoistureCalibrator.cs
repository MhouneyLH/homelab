using HomelabBrain.PlantAnalyzer.Infrastructure;

namespace HomelabBrain.PlantAnalyzer.Application;

// Converts a raw ADC reading to a moisture percentage. Kept separate from the raw value itself
// (SensorReading) rather than done on-device, so recalibrating a drifting/replaced sensor is an
// appsettings change, not a reflash - see CalibrationOptions.
internal static class SoilMoistureCalibrator {
    public static double ToPercent(int rawValue, CalibrationOptions calibration) {
        int span = calibration.DryRaw - calibration.WetRaw;
        if (span == 0)
            return 0;

        double percent = (calibration.DryRaw - rawValue) / (double)span * 100;
        return Math.Clamp(percent, 0, 100);
    }
}
