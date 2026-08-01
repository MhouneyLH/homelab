using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Nodes;

namespace HomelabBrain.DeviceSimulator;

// In-memory stand-in for a real gardening device (src/hardware/gardening) - same MQTT
// contract as ConfigCommands.cpp/DeviceConfig.cpp, minus flash persistence and an
// actual reboot. Lets you exercise HomelabBrain.DeviceConfig's REST API end-to-end
// against a fixed, always-on "device" while developing, without real hardware.
internal sealed class SimulatedDevice {
    public string DeviceId { get; }
    public string WifiSsid { get; private set; }
    public string MqttBrokerHost { get; private set; }
    public int MqttBrokerPort { get; private set; }
    public string PlantId { get; private set; }

    public SimulatedDevice(string deviceId, string wifiSsid, string mqttBrokerHost, int mqttBrokerPort, string plantId) {
        DeviceId = deviceId;
        WifiSsid = wifiSsid;
        MqttBrokerHost = mqttBrokerHost;
        MqttBrokerPort = mqttBrokerPort;
        PlantId = plantId;
    }

    public string ConfigTopicPrefix => $"devices/{DeviceId}/config/";

    // Exact topics, not a wildcard on ConfigTopicPrefix - mirrors ConfigCommands.cpp's
    // subscribeConfigCommands(). A wildcard would also match this device's own "<topic>/response"
    // publishes, which start with the same prefix.
    public IReadOnlyList<string> SubscriptionTopics => [
        $"{ConfigTopicPrefix}wifi/set",
        $"{ConfigTopicPrefix}broker/set",
        $"{ConfigTopicPrefix}plant-id/set",
        $"{ConfigTopicPrefix}get",
    ];

    // Mirrors ConfigCommands.cpp's handleWifiSet/handleBrokerSet/handlePlantIdSet/handleGet -
    // same operation names, same response shape. Returns null for unrecognized operations.
    public JsonObject? Handle(string operation, JsonObject request) {
        string correlationId = request["correlationId"]?.GetValue<string>() ?? "";

        return operation switch {
            "wifi/set" => HandleWifiSet(request, correlationId),
            "broker/set" => HandleBrokerSet(request, correlationId),
            "plant-id/set" => HandlePlantIdSet(request, correlationId),
            "get" => HandleGet(correlationId),
            _ => null,
        };
    }

    private JsonObject HandleWifiSet(JsonObject request, string correlationId) {
        string? ssid = request["ssid"]?.GetValue<string>();
        string? password = request["password"]?.GetValue<string>();

        bool ssidValid = !string.IsNullOrEmpty(ssid) && ssid.Length <= 32;
        bool passwordValid = string.IsNullOrEmpty(password) || (password.Length is >= 8 and <= 63);
        if (!ssidValid || !passwordValid)
            return ErrorResponse(correlationId, "invalid payload");

        WifiSsid = ssid!;
        Console.WriteLine($"[simulator] WiFi set to \"{WifiSsid}\" - a real device would reboot now.");
        return OkResponse(correlationId);
    }

    private JsonObject HandleBrokerSet(JsonObject request, string correlationId) {
        string? host = request["host"]?.GetValue<string>();
        int? port = request["port"]?.GetValue<int>();

        if (string.IsNullOrEmpty(host) || port is null or < 1 or > 65535)
            return ErrorResponse(correlationId, "invalid payload");

        MqttBrokerHost = host;
        MqttBrokerPort = port.Value;
        Console.WriteLine($"[simulator] Broker set to {MqttBrokerHost}:{MqttBrokerPort} - a real device would reboot now.");
        return OkResponse(correlationId);
    }

    private JsonObject HandlePlantIdSet(JsonObject request, string correlationId) {
        string? plantId = request["plantId"]?.GetValue<string>();
        bool isValid = !string.IsNullOrEmpty(plantId)
            && plantId.Length <= 32
            && plantId.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

        if (!isValid)
            return ErrorResponse(correlationId, "invalid payload");

        PlantId = plantId!;
        Console.WriteLine($"[simulator] Plant id set to \"{PlantId}\" (applied live).");
        return OkResponse(correlationId);
    }

    private JsonObject HandleGet(string correlationId) =>
        new() {
            ["correlationId"] = correlationId,
            ["status"] = "ok",
            ["wifiSsid"] = WifiSsid,
            ["mqttBrokerHost"] = MqttBrokerHost,
            ["mqttBrokerPort"] = MqttBrokerPort,
            ["plantId"] = PlantId,
        };

    private static JsonObject OkResponse(string correlationId) =>
        new() { ["correlationId"] = correlationId, ["status"] = "ok" };

    private static JsonObject ErrorResponse(string correlationId, string error) =>
        new() { ["correlationId"] = correlationId, ["status"] = "error", ["error"] = error };

    [SuppressMessage("Security", "CA5394", Justification = "Fake sensor noise for local dev, not security-sensitive")]
    public static string BuildSoilMoistureReading() {
        JsonObject reading = new() {
            ["rawValue"] = Random.Shared.Next(0, 1024),
            ["measuredAt"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        };
        return reading.ToJsonString();
    }
}
