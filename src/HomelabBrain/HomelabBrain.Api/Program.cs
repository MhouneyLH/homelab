using HomelabBrain.PlantAnalyzer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPlantAnalyzer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapPlantAnalyzer();
app.Run();
