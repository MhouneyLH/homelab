using HomelabBrain.DeviceConfig;
using HomelabBrain.PlantAnalyzer;
using HomelabBrain.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPlantAnalyzer();
builder.AddDeviceConfig();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapPlantAnalyzer();
app.MapDeviceConfig();
app.Run();
