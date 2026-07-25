var builder = DistributedApplication.CreateBuilder(args);

var mosquitto = builder.AddContainer("mosquitto", "eclipse-mosquitto")
    .WithEndpoint(port: 1883, targetPort: 1883, name: "mqtt")
    .WithEndpoint(port: 9001, targetPort: 9001, name: "websocket");

var otelCollector = builder.AddContainer("otel-collector", "otel/opentelemetry-collector-contrib")
    .WithBindMount("./otel-collector-config.yaml", "/etc/otelcol-contrib/config.yaml")
    .WithEndpoint(port: 4317, targetPort: 4317, name: "otlp-grpc")
    .WithEndpoint(port: 4318, targetPort: 4318, name: "otlp-http");

builder.AddProject<Projects.HomelabBrain_Api>("api")
    .WithReference(mosquitto.GetEndpoint("mqtt"))
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otelCollector.GetEndpoint("otlp-grpc"))
    .WaitFor(mosquitto)
    .WaitFor(otelCollector);

builder.Build().Run();
