namespace HomelabBrain.PlantAnalyzer.Domain;

public sealed record SensorReading(
    string PlantId,
    string SensorType,
    double Value,
    DateTimeOffset Timestamp);
