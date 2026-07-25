using HomelabBrain.PlantAnalyzer.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using OpenTelemetry.Metrics;

namespace HomelabBrain.PlantAnalyzer;

public static class PlantAnalyzerModule
{
    public static IHostApplicationBuilder AddPlantAnalyzer(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<MqttOptions>()
            .BindConfiguration(MqttOptions.SectionName)
            .Configure<IConfiguration>((opts, cfg) =>
            {
                // Aspire injects container endpoint as services__mosquitto__mqtt__0=tcp://host:port
                var aspireEndpoint = cfg["services__mosquitto__mqtt__0"];
                if (aspireEndpoint is not null
                    && Uri.TryCreate(aspireEndpoint, UriKind.Absolute, out var uri))
                {
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

    public static IEndpointRouteBuilder MapPlantAnalyzer(this IEndpointRouteBuilder routes)
    {
        return routes;
    }
}
