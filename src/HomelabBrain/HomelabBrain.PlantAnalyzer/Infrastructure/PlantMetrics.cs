using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using HomelabBrain.PlantAnalyzer.Application;
using HomelabBrain.PlantAnalyzer.Domain;
using Microsoft.Extensions.Options;

namespace HomelabBrain.PlantAnalyzer.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI (AddSingleton)")]
internal sealed class PlantMetrics : IDisposable {
    internal const string MeterName = "HomelabBrain.PlantAnalyzer";

    private readonly Meter _meter;
    private readonly Gauge<double> _soilMoisture;
    private readonly Gauge<int> _soilMoistureRaw;
    private readonly Counter<long> _messagesReceived;
    private readonly Counter<long> _invalidMessages;
    private readonly CalibrationOptions _calibration;

    public PlantMetrics(IMeterFactory meterFactory, IOptions<CalibrationOptions> calibration) {
        _meter = meterFactory.Create(MeterName);
        _calibration = calibration.Value;

        // Gauge, not Histogram: this is the current value of a sensor, not a
        // distribution to bucket - a Histogram would report an approximated
        // bucket boundary instead of the exact recorded value.
        _soilMoisture = _meter.CreateGauge<double>(
            "plants.soil_moisture",
            unit: "%",
            description: "Soil moisture sensor reading, calibrated to a percentage (see CalibrationOptions)");

        _soilMoistureRaw = _meter.CreateGauge<int>(
            "plants.soil_moisture.raw",
            unit: "{adc_reading}",
            description: "Uncalibrated raw ADC reading from the sensor (0-1023)");

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
        if (reading is not SoilMoistureReading soilMoisture)
            throw new NotSupportedException($"Unsupported sensor reading type '{reading.GetType()}'.");

        TagList tags = new() {
            { "device.id", reading.DeviceId },
            { "plant.id", reading.PlantId },
            { "sensor.type", reading.SensorType },
        };

        _soilMoistureRaw.Record(soilMoisture.RawValue, tags);
        _soilMoisture.Record(SoilMoistureCalibrator.ToPercent(soilMoisture.RawValue, _calibration), tags);
    }

    public void IncrementReceived() => _messagesReceived.Add(1);

    public void IncrementInvalid(string errorType) =>
        _invalidMessages.Add(1, new TagList { { "error.type", errorType } });

    public void Dispose() => _meter.Dispose();
}
