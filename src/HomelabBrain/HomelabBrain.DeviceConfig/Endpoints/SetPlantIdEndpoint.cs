using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using HomelabBrain.DeviceConfig.Domain;
using HomelabBrain.DeviceConfig.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace HomelabBrain.DeviceConfig.Endpoints;

internal static class SetPlantIdEndpoint {
    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by minimal API model binding")]
    internal sealed record SetPlantIdRequest(
        [property: JsonPropertyName("plantId")] string PlantId);

    internal sealed record SetPlantIdResponse(
        [property: JsonPropertyName("status")] string Status);

    public static async Task<IResult> Handle(
        DeviceId deviceId,
        SetPlantIdRequest request,
        DeviceConfigCommandService commandService,
        CancellationToken ct) {
        if (!PlantId.TryParse(request.PlantId, out PlantId plantId)) {
            return Results.ValidationProblem(new Dictionary<string, string[]> {
                ["plantId"] = ["plantId must be 1-32 characters of letters, digits, or hyphens."],
            });
        }

        Dictionary<string, object?> payload = new() {
            ["plantId"] = plantId.Value,
        };

        ConfigCommandOutcome outcome = await commandService
            .SendAsync(deviceId, "plant-id/set", payload, ct)
            .ConfigureAwait(false);

        return outcome.ToApiResult(_ => Results.Ok(new SetPlantIdResponse("ok")));
    }
}
