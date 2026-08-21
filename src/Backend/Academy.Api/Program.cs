using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<DeviceQueryService>();
builder.Services.AddScoped<RecordingService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

string agentApiKey = app.Configuration["AgentApiKey"] ?? string.Empty;
string adminApiKey = app.Configuration["AdminApiKey"] ?? string.Empty;

app.MapPost("/api/agent/heartbeat", async (
    HttpRequest request,
    HeartbeatRequest body,
    DeviceService deviceService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != agentApiKey)
    {
        return Results.Unauthorized();
    }

    var response = await deviceService.ProcessHeartbeatAsync(body, cancellationToken);
    return Results.Ok(response);
});

app.MapPost("/api/agent/recordings", async (
    HttpRequest request,
    RecordingSubmittedRequest body,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != agentApiKey)
    {
        return Results.Unauthorized();
    }

    var response = await recordingService.SubmitRecordingAsync(body, cancellationToken);
    return Results.Ok(response);
});

app.MapGet("/api/admin/devices", async (
    HttpRequest request,
    DeviceQueryService deviceQueryService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != adminApiKey)
    {
        return Results.Unauthorized();
    }

    var devices = await deviceQueryService.GetDevicesAsync(cancellationToken);
    return Results.Ok(devices);
});

app.MapGet("/api/admin/recordings", async (
    HttpRequest request,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != adminApiKey)
    {
        return Results.Unauthorized();
    }

    var recordings = await recordingService.GetRecordingsAsync(cancellationToken);
    return Results.Ok(recordings);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();