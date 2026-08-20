using Academy.Agent.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Academy Agent Service";
});

builder.Services.AddHostedService<RecordingWorker>();

var host = builder.Build();

host.Run();