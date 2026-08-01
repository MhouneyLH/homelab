namespace HomelabBrain.PlantAnalyzer.Domain;

internal abstract record SensorReading(string DeviceId, string PlantId, string SensorType, DateTimeOffset ReceivedAt);

internal sealed record SoilMoistureReading(
    string DeviceId,
    string PlantId,
    DateTimeOffset ReceivedAt,
    int RawValue,
    DateTimeOffset MeasuredAt) : SensorReading(DeviceId, PlantId, "soil-moisture", ReceivedAt);
