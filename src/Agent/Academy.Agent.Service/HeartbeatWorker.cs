using Academy.Agent.Cloud;

namespace Academy.Agent.Service;

public sealed class HeartbeatWorker : BackgroundService
{
    private readonly ILogger<HeartbeatWorker> _logger;
    private readonly IAgentCloudClient _cloudClient;
    private readonly IDeviceIdentityProvider _identityProvider;
    private readonly CloudOptions _cloudOptions;

    public HeartbeatWorker(
        ILogger<HeartbeatWorker> logger,
        IAgentCloudClient cloudClient,
        IDeviceIdentityProvider identityProvider,
        CloudOptions cloudOptions)
    {
        _logger = logger;
        _cloudClient = cloudClient;
        _identityProvider = identityProvider;
        _cloudOptions = cloudOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cloudOptions.Enabled)
        {
            _logger.LogInformation("Cloud heartbeat is disabled.");
            return;
        }

        _logger.LogInformation(
            "Cloud heartbeat enabled. BaseUrl={BaseUrl}, Interval={IntervalSeconds}s",
            _cloudOptions.BaseUrl,
            _cloudOptions.HeartbeatIntervalSeconds);

        var identity = await _identityProvider.GetOrCreateIdentityAsync(stoppingToken);

        _logger.LogInformation(
            "Device identity loaded: {DeviceId} ({DeviceName})",
            identity.DeviceId,
            identity.DeviceName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new HeartbeatRequest
                {
                    DeviceId = identity.DeviceId,
                    DeviceName = identity.DeviceName
                };

                var response = await _cloudClient.SendHeartbeatAsync(request, stoppingToken);

                _logger.LogInformation(
                    "Heartbeat sent. Received={Received}, Command={Command}",
                    response.Received,
                    response.Command ?? "None");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat failed. Will retry.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_cloudOptions.HeartbeatIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}