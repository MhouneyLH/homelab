using HomelabBrain.DeviceConfig.Infrastructure;
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

    public static IEndpointRouteBuilder MapDeviceConfig(this IEndpointRouteBuilder routes) => routes;
}
