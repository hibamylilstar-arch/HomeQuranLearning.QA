using Academy.Infrastructure.Repositories;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Exceptions;
using Academy.Application.Options;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.DependencyInjection;
using Academy.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string OwnerOrAdminPolicy = "OwnerOrAdmin";
const string OwnerOnlyPolicy = "OwnerOnly";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<DeviceQueryService>();
builder.Services.AddScoped<QaRuleService>();
builder.Services.AddScoped<QaAlertService>();
builder.Services.AddScoped<QaCandidateService>();
builder.Services.AddScoped<TranscriptSegmentService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<TeacherService>();
builder.Services.AddScoped<ManagerAssignmentService>();
builder.Services.AddScoped<DashboardQueryService>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<CourseService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<ScheduleAccessService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AttendanceReducer>();
builder.Services.AddScoped<DailyAttendanceReportService>();
builder.Services.AddScoped<ISessionEventRepository, SessionEventRepository>();
builder.Services.AddScoped<LiveKitTokenService>();

builder.Services.AddHostedService<Academy.Api.SessionSchedulerWorker>();
builder.Services.AddHostedService<Academy.Api.RecordingRetentionWorker>();

builder.Services.AddSingleton(builder.Configuration["Storage:Bucket"] ?? "academy-recordings");

var liveKitOptions = builder.Configuration
    .GetSection("LiveKit")
    .Get<LiveKitOptions>() ?? new LiveKitOptions();

builder.Services.AddSingleton(liveKitOptions);

builder.Services.AddScoped<RecordingService>(sp =>
{
    var recordingRepository = sp.GetRequiredService<IRecordingRepository>();
    var deviceRepository = sp.GetRequiredService<IDeviceRepository>();
    var sessionRepository = sp.GetRequiredService<ISessionRepository>();
    var storageService = sp.GetRequiredService<IStorageService>();
    var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
    var bucketName = sp.GetRequiredService<string>();

    return new RecordingService(
        recordingRepository,
        deviceRepository,
        sessionRepository,
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        OwnerOrAdminPolicy,
        policy => policy.RequireRole(
            UserRole.Owner.ToString(),
            UserRole.Admin.ToString()));
    options.AddPolicy(OwnerOnlyPolicy, policy => policy.RequireRole(UserRole.Owner.ToString()));
});

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

if (app.Configuration.GetValue("HttpsRedirection:Enabled", true))
{
    app.UseHttpsRedirection();
}
app.UseCors("DashboardCors");
app.UseAuthentication();
app.UseAuthorization();

string agentApiKey = app.Configuration["AgentApiKey"] ?? string.Empty;
string workerApiKey = app.Configuration["WorkerApiKey"] ?? string.Empty;
string archiveRegistrarApiKey = app.Configuration["ArchiveRegistrarApiKey"] ?? string.Empty;

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

app.MapGet("/api/agent/sessions/active-stream", async (
    HttpRequest request,
    IDeviceRepository deviceRepository,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != agentApiKey)
    {
        return Results.Unauthorized();
    }

    if (!request.Query.TryGetValue("deviceId", out var deviceIdValue) ||
        string.IsNullOrWhiteSpace(deviceIdValue.ToString()))
    {
        return Results.BadRequest("deviceId is required.");
    }

    var device = await deviceRepository.GetByDeviceIdAsync(
        deviceIdValue.ToString(),
        cancellationToken);

    if (device is null || string.IsNullOrWhiteSpace(device.LiveKitStreamKey))
    {
        return Results.Ok(new { hasStream = false });
    }

    return Results.Ok(new
    {
        hasStream = true,
        deviceId = device.Id,
        roomName = $"device-{device.Id}",
        streamKey = device.LiveKitStreamKey
    });
});

