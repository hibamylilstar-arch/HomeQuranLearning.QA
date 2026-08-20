namespace Academy.Agent.Service;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HomeQuranLearning Academy Agent starting up.");

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Agent heartbeat at: {time}", DateTimeOffset.Now);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}