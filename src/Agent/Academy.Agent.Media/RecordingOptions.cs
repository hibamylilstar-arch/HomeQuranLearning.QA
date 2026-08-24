namespace Academy.Agent.Media;

public sealed class RecordingOptions
{
    public int FrameRate { get; init; } = 5;
    public int VideoCrf { get; init; } = 23;
    public string AudioBitrate { get; init; } = "128k";
    public string FfmpegPath { get; init; } = "ffmpeg";

    // Maximum length of one recording file.
    // RecordingWorker will later use this for automatic segmentation.
    public int SegmentMinutes { get; init; } = 15;

    // Direct FFmpeg Desktop Duplication capture.
    // Avoids huge temporary BGRA video.raw files.
    public bool UseDirectFfmpegCapture { get; init; } = true;
}
