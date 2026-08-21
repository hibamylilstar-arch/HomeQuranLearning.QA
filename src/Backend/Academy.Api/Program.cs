using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<DeviceQueryService>();

builder.Services.AddSingleton(builder.Configuration["Storage:Bucket"] ?? "academy-recordings");

builder.Services.AddScoped<RecordingService>(sp =>
{
    var recordingRepository = sp.GetRequiredService<IRecordingRepository>();
    var deviceRepository = sp.GetRequiredService<IDeviceRepository>();
    var storageService = sp.GetRequiredService<IStorageService>();
    var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
    var bucketName = sp.GetRequiredService<string>();

    return new RecordingService(
        recordingRepository,
        deviceRepository,
        storageService,
        unitOfWork,
        bucketName);
});

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

app.MapPost("/api/agent/recordings/{recordingId:guid}/upload", async (
    HttpRequest request,
    Guid recordingId,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != agentApiKey)
    {
        return Results.Unauthorized();
    }

    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Expected multipart/form-data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.FirstOrDefault();

    if (file is null)
    {
        return Results.BadRequest("No file uploaded.");
    }

    await using var stream = file.OpenReadStream();

    await recordingService.UploadRecordingAsync(
        recordingId,
        stream,
        file.ContentType,
        cancellationToken);

    return Results.Ok(new { uploaded = true });
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

    var recordings = await recordingService.GetRecordingListAsync(cancellationToken);
    return Results.Ok(recordings);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();