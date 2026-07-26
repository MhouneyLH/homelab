using System.Text.Json;
using HomelabBrain.PlantAnalyzer.Contracts;
using HomelabBrain.PlantAnalyzer.Domain;

namespace HomelabBrain.PlantAnalyzer.Application;

internal static class MqttMessageParser {
    // Expected topic format: plants/{plantId}/{sensorType}
    private const string TopicPrefix = "plants";
    private const int SegmentCount = 3;
    private const int PrefixSegment = 0;
    private const int PlantIdSegment = 1;
    private const int SensorTypeSegment = 2;

    private const string SoilMoistureSensorType = "soil-moisture";

    public static MqttMessageResult Parse(string topic, string payload) {
        string[] parts = topic.Split('/');

        if (parts.Length != SegmentCount || parts[PrefixSegment] != TopicPrefix)
            return new MqttMessageResult.UnknownTopic(topic);

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
            new SoilMoistureReading(plantId, receivedAt, dto.ValueInPercent, dto.MeasuredAt));
    }
}
