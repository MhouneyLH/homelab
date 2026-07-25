using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using HomelabBrain.PlantAnalyzer.Domain;

namespace HomelabBrain.PlantAnalyzer.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI (AddSingleton)")]
internal sealed class PlantMetrics : IDisposable {
    internal const string MeterName = "HomelabBrain.PlantAnalyzer";

    private readonly Meter _meter;
    private readonly Histogram<double> _soilMoistureReading;
    private readonly Histogram<double> _genericSensorReading;
    private readonly Counter<long> _messagesReceived;
    private readonly Counter<long> _invalidMessages;

    public PlantMetrics(IMeterFactory meterFactory) {
        _meter = meterFactory.Create(MeterName);

        _soilMoistureReading = _meter.CreateHistogram<double>(
            "plants.soil_moisture.reading",
            unit: "%",
            description: "Soil moisture sensor reading");

        // Sensor types without a dedicated SensorReading subtype (and
        // dedicated instrument) yet land here instead of being dropped.
        _genericSensorReading = _meter.CreateHistogram<double>(
            "plants.sensor.reading",
            unit: "1",
            description: "Plant sensor reading for sensor types without a dedicated metric yet");

        _messagesReceived = _meter.CreateCounter<long>(
            "plants.mqtt.messages.received",
            unit: "{message}",
            description: "Total MQTT messages received");

        _invalidMessages = _meter.CreateCounter<long>(
            "plants.mqtt.messages.invalid",
            unit: "{message}",
            description: "MQTT messages that failed parsing");
    }

    public void RecordSensorReading(SensorReading reading) {
        (Histogram<double> histogram, double value) = reading switch {
            SoilMoistureReading soilMoisture => (_soilMoistureReading, soilMoisture.ValueInPercent),
            GenericSensorReading generic => (_genericSensorReading, generic.Value),
            _ => throw new NotSupportedException($"Unsupported sensor reading type '{reading.GetType()}'."),
        };

        histogram.Record(
            value,
            new TagList {
                { "plant.id", reading.PlantId },
                { "sensor.type", reading.SensorType },
            });
    }

    public void IncrementReceived() => _messagesReceived.Add(1);

    public void IncrementInvalid(string errorType) =>
        _invalidMessages.Add(1, new TagList { { "error.type", errorType } });

    public void Dispose() => _meter.Dispose();
}
