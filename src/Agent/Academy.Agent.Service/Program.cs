using Academy.Agent.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "HomeQuranLearning Academy Agent";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();