using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using HomelabBrain.DeviceConfig.Domain;
using HomelabBrain.DeviceConfig.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace HomelabBrain.DeviceConfig.Endpoints;

internal static class SetBrokerEndpoint {
    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by minimal API model binding")]
    internal sealed record Request(
        [property: JsonPropertyName("host")] string Host,
        [property: JsonPropertyName("port")] int Port);

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

        Dictionary<string, object?> payload = new() {
            ["host"] = request.Host,
            ["port"] = request.Port,
        };

        ConfigCommandOutcome outcome = await commandService
            .SendAsync(deviceId, "broker/set", payload, ct)
            .ConfigureAwait(false);

        return outcome.ToApiResult(_ => Results.Ok(new Response("ok")));
    }

    private static Dictionary<string, string[]> Validate(Request request) {
        Dictionary<string, string[]> errors = [];

        if (string.IsNullOrWhiteSpace(request.Host))
            errors["host"] = ["host is required."];
        else if (request.Host.Length > 253 || Uri.CheckHostName(request.Host) == UriHostNameType.Unknown)
            errors["host"] = ["host must be a valid hostname or IP address."];

        if (request.Port is < 1 or > 65535)
            errors["port"] = ["port must be between 1 and 65535."];

        return errors;
    }
}
