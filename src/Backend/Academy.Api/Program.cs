using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Options;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.DependencyInjection;
using Academy.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<DeviceQueryService>();
builder.Services.AddScoped<QaRuleService>();
builder.Services.AddScoped<QaAlertService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<TeacherService>();
builder.Services.AddScoped<ManagerAssignmentService>();

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

// Authentication / JWT
var jwtOptions = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardCors", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("DashboardCors");
app.UseAuthentication();
app.UseAuthorization();

string agentApiKey = app.Configuration["AgentApiKey"] ?? string.Empty;
string workerApiKey = app.Configuration["WorkerApiKey"] ?? string.Empty;

var jsonOptions = new System.Text.Json.JsonSerializerOptions(
    System.Text.Json.JsonSerializerDefaults.Web);
jsonOptions.Converters.Add(new JsonStringEnumConverter());

// Seed owner
await SeedOwnerAsync(app);

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    AuthService authService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        return Results.Ok(response);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Json(new { message = ex.Message }, statusCode: 401);
    }
});

app.MapGet("/api/auth/me", async (
    ClaimsPrincipal user,
    AuthService authService,
    CancellationToken cancellationToken) =>
{
    string? userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userIdValue is null || !Guid.TryParse(userIdValue, out Guid userId))
    {
        return Results.Unauthorized();
    }

    var profile = await authService.GetCurrentUserAsync(userId, cancellationToken);
    return Results.Ok(profile);
}).RequireAuthorization();

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
    var devices = await deviceQueryService.GetDevicesAsync(cancellationToken);
    return Results.Ok(devices);
}).RequireAuthorization();

app.MapGet("/api/admin/recordings", async (
    HttpRequest request,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    var recordings = await recordingService.GetRecordingListAsync(cancellationToken);
    return Results.Ok(recordings);
}).RequireAuthorization();

app.MapGet("/api/admin/recordings/{recordingId:guid}/playback-url", async (
    HttpRequest request,
    Guid recordingId,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    var url = await recordingService.GetPlaybackUrlAsync(
        recordingId,
        TimeSpan.FromMinutes(10),
        cancellationToken);

    return Results.Ok(new { url });
}).RequireAuthorization();

app.MapGet("/api/admin/qa-rules", async (
    HttpRequest request,
    QaRuleService ruleService,
    CancellationToken cancellationToken) =>
{
    var rules = await ruleService.GetRulesAsync(cancellationToken);
    return Results.Ok(rules);
}).RequireAuthorization();

app.MapPost("/api/admin/qa-rules", async (
    HttpRequest request,
    QaRuleService ruleService,
    CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<CreateQaRuleRequest>(
        jsonOptions,
        cancellationToken);

    if (body is null || string.IsNullOrWhiteSpace(body.Phrase))
    {
        return Results.BadRequest("Phrase is required.");
    }

    var rule = await ruleService.CreateRuleAsync(
        body.Phrase,
        body.Severity,
        cancellationToken);

    return Results.Ok(rule);
}).RequireAuthorization();

app.MapGet("/api/admin/qa-alerts", async (
    HttpRequest request,
    QaAlertService alertService,
    CancellationToken cancellationToken) =>
{
    var alerts = await alertService.GetAlertsAsync(cancellationToken);
    return Results.Ok(alerts);
}).RequireAuthorization();

app.MapPost("/api/admin/qa-alerts", async (
    HttpRequest request,
    QaAlertService alertService,
    CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<CreateQaAlertRequest>(
        jsonOptions,
        cancellationToken);

    if (body is null || string.IsNullOrWhiteSpace(body.MatchedPhrase))
    {
        return Results.BadRequest("MatchedPhrase is required.");
    }

    await alertService.CreateAlertAsync(
        body.RecordingId,
        body.MatchedPhrase,
        body.TimestampUtc,
        cancellationToken);

    return Results.Ok(new { created = true });
}).RequireAuthorization();

app.MapGet("/api/admin/users", async (
    HttpRequest request,
    AdminUserService adminUserService,
    CancellationToken cancellationToken) =>
{
    var users = await adminUserService.GetUsersAsync(cancellationToken);
    return Results.Ok(users);
}).RequireAuthorization();

