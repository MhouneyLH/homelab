using System.Text.Json;
using HomelabBrain.DeviceConfig.Domain;
using Microsoft.AspNetCore.Http;

namespace HomelabBrain.DeviceConfig.Endpoints;

internal static class ConfigCommandResults {
    public static IResult ToApiResult(this ConfigCommandOutcome outcome, Func<JsonElement, IResult> onSuccess) {
        switch (outcome) {
            case ConfigCommandOutcome.Success success:
                JsonElement payload = success.Payload;
                if (payload.TryGetProperty("status", out JsonElement status)
                    && status.ValueKind == JsonValueKind.String
                    && status.GetString() == "error") {
                    string? error = payload.TryGetProperty("error", out JsonElement errorElement)
                        ? errorElement.GetString()
                        : null;
                    return Results.Problem(
                        statusCode: StatusCodes.Status502BadGateway,
                        title: "Device rejected command",
                        detail: error);
                }
                return onSuccess(payload);

            case ConfigCommandOutcome.TimedOut:
                return Results.Problem(
                    statusCode: StatusCodes.Status504GatewayTimeout,
                    title: "Device did not respond in time");

            default:
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
