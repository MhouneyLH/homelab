using System.Diagnostics.Metrics;
using HomelabBrain.PlantAnalyzer.Domain;

namespace HomelabBrain.PlantAnalyzer.Infrastructure;

internal sealed class PlantMetrics : IDisposable
{
    internal const string MeterName = "HomelabBrain.PlantAnalyzer";

    private readonly Meter _meter;
    private readonly Histogram<double> _sensorReadings;
    private readonly Counter<long> _messagesReceived;
    private readonly Counter<long> _invalidMessages;

    public PlantMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _sensorReadings = _meter.CreateHistogram<double>(
            "plants.sensor.reading",
            unit: "{reading}",
            description: "Plant sensor reading value");

        _messagesReceived = _meter.CreateCounter<long>(
            "plants.mqtt.messages.received",
            unit: "{message}",
            description: "Total MQTT messages received");

        _invalidMessages = _meter.CreateCounter<long>(
            "plants.mqtt.messages.invalid",
            unit: "{message}",
            description: "MQTT messages that failed parsing");
    }

    public void RecordSensorReading(SensorReading reading) =>
        _sensorReadings.Record(
            reading.Value,
            new TagList
            {
                { "plant.id", reading.PlantId },
                { "sensor.type", reading.SensorType },
            });

    public void IncrementReceived() => _messagesReceived.Add(1);

    public void IncrementInvalid(string reason) =>
        _invalidMessages.Add(1, new TagList { { "reason", reason } });

    public void Dispose() => _meter.Dispose();
}