app.MapPost("/api/admin/users", async (
    HttpRequest request,
    AdminUserService adminUserService,
    CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<CreateUserRequest>(
        jsonOptions,
        cancellationToken);

    if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
    {
        return Results.BadRequest("FullName, Email, and Password are required.");
    }

    try
    {
        var user = await adminUserService.CreateUserAsync(body, cancellationToken);
        return Results.Ok(user);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapPatch("/api/admin/users/{userId:guid}/status", async (
    Guid userId,
    bool isActive,
    HttpRequest request,
    AdminUserService adminUserService,
    CancellationToken cancellationToken) =>
{
    try
    {
        await adminUserService.UpdateUserStatusAsync(userId, isActive, cancellationToken);
        return Results.Ok(new { updated = true });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/admin/teachers", async (
    HttpRequest request,
    TeacherService teacherService,
    CancellationToken cancellationToken) =>
{
    var teachers = await teacherService.GetTeachersAsync(cancellationToken);
    return Results.Ok(teachers);
}).RequireAuthorization();

app.MapPost("/api/admin/teachers", async (
    HttpRequest request,
    TeacherService teacherService,
    CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<CreateTeacherRequest>(
        jsonOptions,
        cancellationToken);

    if (body is null || string.IsNullOrWhiteSpace(body.FullName))
    {
        return Results.BadRequest("FullName is required.");
    }

    var teacher = await teacherService.CreateTeacherAsync(body, cancellationToken);
    return Results.Ok(teacher);
}).RequireAuthorization();

app.MapGet("/api/admin/manager-assignments", async (
    HttpRequest request,
    ManagerAssignmentService assignmentService,
    CancellationToken cancellationToken) =>
{
    var assignments = await assignmentService.GetAssignmentsAsync(cancellationToken);
    return Results.Ok(assignments);
}).RequireAuthorization();

app.MapPost("/api/admin/manager-assignments", async (
    HttpRequest request,
    ManagerAssignmentService assignmentService,
    CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<CreateManagerAssignmentRequest>(
        jsonOptions,
        cancellationToken);

    if (body is null || body.ManagerUserId == Guid.Empty || body.TeacherId == Guid.Empty)
    {
        return Results.BadRequest("ManagerUserId and TeacherId are required.");
    }

    try
    {
        await assignmentService.AssignTeacherAsync(
            body.ManagerUserId,
            body.TeacherId,
            cancellationToken);

        return Results.Ok(new { assigned = true });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/worker/recordings/pending", async (
    HttpRequest request,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != workerApiKey)
    {
        return Results.Unauthorized();
    }

    var pending = await recordingService.GetPendingQaRecordingsAsync(cancellationToken);
    return Results.Ok(pending);
});

app.MapPost("/api/worker/recordings/{recordingId:guid}/mark-qa-processed", async (
    HttpRequest request,
    Guid recordingId,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != workerApiKey)
    {
        return Results.Unauthorized();
    }

    await recordingService.MarkQaProcessedAsync(recordingId, cancellationToken);
    return Results.Ok(new { processed = true });
});

app.MapGet("/api/worker/qa-rules", async (
    HttpRequest request,
    QaRuleService ruleService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != workerApiKey)
    {
        return Results.Unauthorized();
    }

    var rules = await ruleService.GetRulesAsync(cancellationToken);
    return Results.Ok(rules);
});

app.MapPost("/api/worker/qa-alerts", async (
    HttpRequest request,
    QaAlertService alertService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != workerApiKey)
    {
        return Results.Unauthorized();
    }

    var body = await request.ReadFromJsonAsync<CreateQaAlertRequest>(
        jsonOptions,
        cancellationToken);

    if (body is null || string.IsNullOrWhiteSpace(body.MatchedPhrase))
    {
        return Results.BadRequest("MatchedPhrase is required.");
    }

    await alertService.CreateAlertAsync(
        body.RecordingId,
        body.MatchedPhrase,
        body.TimestampUtc,
        cancellationToken);

    return Results.Ok(new { created = true });
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

static async Task SeedOwnerAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    await dbContext.Database.MigrateAsync();

    string seedEmail = app.Configuration["SeedOwner:Email"] ?? "owner@academy.local";

    bool exists = await dbContext.Users.AnyAsync(u => u.Email == seedEmail);

    if (!exists)
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            FullName = app.Configuration["SeedOwner:FullName"] ?? "Owner",
            Email = seedEmail,
            PasswordHash = passwordHasher.Hash(
                app.Configuration["SeedOwner:Password"] ?? "OwnerPass123!"),
            Role = UserRole.Owner,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Users.Add(owner);
        await dbContext.SaveChangesAsync();
    }
}