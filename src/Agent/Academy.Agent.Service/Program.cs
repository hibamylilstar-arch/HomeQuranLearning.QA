using Academy.Agent.Audio;
using Academy.Agent.Cloud;
using Academy.Agent.Service;

var builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

string agentLogDirectory =
    Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData),
        "AcademyAgent",
        "Logs");

builder.Logging.AddProvider(
    new AgentFileLoggerProvider(
        agentLogDirectory));

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Home Quran Learning";
});

var cloudOptions = builder.Configuration
    .GetSection("Cloud")
    .Get<CloudOptions>() ?? new CloudOptions();

if (!string.IsNullOrWhiteSpace(
        cloudOptions.ApiKeyProtectedFile))
{
    cloudOptions.ApiKey =
        WindowsProtectedSecretStore.UnprotectFromFile(
            cloudOptions.ApiKeyProtectedFile);
}

if (cloudOptions.Enabled &&
    string.IsNullOrWhiteSpace(cloudOptions.ApiKey))
{
    throw new InvalidOperationException(
        "The Classroom Agent cloud credential is unavailable.");
}

if (cloudOptions.Enabled &&
    string.IsNullOrWhiteSpace(cloudOptions.AgentVersion))
{
    throw new InvalidOperationException(
        "The Classroom Agent installed version is unavailable.");
}

builder.Services.AddSingleton(cloudOptions);
builder.Services.AddSingleton<ClassroomAudioHub>();
builder.Services.AddSingleton<ClassroomAudioCaptureCoordinator>();
builder.Services.AddSingleton<ClassroomAudioRuntime>();
builder.Services.AddSingleton<AgentActivityState>();
builder.Services.AddSingleton<TeamsObservationTargetState>();
builder.Services.AddSingleton<TeamsEvidenceInbox>();

builder.Services.AddHttpClient<IAgentCloudClient, AgentCloudClient>(client =>
{
    client.BaseAddress = new Uri(cloudOptions.BaseUrl);
});

builder.Services.AddSingleton<IDeviceIdentityProvider>(_ =>
{
    string identityPath =
        builder.Configuration["DeviceIdentityFile"]
        ?? string.Empty;

    if (string.IsNullOrWhiteSpace(identityPath))
    {
        identityPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "AcademyAgent",
            "device.json");
    }

    return new FileDeviceIdentityProvider(
        identityPath,
        Environment.MachineName);
});

builder.Services.AddSingleton(_ =>
{
    string attendancePath =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "AcademyAgent",
            "attendance");

    return new AttendanceEventJournal(
        attendancePath);
});

builder.Services.AddHostedService<AttendanceEventDeliveryWorker>();
builder.Services.AddHostedService<ClassObserverWorker>();
builder.Services.AddHostedService<CommunicationProcessMonitorWorker>();
builder.Services.AddHostedService<TeamsEvidencePipeServer>();
builder.Services.AddHostedService<TeamsEvidenceJournalWorker>();

builder.Services.AddHostedService<RecordingWorker>();
builder.Services.AddHostedService<HeartbeatWorker>();
builder.Services.AddHostedService<LiveStreamingWorker>();
builder.Services.AddHostedService<AgentUpdateReadinessWorker>();

var host = builder.Build();

host.Run();


