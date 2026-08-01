using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HomelabBrain.DeviceConfig.Domain;
using HomelabBrain.DeviceConfig.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace HomelabBrain.DeviceConfig.Endpoints;

internal static partial class SetPlantIdEndpoint {
    // Becomes an MQTT topic segment ("plants/{plantId}/soil-moisture"), so
    // MQTT-special characters (/, +, #) and whitespace are rejected.
    [GeneratedRegex(@"^[a-zA-Z0-9-]{1,32}$")]
    private static partial Regex PlantIdPattern();

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by minimal API model binding")]
    internal sealed record Request(
        [property: JsonPropertyName("plantId")] string PlantId);

    internal sealed record Response(
        [property: JsonPropertyName("status")] string Status);

    public static async Task<IResult> Handle(
        DeviceId deviceId,
        Request request,
        DeviceConfigCommandService commandService,
        CancellationToken ct) {
        Dictionary<string, string[]> errors = Validate(request);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        PlantId plantId = new(request.PlantId);

        Dictionary<string, object?> payload = new() {
            ["plantId"] = plantId.Value,
        };

        ConfigCommandOutcome outcome = await commandService
            .SendAsync(deviceId, "plant-id/set", payload, ct)
            .ConfigureAwait(false);

        return outcome.ToApiResult(_ => Results.Ok(new Response("ok")));
    }

    private static Dictionary<string, string[]> Validate(Request request) {
        Dictionary<string, string[]> errors = [];

        if (string.IsNullOrWhiteSpace(request.PlantId) || !PlantIdPattern().IsMatch(request.PlantId))
            errors["plantId"] = ["plantId must be 1-32 characters of letters, digits, or hyphens."];

        return errors;
    }
}
