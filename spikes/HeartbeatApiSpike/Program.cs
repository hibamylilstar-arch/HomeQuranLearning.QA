using Academy.Agent.Cloud;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapPost("/api/agent/heartbeat", (HttpRequest request, HeartbeatRequest body) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var apiKey) || apiKey != "test-key")
    {
        return Results.Unauthorized();
    }

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Heartbeat received from {body.DeviceName} ({body.DeviceId}) Status={body.Status}");

    return Results.Ok(new HeartbeatResponse
    {
        Received = true,
        Command = null,
        SessionId = null
    });
});

app.Run();