app.MapPost("/api/agent/session-events", async (
    HttpRequest request,
    AgentSessionEventRequest body,
    SessionService sessionService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != agentApiKey)
    {
        return Results.Unauthorized();
    }

    try
    {
        var result =
            await sessionService.SubmitAgentSessionEventAsync(
                body,
                cancellationToken);

        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(
            new { error = ex.Message });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(
            new { error = ex.Message });
    }
});
app.MapGet("/api/agent/class-window", async (
    HttpRequest request,
    SessionService sessionService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != agentApiKey)
    {
        return Results.Unauthorized();
    }

    if (!request.Query.TryGetValue("deviceId", out var deviceIdValue) ||
        string.IsNullOrWhiteSpace(deviceIdValue.ToString()))
    {
        return Results.BadRequest("deviceId is required.");
    }

    try
    {
        var window =
            await sessionService.GetAgentClassWindowAsync(
                deviceIdValue.ToString(),
                cancellationToken);

        return Results.Ok(window);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(
            new { error = ex.Message });
    }
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

    try
    {
        var response = await recordingService.SubmitRecordingAsync(body, cancellationToken);
        return Results.Ok(response);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
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


app.MapPost("/api/worker/server-recordings/finalized", async (
    HttpRequest request,
    ServerArchiveCompletedRequest body,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(archiveRegistrarApiKey) ||
        !request.Headers.TryGetValue(
            "X-Api-Key",
            out var values) ||
        values.ToString() != archiveRegistrarApiKey)
    {
        return Results.Unauthorized();
    }

    try
    {
        ServerArchiveRegistrationResponse response =
            await recordingService.RegisterServerArchiveAsync(
                body,
                cancellationToken);

        return Results.Ok(response);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(
            new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(
            new { error = ex.Message });
    }
});

app.MapPost("/api/worker/server-recordings/resolve-device", async (
    HttpRequest request,
    ServerArchiveDeviceResolveRequest body,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(archiveRegistrarApiKey) ||
        !request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != archiveRegistrarApiKey)
    {
        return Results.Unauthorized();
    }

    try
    {
        var response = await recordingService.ResolveServerArchiveDeviceAsync(body, cancellationToken);
        return Results.Ok(response);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapGet("/api/admin/devices", async (
    ClaimsPrincipal user,
    HttpRequest request,
    DashboardQueryService dashboardQueryService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);
    var devices = await dashboardQueryService.GetVisibleDevicesAsync(userId, role, cancellationToken);
    return Results.Ok(devices);
}).RequireAuthorization();

app.MapPatch("/api/admin/devices/{deviceId:guid}/recording-display-name", async (
    Guid deviceId,
    UpdateRecordingDisplayNameRequest body,
    IDeviceRepository deviceRepository,
    IUnitOfWork unitOfWork,
    CancellationToken cancellationToken) =>
{
    var device =
        await deviceRepository.GetByIdAsync(
            deviceId,
            cancellationToken);

    if (device is null)
    {
        return Results.NotFound(
            new { error = "Device not found." });
    }

    string? displayName =
        string.IsNullOrWhiteSpace(
            body.RecordingDisplayName)
            ? null
            : body.RecordingDisplayName.Trim();

    if (displayName is not null &&
        displayName.Length > 100)
    {
        return Results.BadRequest(
            new
            {
                error =
                    "Recording display name must be 100 characters or less."
            });
    }

    device.RecordingDisplayName =
        displayName;

    device.UpdatedAtUtc =
        DateTimeOffset.UtcNow;

    deviceRepository.Update(device);

    await unitOfWork.SaveChangesAsync(
        cancellationToken);

    return Results.Ok(new
    {
        deviceId = device.Id,
        actualDeviceName = device.DeviceName,
        recordingDisplayName =
            device.RecordingDisplayName
    });
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapGet("/api/admin/recordings", async (
    ClaimsPrincipal user,
    HttpRequest request,
    DashboardQueryService dashboardQueryService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);
    var recordings = await dashboardQueryService.GetVisibleRecordingsAsync(userId, role, cancellationToken);
    return Results.Ok(recordings);
}).RequireAuthorization();

app.MapGet("/api/admin/recordings/{recordingId:guid}/playback-url", async (
    ClaimsPrincipal user,
    HttpRequest request,
    Guid recordingId,
    DashboardQueryService dashboardQueryService,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);

    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    var canAccess =
        await dashboardQueryService.CanAccessRecordingAsync(
            recordingId,
            userId,
            role,
            cancellationToken);

    if (!canAccess)
    {
        return Results.NotFound();
    }

    try
    {
        var url = await recordingService.GetPlaybackUrlAsync(
            recordingId,
            TimeSpan.FromMinutes(10),
            cancellationToken);

        return Results.Ok(new { url });
    }
    catch (RecordingUnavailableException)
    {
        return Results.BadRequest(
            new { error = "Recording file is not available." });
    }
}).RequireAuthorization();

app.MapGet("/api/admin/recordings/{recordingId:guid}/transcript-segments", async (
    ClaimsPrincipal user,
    Guid recordingId,
    DashboardQueryService dashboardQueryService,
    TranscriptSegmentService transcriptSegmentService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);

    if (!await dashboardQueryService.CanAccessRecordingAsync(
            recordingId,
            userId,
            role,
            cancellationToken))
    {
        return Results.NotFound();
    }

    var segments = await transcriptSegmentService.GetByRecordingIdAsync(
        recordingId,
        cancellationToken);

    return Results.Ok(segments);
}).RequireAuthorization();

app.MapGet("/api/admin/recordings/{recordingId:guid}/download-url", async (
    ClaimsPrincipal user,
    Guid recordingId,
    DashboardQueryService dashboardQueryService,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);

    var visibleRecordings =
        await dashboardQueryService.GetVisibleRecordingsAsync(
            userId,
            role,
            cancellationToken);

    var recording =
        visibleRecordings.FirstOrDefault(x => x.Id == recordingId);

    if (recording is null)
    {
        return Results.Forbid();
    }

    if (!string.Equals(
            recording.Status,
            "Uploaded",
            StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(
            new { error = "Recording file is not available." });
    }

    var url =
        await recordingService.GetPlaybackUrlAsync(
            recordingId,
            TimeSpan.FromMinutes(10),
            cancellationToken);

    return Results.Ok(new
    {
        url,
        fileName = recording.FileName
    });
}).RequireAuthorization();

app.MapPost("/api/admin/recordings/{recordingId:guid}/preserve", async (
    ClaimsPrincipal user,
    Guid recordingId,
    DashboardQueryService dashboardQueryService,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);

    var visibleRecordings =
        await dashboardQueryService.GetVisibleRecordingsAsync(
            userId,
            role,
            cancellationToken);

    if (!visibleRecordings.Any(x => x.Id == recordingId))
    {
        return Results.Forbid();
    }

    await recordingService.SetPreservedAsync(
        recordingId,
        true,
        cancellationToken);

    return Results.Ok(new { preserved = true });
}).RequireAuthorization();

app.MapPost("/api/admin/recordings/{recordingId:guid}/unpreserve", async (
    ClaimsPrincipal user,
    Guid recordingId,
    DashboardQueryService dashboardQueryService,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);

    var visibleRecordings =
        await dashboardQueryService.GetVisibleRecordingsAsync(
            userId,
            role,
            cancellationToken);

    if (!visibleRecordings.Any(x => x.Id == recordingId))
    {
        return Results.Forbid();
    }

    await recordingService.SetPreservedAsync(
        recordingId,
        false,
        cancellationToken);

    return Results.Ok(new { preserved = false });
}).RequireAuthorization();

