using System.Globalization;
using HomelabBrain.PlantAnalyzer.Domain;

namespace HomelabBrain.PlantAnalyzer.Application;

internal static class MqttMessageParser
{
    // Expected topic format: plants/{plantId}/{sensorType}
    public static MqttMessageResult Parse(string topic, string payload)
    {
        var parts = topic.Split('/');

        if (parts.Length != 3 || parts[0] != "plants")
            return new MqttMessageResult.UnknownTopic(topic);

        if (!double.TryParse(payload, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return new MqttMessageResult.InvalidPayload(topic, payload);

        return new MqttMessageResult.Valid(new SensorReading(
            PlantId: parts[1],
            SensorType: parts[2],
            Value: value,
            Timestamp: DateTimeOffset.UtcNow));
    }
}
