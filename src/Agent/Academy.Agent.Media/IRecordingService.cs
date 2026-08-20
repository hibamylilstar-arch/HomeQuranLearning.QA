namespace Academy.Agent.Media;

public interface IRecordingService
{
    event EventHandler<RecordingCompletedEventArgs>? RecordingCompleted;
    event EventHandler<RecordingFailedEventArgs>? RecordingFailed;

    Task StartAsync(string outputPath, RecordingOptions? options = null, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}