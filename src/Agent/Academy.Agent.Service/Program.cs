using Academy.Agent.Cloud;
using Academy.Agent.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Academy Agent Service";
});

var cloudOptions = builder.Configuration
    .GetSection("Cloud")
    .Get<CloudOptions>() ?? new CloudOptions();

builder.Services.AddSingleton(cloudOptions);

builder.Services.AddHttpClient<IAgentCloudClient, AgentCloudClient>(client =>
{
    client.BaseAddress = new Uri(cloudOptions.BaseUrl);
});

builder.Services.AddSingleton<IDeviceIdentityProvider>(_ =>
{
    string identityPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AcademyAgent",
        "device.json");

    return new FileDeviceIdentityProvider(identityPath, Environment.MachineName);
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

builder.Services.AddHostedService<RecordingWorker>();
builder.Services.AddHostedService<HeartbeatWorker>();
builder.Services.AddHostedService<LiveStreamingWorker>();

var host = builder.Build();

host.Run();
