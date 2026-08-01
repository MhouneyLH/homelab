using HomelabBrain.DeviceConfig.Endpoints;
using HomelabBrain.DeviceConfig.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;

namespace HomelabBrain.DeviceConfig;

public static class DeviceConfigModule {
    public static IHostApplicationBuilder AddDeviceConfig(this IHostApplicationBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<MqttOptions>()
            .BindConfiguration(MqttOptions.SectionName)
            .Configure<IConfiguration>((opts, cfg) => {
                // Same Aspire-managed broker as PlantAnalyzer - see its
                // AddPlantAnalyzer for why this env var key is used.
                string? aspireEndpoint = cfg["services:mosquitto:mqtt:0"];
                if (aspireEndpoint is not null
                    && Uri.TryCreate(aspireEndpoint, UriKind.Absolute, out Uri? uri)) {
                    opts.BrokerHost = uri.Host;
                    opts.BrokerPort = uri.Port;
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IManagedMqttClient>(
            _ => new MqttFactory().CreateManagedMqttClient());

        builder.Services.AddSingleton<PendingCommandRegistry>();
        builder.Services.AddSingleton<DeviceConfigCommandService>();
        builder.Services.AddHostedService<DeviceConfigMqttConsumer>();

        return builder;
    }

    public static IEndpointRouteBuilder MapDeviceConfig(this IEndpointRouteBuilder routes) {
        RouteGroupBuilder group = routes
            .MapGroup("/api/devices/{deviceId}/config")
            .WithTags("DeviceConfig");

        group.MapPost("/wifi", SetWifiEndpoint.Handle)
            .WithName("SetDeviceWifi")
            .WithSummary("Set the device's WiFi credentials (reboots the device on success)")
            .Produces<SetWifiEndpoint.Response>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        group.MapPost("/broker", SetBrokerEndpoint.Handle)
            .WithName("SetDeviceBroker")
            .WithSummary("Set the device's MQTT broker (reboots the device on success)")
            .Produces<SetBrokerEndpoint.Response>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return routes;
    }
}
