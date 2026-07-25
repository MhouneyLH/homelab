using HomelabBrain.PlantAnalyzer.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;

namespace HomelabBrain.PlantAnalyzer;

public static class PlantAnalyzerModule {
    public static IHostApplicationBuilder AddPlantAnalyzer(this IHostApplicationBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<MqttOptions>()
            .BindConfiguration(MqttOptions.SectionName)
            .Configure<IConfiguration>((opts, cfg) => {
                // Env var services__mosquitto__mqtt__0 is normalized by the
                // configuration provider to "services:mosquitto:mqtt:0" (double
                // underscore -> colon), so look it up with colons.
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

        builder.Services.AddSingleton<PlantMetrics>();
        builder.Services.AddHostedService<MqttConsumerService>();

        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m.AddMeter(PlantMetrics.MeterName));

        return builder;
    }

    public static IEndpointRouteBuilder MapPlantAnalyzer(this IEndpointRouteBuilder routes) => routes;
}
