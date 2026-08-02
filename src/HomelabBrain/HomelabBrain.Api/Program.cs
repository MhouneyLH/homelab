using HomelabBrain.DeviceConfig;
using HomelabBrain.PlantAnalyzer;
using HomelabBrain.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPlantAnalyzer();
builder.AddDeviceConfig();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Not gated to Development: this is an internal-only service (no public ingress/TLS, see
// src/k8s/apps/services/homelab-brain), so there's no exposure risk in always serving docs.
app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();
app.MapPlantAnalyzer();
app.MapDeviceConfig();
app.Run();
