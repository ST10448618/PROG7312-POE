using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json.Serialization;
using SmartX.Api.Domain.Collections;
using SmartX.Api.Endpoints;
using SmartX.Api.Hubs;
using SmartX.Api.Infrastructure.Data;
using SmartX.Api.Infrastructure.FileStorage;
using SmartX.Api.Infrastructure.Seeding;
using SmartX.Api.Middleware;
using SmartX.Api.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!);

var clientOrigin = builder.Configuration["ClientOrigin"] ?? "http://localhost:8081";
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(clientOrigin).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddSingleton<AnomalyDetectionService>();
builder.Services.AddSingleton<TelemetryBatchStore>();
builder.Services.AddSingleton<IFileStorageService, EncryptedFileStorageService>();
builder.Services.AddScoped<SensorService>();
builder.Services.AddScoped<TelemetryIngestionService>();
builder.Services.AddScoped<DeploymentTreeService>();
builder.Services.AddHostedService<MockTelemetrySeeder>();
builder.Services.AddScoped<IntegrationService>();
builder.Services.AddScoped<IntegrationDispatchService>();
builder.Services.AddScoped<AnomalyLogService>();
builder.Services.AddSingleton<ConnectionTracker>();
builder.Services.AddScoped<SystemHealthService>();
builder.Services.AddHostedService<AutoTelemetrySimulator>();
builder.Services.AddSingleton<LiveDeviceRegistry>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var registry = scope.ServiceProvider.GetRequiredService<LiveDeviceRegistry>();
    var existingSensors = db.Sensors.ToList();
    foreach (var sensor in existingSensors)
    registry.Register(sensor.MacAddress, sensor.Location, sensor.Category);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapSensorEndpoints();
app.MapTelemetryEndpoints();
app.MapPowerEndpoints();
app.MapDeploymentEndpoints();
app.MapHub<TelemetryHub>("/hubs/telemetry");
app.MapAnomalyLogEndpoints();
app.MapIntegrationEndpoints();
app.MapSystemHealthEndpoints();

app.Run();