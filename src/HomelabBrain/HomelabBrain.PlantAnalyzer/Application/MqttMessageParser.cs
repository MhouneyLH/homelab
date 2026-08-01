using System.Text.Json;
using HomelabBrain.PlantAnalyzer.Contracts;
using HomelabBrain.PlantAnalyzer.Domain;

namespace HomelabBrain.PlantAnalyzer.Application;

internal static class MqttMessageParser {
    // Expected topic format: devices/{deviceId}/plants/{plantId}/{sensorType} - deviceId leads
    // (stable chip-id), plantId nested underneath it (mutable business label), matching
    // DeviceConfig's addressing convention.
    private const string TopicPrefix = "devices";
    private const string PlantsSegmentValue = "plants";
    private const int SegmentCount = 5;
    private const int PrefixSegment = 0;
    private const int DeviceIdSegment = 1;
    private const int PlantsSegment = 2;
    private const int PlantIdSegment = 3;
    private const int SensorTypeSegment = 4;

    private const string SoilMoistureSensorType = "soil-moisture";

    public static MqttMessageResult Parse(string topic, string payload) {
        string[] parts = topic.Split('/');

        if (parts.Length != SegmentCount
            || parts[PrefixSegment] != TopicPrefix
            || parts[PlantsSegment] != PlantsSegmentValue) {
            return new MqttMessageResult.UnknownTopic(topic);
        }

        string deviceId = parts[DeviceIdSegment];
        string plantId = parts[PlantIdSegment];
        string sensorType = parts[SensorTypeSegment];

        if (sensorType != SoilMoistureSensorType)
            return new MqttMessageResult.UnknownTopic(topic);

        SoilMoistureReadingDto? dto;
        try {
            dto = JsonSerializer.Deserialize<SoilMoistureReadingDto>(payload);
        } catch (JsonException) {
            return new MqttMessageResult.InvalidPayload(topic, payload);
        }

        if (dto is null)
            return new MqttMessageResult.InvalidPayload(topic, payload);

        DateTimeOffset receivedAt = DateTimeOffset.UtcNow;

        return new MqttMessageResult.Valid(
            new SoilMoistureReading(deviceId, plantId, receivedAt, dto.RawValue, dto.MeasuredAt));
    }
}
