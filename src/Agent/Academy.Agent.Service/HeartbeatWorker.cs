using Academy.Agent.Cloud;

namespace Academy.Agent.Service;

public sealed class HeartbeatWorker : BackgroundService
{
    private readonly ILogger<HeartbeatWorker> _logger;
    private readonly IAgentCloudClient _cloudClient;
    private readonly IDeviceIdentityProvider _identityProvider;
    private readonly CloudOptions _cloudOptions;
    private readonly AgentActivityState _activityState;

    private bool _connectionFailureObserved;

    public HeartbeatWorker(
        ILogger<HeartbeatWorker> logger,
        IAgentCloudClient cloudClient,
        IDeviceIdentityProvider identityProvider,
        CloudOptions cloudOptions,
        AgentActivityState activityState)
    {
        _logger = logger;
        _cloudClient = cloudClient;
        _identityProvider = identityProvider;
        _cloudOptions = cloudOptions;
        _activityState = activityState;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_cloudOptions.Enabled)
        {
            _logger.LogInformation(
                "Cloud heartbeat is disabled.");

            return;
        }

        _logger.LogInformation(
            "Cloud heartbeat enabled. BaseUrl={BaseUrl}, Interval={IntervalSeconds}s",
            _cloudOptions.BaseUrl,
            _cloudOptions.HeartbeatIntervalSeconds);

        var identity =
            await _identityProvider.GetOrCreateIdentityAsync(
                stoppingToken);

        _logger.LogInformation(
            "Device identity loaded: {DeviceId} ({DeviceName})",
            identity.DeviceId,
            identity.DeviceName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request =
                    new HeartbeatRequest
                    {
                        DeviceId =
                            identity.DeviceId,

                        DeviceName =
                            identity.DeviceName,

                        AgentVersion =
                            _cloudOptions.AgentVersion
                    };

                var response =
                    await _cloudClient.SendHeartbeatAsync(
                        request,
                        stoppingToken);

                var nowUtc =
                    DateTimeOffset.UtcNow;

                _activityState.Publish(
                    new AgentActivitySignal
                    {
                        Type =
                            AgentActivitySignalType.DeviceOnline,

                        OccurredAtUtc =
                            nowUtc,

                        Source =
                            "Heartbeat",

                        Details =
                            response.Command is null
                                ? "Heartbeat acknowledged."
                                : $"Heartbeat acknowledged. Command={response.Command}"
                    });

                if (_connectionFailureObserved)
                {
                    _activityState.Publish(
                        new AgentActivitySignal
                        {
                            Type =
                                AgentActivitySignalType.ConnectionRestored,

                            OccurredAtUtc =
                                nowUtc,

                            Source =
                                "Heartbeat",

                            Details =
                                "Backend connectivity restored."
                        });

                    _connectionFailureObserved =
                        false;
                }

                _logger.LogInformation(
                    "Heartbeat sent. Received={Received}, Command={Command}",
                    response.Received,
                    response.Command ?? "None");
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!_connectionFailureObserved)
                {
                    _connectionFailureObserved =
                        true;

                    _activityState.Publish(
                        new AgentActivitySignal
                        {
                            Type =
                                AgentActivitySignalType.ConnectionLost,

                            OccurredAtUtc =
                                DateTimeOffset.UtcNow,

                            Source =
                                "Heartbeat",

                            Details =
                                "Backend heartbeat failed."
                        });
                }

                _logger.LogWarning(
                    ex,
                    "Heartbeat failed. Will retry.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        _cloudOptions.HeartbeatIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
