using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using HomelabBrain.PlantAnalyzer.Domain;

namespace HomelabBrain.PlantAnalyzer.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI (AddSingleton)")]
internal sealed class PlantMetrics : IDisposable {
    internal const string MeterName = "HomelabBrain.PlantAnalyzer";

    private readonly Meter _meter;
    private readonly Gauge<double> _soilMoisture;
    private readonly Counter<long> _messagesReceived;
    private readonly Counter<long> _invalidMessages;

    public PlantMetrics(IMeterFactory meterFactory) {
        _meter = meterFactory.Create(MeterName);

        // Gauge, not Histogram: this is the current value of a sensor, not a
        // distribution to bucket - a Histogram would report an approximated
        // bucket boundary instead of the exact recorded value.
        _soilMoisture = _meter.CreateGauge<double>(
            "plants.soil_moisture",
            unit: "%",
            description: "Soil moisture sensor reading");

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
        (Gauge<double> gauge, double value) = reading switch {
            SoilMoistureReading soilMoisture => (_soilMoisture, soilMoisture.ValueInPercent),
            _ => throw new NotSupportedException($"Unsupported sensor reading type '{reading.GetType()}'."),
        };

        gauge.Record(
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
