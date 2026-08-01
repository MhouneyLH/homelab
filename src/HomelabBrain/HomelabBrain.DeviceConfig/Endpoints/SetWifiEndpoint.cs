using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using HomelabBrain.DeviceConfig.Domain;
using HomelabBrain.DeviceConfig.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace HomelabBrain.DeviceConfig.Endpoints;

internal static class SetWifiEndpoint {
    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by minimal API model binding")]
    internal sealed record SetWifiRequest(
        [property: JsonPropertyName("ssid")] string Ssid,
        [property: JsonPropertyName("password")] string Password);

    internal sealed record SetWifiResponse(
        [property: JsonPropertyName("status")] string Status);

    public static async Task<IResult> Handle(
        DeviceId deviceId,
        SetWifiRequest request,
        DeviceConfigCommandService commandService,
        CancellationToken ct) {
        Dictionary<string, string[]> errors = Validate(request);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        Dictionary<string, object?> payload = new() {
            ["ssid"] = request.Ssid,
            ["password"] = request.Password,
        };

        ConfigCommandOutcome outcome = await commandService
            .SendAsync(deviceId, "wifi/set", payload, ct)
            .ConfigureAwait(false);

        return outcome.ToApiResult(_ => Results.Ok(new SetWifiResponse("ok")));
    }

    private static Dictionary<string, string[]> Validate(SetWifiRequest request) {
        Dictionary<string, string[]> errors = [];

        if (string.IsNullOrWhiteSpace(request.Ssid))
            errors["ssid"] = ["ssid is required."];
        else if (request.Ssid.Length > 32)
            errors["ssid"] = ["ssid must be at most 32 characters."];

        if (!string.IsNullOrEmpty(request.Password) && request.Password.Length is < 8 or > 63)
            errors["password"] = ["password must be empty (open network) or 8-63 characters (WPA2)."];

        return errors;
    }
}
