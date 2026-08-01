using System.Text.Json.Serialization;
using HomelabBrain.DeviceConfig.Domain;
using HomelabBrain.DeviceConfig.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace HomelabBrain.DeviceConfig.Endpoints;

internal static class GetConfigEndpoint {
    internal sealed record GetConfigResponse(
        [property: JsonPropertyName("wifiSsid")] string WifiSsid,
        [property: JsonPropertyName("mqttBrokerHost")] string MqttBrokerHost,
        [property: JsonPropertyName("mqttBrokerPort")] int MqttBrokerPort,
        [property: JsonPropertyName("plantId")] string PlantId);

    public static async Task<IResult> Handle(
        DeviceId deviceId,
        DeviceConfigCommandService commandService,
        CancellationToken ct) {
        ConfigCommandOutcome outcome = await commandService
            .SendAsync(deviceId, "get", new Dictionary<string, object?>(), ct)
            .ConfigureAwait(false);

        return outcome.ToApiResult(payload => Results.Ok(new GetConfigResponse(
            payload.GetProperty("wifiSsid").GetString() ?? string.Empty,
            payload.GetProperty("mqttBrokerHost").GetString() ?? string.Empty,
            payload.GetProperty("mqttBrokerPort").GetInt32(),
            payload.GetProperty("plantId").GetString() ?? string.Empty)));
    }
}
