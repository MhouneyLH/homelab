namespace HomelabBrain.PlantAnalyzer.Domain;

internal abstract record SensorReading(string PlantId, string SensorType, DateTimeOffset Timestamp);

internal sealed record SoilMoistureReading(
    string PlantId,
    DateTimeOffset Timestamp,
    double ValueInPercent) : SensorReading(PlantId, "soil-moisture", Timestamp);

// Fallback for sensor types without a dedicated reading type yet.
internal sealed record GenericSensorReading(
    string PlantId,
    string SensorType,
    DateTimeOffset Timestamp,
    double Value) : SensorReading(PlantId, SensorType, Timestamp);
