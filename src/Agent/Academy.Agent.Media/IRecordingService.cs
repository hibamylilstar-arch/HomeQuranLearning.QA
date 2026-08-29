namespace Academy.Agent.Media;

public interface IRecordingService
{
    event EventHandler<RecordingCompletedEventArgs>? RecordingCompleted;
    event EventHandler<RecordingFailedEventArgs>? RecordingFailed;
    event EventHandler<TeacherAudioCoverageChangedEventArgs>? TeacherAudioCoverageChanged;

    Task StartAsync(string outputPath, RecordingOptions? options = null, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
