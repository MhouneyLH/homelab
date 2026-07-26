namespace HomelabBrain.PlantAnalyzer.Domain;

internal abstract record SensorReading(string PlantId, string SensorType, DateTimeOffset ReceivedAt);

internal sealed record SoilMoistureReading(
    string PlantId,
    DateTimeOffset ReceivedAt,
    double ValueInPercent,
    DateTimeOffset MeasuredAt) : SensorReading(PlantId, "soil-moisture", ReceivedAt);
