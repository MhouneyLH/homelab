var builder = DistributedApplication.CreateBuilder(args);

var mosquitto = builder.AddContainer("mosquitto", "eclipse-mosquitto")
    .WithEndpoint(port: 1883, targetPort: 1883, name: "mqtt")
    .WithEndpoint(port: 9001, targetPort: 9001, name: "websocket");

builder.AddProject<Projects.HomelabBrain_Api>("api")
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReference(mosquitto.GetEndpoint("mqtt"))
    .WaitFor(mosquitto);

builder.Build().Run();