app.MapDelete("/api/admin/recordings/{recordingId:guid}", async (
    ClaimsPrincipal user,
    Guid recordingId,
    RecordingService recordingService,
    CancellationToken cancellationToken) =>
{
    var (userId, _) = GetUserInfo(user);

    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    bool deleted =
        await recordingService.DeleteRecordingMediaAsync(
            recordingId,
            userId,
            "OwnerManual",
            cancellationToken);

    return deleted
        ? Results.Ok(new { deleted = true })
        : Results.NotFound();
}).RequireAuthorization(OwnerOnlyPolicy);

app.MapGet("/api/admin/qa-rules", async (
    ClaimsPrincipal user,
    HttpRequest request,
    QaRuleService ruleService,
    CancellationToken cancellationToken) =>
{
    var rules = await ruleService.GetRulesAsync(cancellationToken);
    return Results.Ok(rules);
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapPost("/api/admin/qa-rules", async (
    ClaimsPrincipal user,
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
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapGet("/api/admin/qa-alerts", async (
    ClaimsPrincipal user,
    HttpRequest request,
    DashboardQueryService dashboardQueryService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);
    var alerts = await dashboardQueryService.GetVisibleQaAlertsAsync(userId, role, cancellationToken);
    return Results.Ok(alerts);
}).RequireAuthorization();

app.MapPost("/api/admin/qa-alerts", async (
    ClaimsPrincipal user,
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
        body.QaRuleId,
        body.MatchedPhrase,
        body.TimestampUtc,
        cancellationToken);

    return Results.Ok(new { created = true });
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapGet("/api/admin/users", async (
    ClaimsPrincipal user,
    HttpRequest request,
    AdminUserService adminUserService,
    CancellationToken cancellationToken) =>
{
    var users = await adminUserService.GetUsersAsync(cancellationToken);
    return Results.Ok(users);
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapPost("/api/admin/users", async (
    ClaimsPrincipal user,
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
        var createdUser = await adminUserService.CreateUserAsync(body, cancellationToken);
        return Results.Ok(createdUser);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization(OwnerOnlyPolicy);

app.MapPatch("/api/admin/users/{userId:guid}/status", async (
    ClaimsPrincipal user,
    HttpRequest request,
    Guid userId,
    bool isActive,
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
}).RequireAuthorization(OwnerOnlyPolicy);

app.MapPost("/api/admin/users/{userId:guid}/reset-password", async (Guid userId, ResetUserPasswordRequest body, AdminUserService adminUserService, CancellationToken cancellationToken) =>
{
    try { await adminUserService.ResetPasswordAsync(userId, body.Password, cancellationToken); return Results.Ok(new { updated = true }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
}).RequireAuthorization(OwnerOnlyPolicy);

app.MapDelete("/api/admin/users/{userId:guid}", async (Guid userId, AdminUserService adminUserService, CancellationToken cancellationToken) =>
{
    try { await adminUserService.DeleteUserAsync(userId, cancellationToken); return Results.Ok(new { deleted = true }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
    catch (Microsoft.EntityFrameworkCore.DbUpdateException) { return Results.Conflict(new { message = "Account has preserved history. Disable it instead." }); }
}).RequireAuthorization(OwnerOnlyPolicy);

app.MapGet("/api/admin/teachers", async (
    ClaimsPrincipal user,
    HttpRequest request,
    TeacherService teacherService,
    CancellationToken cancellationToken) =>
{
    var teachers = await teacherService.GetTeachersAsync(cancellationToken);
    return Results.Ok(teachers);
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapPost("/api/admin/teachers", async (
    ClaimsPrincipal user,
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
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapGet("/api/admin/manager-assignments", async (
    ClaimsPrincipal user,
    HttpRequest request,
    ManagerAssignmentService assignmentService,
    CancellationToken cancellationToken) =>
{
    var assignments = await assignmentService.GetAssignmentsAsync(cancellationToken);
    return Results.Ok(assignments);
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapPost("/api/admin/manager-assignments", async (
    ClaimsPrincipal user,
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
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapGet("/api/admin/students", async (
    ClaimsPrincipal user,
    HttpRequest request,
    StudentService studentService,
    CancellationToken cancellationToken) =>
{
    var students = await studentService.GetStudentsAsync(cancellationToken);
    return Results.Ok(students);
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapPost("/api/admin/students", async (
    ClaimsPrincipal user,
    HttpRequest request,
    StudentService studentService,
    CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<CreateStudentRequest>(
        jsonOptions,
        cancellationToken);

    if (body is null || string.IsNullOrWhiteSpace(body.FullName))
    {
        return Results.BadRequest("FullName is required.");
    }

    var student = await studentService.CreateStudentAsync(body, cancellationToken);
    return Results.Ok(student);
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapGet("/api/admin/courses", async (
    ClaimsPrincipal user,
    HttpRequest request,
    CourseService courseService,
    CancellationToken cancellationToken) =>
{
    var courses = await courseService.GetCoursesAsync(cancellationToken);
    return Results.Ok(courses);
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapPost("/api/admin/courses", async (
    ClaimsPrincipal user,
    HttpRequest request,
    CourseService courseService,
    CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<CreateCourseRequest>(
        jsonOptions,
        cancellationToken);

    if (body is null || string.IsNullOrWhiteSpace(body.Name))
    {
        return Results.BadRequest("Name is required.");
    }

    var course = await courseService.CreateCourseAsync(body, cancellationToken);
    return Results.Ok(course);
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapGet("/api/admin/schedules", async (
    ClaimsPrincipal user,
    ScheduleService scheduleService,
    ScheduleAccessService scheduleAccessService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) =
        GetUserInfo(user);

    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    var schedules =
        await scheduleService.GetSchedulesAsync(
            cancellationToken);

    var visibleSchedules =
        await scheduleAccessService
            .FilterVisibleSchedulesAsync(
                schedules,
                userId,
                role,
                cancellationToken);

    return Results.Ok(
        visibleSchedules);
}).RequireAuthorization();

app.MapPost("/api/admin/schedules", async (
    ClaimsPrincipal user,
    HttpRequest request,
    ScheduleService scheduleService,
    ScheduleAccessService scheduleAccessService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) =
        GetUserInfo(user);

    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    var body =
        await request.ReadFromJsonAsync<CreateScheduleRequest>(
            jsonOptions,
            cancellationToken);

    if (body is null)
    {
        return Results.BadRequest(
            "Schedule data is required.");
    }

    var canManageTeacher =
        await scheduleAccessService
            .CanManageTeacherAsync(
                userId,
                role,
                body.TeacherId,
                cancellationToken);

    if (!canManageTeacher)
    {
        return Results.Forbid();
    }

    try
    {
        var schedule =
            await scheduleService.CreateScheduleAsync(
                body,
                cancellationToken);

        return Results.Ok(
            schedule);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(
            new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(
            new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPatch("/api/admin/schedules/{scheduleId:guid}", async (
    ClaimsPrincipal user,
    Guid scheduleId,
    HttpRequest request,
    ScheduleService scheduleService,
    ScheduleAccessService scheduleAccessService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) =
        GetUserInfo(user);

    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    var body =
        await request.ReadFromJsonAsync<UpdateScheduleRequest>(
            jsonOptions,
            cancellationToken);

    if (body is null)
    {
        return Results.BadRequest(
            "Schedule data is required.");
    }

    var canAccessCurrent =
        await scheduleAccessService
            .CanAccessScheduleAsync(
                scheduleId,
                userId,
                role,
                cancellationToken);

    if (!canAccessCurrent)
    {
        // Match the existing session-access behavior:
        // do not expose schedules outside Manager scope.
        return Results.NotFound();
    }

    var canManageReplacementTeacher =
        await scheduleAccessService
            .CanManageTeacherAsync(
                userId,
                role,
                body.TeacherId,
                cancellationToken);

    if (!canManageReplacementTeacher)
    {
        return Results.Forbid();
    }

    try
    {
        var schedule =
            await scheduleService.ReplaceScheduleAsync(
                scheduleId,
                body,
                cancellationToken);

        return Results.Ok(
            schedule);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(
            new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(
            new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(
            new { error = ex.Message });
    }
}).RequireAuthorization();
app.MapGet("/api/admin/reports/daily-attendance", async (
    DateOnly? date,
    ClaimsPrincipal user,
    DailyAttendanceReportService reportService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) =
        GetUserInfo(user);

    var report =
        await reportService
            .GetDailyReportAsync(
                date,
                userId,
                role,
                cancellationToken);

    return Results.Ok(report);
})
.RequireAuthorization();

app.MapGet("/api/admin/sessions", async (
    ClaimsPrincipal user,
    DashboardQueryService dashboardQueryService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) =
        GetUserInfo(user);

    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    var sessions =
        await dashboardQueryService
            .GetVisibleSessionsAsync(
                userId,
                role,
                cancellationToken);

    return Results.Ok(sessions);
}).RequireAuthorization();

app.MapGet("/api/admin/qa-candidates", async (
    ClaimsPrincipal user,
    DashboardQueryService dashboardQueryService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);
    var candidates = await dashboardQueryService.GetVisibleQaCandidatesAsync(
        userId,
        role,
        cancellationToken);

    return Results.Ok(candidates);
}).RequireAuthorization();

app.MapPost("/api/admin/qa-candidates/{candidateId:guid}/review", async (
    ClaimsPrincipal user,
    Guid candidateId,
    ReviewQaCandidateRequest body,
    DashboardQueryService dashboardQueryService,
    QaCandidateService candidateService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);

    if (!await dashboardQueryService.CanAccessCandidateAsync(
            candidateId,
            userId,
            role,
            cancellationToken))
    {
        return Results.NotFound();
    }

    try
    {
        var candidate = await candidateService.ReviewAsync(
            candidateId,
            userId,
            body,
            cancellationToken);

        return Results.Ok(candidate);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapGet("/api/admin/sessions/{sessionId:guid}/events", async (
    ClaimsPrincipal user,
    Guid sessionId,
    DashboardQueryService dashboardQueryService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) =
        GetUserInfo(user);

    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    var events =
        await dashboardQueryService
            .GetVisibleSessionEventsAsync(
                sessionId,
                userId,
                role,
                cancellationToken);

    return events is null
        ? Results.NotFound()
        : Results.Ok(events);
}).RequireAuthorization();

app.MapPatch("/api/admin/sessions/{sessionId:guid}/attendance-review", async (
    ClaimsPrincipal user,
    Guid sessionId,
    ReviewAttendanceRequest body,
    DashboardQueryService dashboardQueryService,
    SessionService sessionService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) =
        GetUserInfo(user);

    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    var canAccess =
        await dashboardQueryService
            .CanAccessSessionAsync(
                sessionId,
                userId,
                role,
                cancellationToken);

    if (!canAccess)
    {
        return Results.NotFound();
    }

    try
    {
        await sessionService
            .ReviewAttendanceAsync(
                sessionId,
                body,
                cancellationToken);

        return Results.Ok(
            new
            {
                updated = true
            });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(
            new
            {
                error = ex.Message
            });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(
            new
            {
                error = ex.Message
            });
    }
}).RequireAuthorization();
app.MapGet("/api/admin/live-sessions", async (
    ClaimsPrincipal user,
    HttpRequest request,
    DashboardQueryService dashboardQueryService,
    CancellationToken cancellationToken) =>
{
    var (userId, role) = GetUserInfo(user);

    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    var sessions =
        await dashboardQueryService.GetVisibleLiveSessionsAsync(
            userId,
            role,
            cancellationToken);

    return Results.Ok(sessions);
}).RequireAuthorization();

app.MapPost("/api/admin/sessions", async (
    ClaimsPrincipal user,
    HttpRequest request,
    SessionService sessionService,
    CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<CreateSessionRequest>(
        jsonOptions,
        cancellationToken);

    if (body is null)
    {
        return Results.BadRequest("Session data is required.");
    }

    var session = await sessionService.CreateSessionAsync(body, cancellationToken);
    return Results.Ok(session);
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapPost("/api/admin/livekit/token", async (
    ClaimsPrincipal user,
    HttpRequest request,
    DashboardQueryService dashboardQueryService,
    LiveKitTokenService liveKitTokenService,
    CancellationToken cancellationToken) =>
{
    var body = await request.ReadFromJsonAsync<LiveKitTokenRequest>(
        jsonOptions,
        cancellationToken);

    if (body is null ||
        string.IsNullOrWhiteSpace(body.RoomName) ||
        string.IsNullOrWhiteSpace(body.Identity))
    {
        return Results.BadRequest("RoomName and Identity are required.");
    }

    var (userId, role) = GetUserInfo(user);

    if (userId == Guid.Empty)
    {
        return Results.Unauthorized();
    }

    if (role == UserRole.Manager.ToString() && body.CanPublish)
    {
        return Results.Forbid();
    }

    bool canAccess;

    if (body.RoomName.StartsWith("device-", StringComparison.Ordinal) &&
        Guid.TryParse(body.RoomName["device-".Length..], out Guid deviceId))
    {
        var visibleDevices =
            await dashboardQueryService.GetVisibleDevicesAsync(
                userId,
                role,
                cancellationToken);

        canAccess = visibleDevices.Any(x => x.Id == deviceId);
    }
    else if (body.RoomName.StartsWith("session-", StringComparison.Ordinal) &&
             Guid.TryParse(body.RoomName["session-".Length..], out Guid sessionId))
    {
        canAccess =
            await dashboardQueryService.CanAccessLiveSessionAsync(
                sessionId,
                userId,
                role,
                cancellationToken);
    }
    else
    {
        return Results.BadRequest(
            "RoomName must use the device-{guid} or session-{guid} format.");
    }

    if (!canAccess)
    {
        return Results.NotFound();
    }

    var token = liveKitTokenService.GenerateToken(
        body.RoomName,
        body.Identity,
        body.CanPublish,
        body.CanSubscribe);

    return Results.Ok(new
    {
        url = liveKitTokenService.Host,
        token
    });
}).RequireAuthorization();

app.MapGet("/api/admin/livekit/server-token", async (
    ClaimsPrincipal user,
    HttpRequest request,
    LiveKitTokenService liveKitTokenService) =>
{
    var token = liveKitTokenService.GenerateServerApiToken();
    return Results.Ok(new { token });
}).RequireAuthorization(OwnerOrAdminPolicy);

app.MapGet("/api/worker/devices/pending-livekit-ingress", async (
    HttpRequest request,
    IDeviceRepository deviceRepository,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != workerApiKey)
    {
        return Results.Unauthorized();
    }

    var devices = await deviceRepository.GetAllAsync(cancellationToken);

    var pending = devices
        .Where(x => string.IsNullOrWhiteSpace(x.LiveKitStreamKey))
        .Select(x => new
        {
            deviceId = x.Id,
            roomName = $"device-{x.Id}",
            deviceName = x.DeviceName
        })
        .ToList();

    return Results.Ok(pending);
});

app.MapPost("/api/worker/devices/{deviceId:guid}/livekit-ingress", async (
    HttpRequest request,
    Guid deviceId,
    UpdateSessionLiveKitIngressRequest body,
    IDeviceRepository deviceRepository,
    IUnitOfWork unitOfWork,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != workerApiKey)
    {
        return Results.Unauthorized();
    }

    if (body is null ||
        string.IsNullOrWhiteSpace(body.IngressId) ||
        string.IsNullOrWhiteSpace(body.StreamKey))
    {
        return Results.BadRequest("IngressId and StreamKey are required.");
    }

    var device = await deviceRepository.GetByIdAsync(
        deviceId,
        cancellationToken);

    if (device is null)
    {
        return Results.NotFound(new { error = "Device not found." });
    }

    device.LiveKitIngressId = body.IngressId;
    device.LiveKitStreamKey = body.StreamKey;
    device.UpdatedAtUtc = DateTimeOffset.UtcNow;

    deviceRepository.Update(device);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Results.Ok(new { updated = true });
});

app.MapGet("/api/worker/sessions/pending-livekit-ingress", async (
    HttpRequest request,
    SessionService sessionService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != workerApiKey)
    {
        return Results.Unauthorized();
    }

    var pending = await sessionService.GetPendingLiveKitIngressAsync(cancellationToken);
    return Results.Ok(pending);
});

app.MapPost("/api/worker/sessions/{sessionId:guid}/livekit-ingress", async (
    HttpRequest request,
    Guid sessionId,
    UpdateSessionLiveKitIngressRequest body,
    SessionService sessionService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != workerApiKey)
    {
        return Results.Unauthorized();
    }

    if (body is null || string.IsNullOrWhiteSpace(body.IngressId) || string.IsNullOrWhiteSpace(body.StreamKey))
    {
        return Results.BadRequest("IngressId and StreamKey are required.");
    }

    await sessionService.UpdateLiveKitIngressAsync(sessionId, body.IngressId, body.StreamKey, cancellationToken);
    return Results.Ok(new { updated = true });
});

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
        body.QaRuleId,
        body.MatchedPhrase,
        body.TimestampUtc,
        cancellationToken);

    return Results.Ok(new { created = true });
});

app.MapPost("/api/worker/qa-candidates", async (
    HttpRequest request,
    CreateQaCandidateRequest body,
    QaCandidateService candidateService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != workerApiKey)
    {
        return Results.Unauthorized();
    }

    try
    {
        var candidate = await candidateService.CreateAsync(
            body,
            cancellationToken);

        return Results.Ok(candidate);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapPost("/api/worker/recordings/{recordingId:guid}/transcript-segments", async (
    HttpRequest request,
    Guid recordingId,
    PersistTranscriptSegmentsRequest body,
    TranscriptSegmentService transcriptSegmentService,
    CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-Api-Key", out var values) ||
        values.ToString() != workerApiKey)
    {
        return Results.Unauthorized();
    }

    try
    {
        var result = await transcriptSegmentService.PersistAsync(
            recordingId,
            body.Segments,
            cancellationToken);

        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

static (Guid UserId, string Role) GetUserInfo(ClaimsPrincipal user)
{
    string? userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    Guid userId = userIdValue is not null && Guid.TryParse(userIdValue, out Guid parsed)
        ? parsed
        : Guid.Empty;

    string role = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    return (userId, role);
}

static bool TryGetSessionIdFromRoomName(
    string roomName,
    out Guid sessionId)
{
    const string prefix = "session-";

    if (!roomName.StartsWith(
            prefix,
            StringComparison.Ordinal))
    {
        sessionId = Guid.Empty;
        return false;
    }

    return Guid.TryParse(
        roomName[prefix.Length..],
        out sessionId);
}

static async Task SeedOwnerAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    await dbContext.Database.MigrateAsync();

    string? seedEmail = app.Configuration["SeedOwner:Email"];
    string? seedPassword = app.Configuration["SeedOwner:Password"];

    if (string.IsNullOrWhiteSpace(seedEmail) && string.IsNullOrWhiteSpace(seedPassword))
        return;

    if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword))
        throw new InvalidOperationException("SeedOwner requires both Email and Password.");
    bool exists = await dbContext.Users.AnyAsync(u => u.Email == seedEmail);

    if (!exists)
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            FullName = app.Configuration["SeedOwner:FullName"] ?? "Owner",
            Email = seedEmail,
            PasswordHash = passwordHasher.Hash(seedPassword!),
            Role = UserRole.Owner,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Users.Add(owner);
        await dbContext.SaveChangesAsync();
    }
}




