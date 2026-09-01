namespace Academy.Agent.Media;

public sealed class RecordingOptions
{
    public int FrameRate { get; init; } = 5;
    public int VideoCrf { get; init; } = 32;
    public string VideoPreset { get; init; } = "veryfast";
    public int VideoMaxBitrateKbps { get; init; } = 700;
    public int VideoBufferSizeKbps { get; init; } = 1400;
    public int AudioBitrateKbps { get; init; } = 64;
    public int AudioSampleRate { get; init; } = 32000;
    public int AudioChannels { get; init; } = 1;
    public int TeacherMicrophoneRetrySeconds { get; init; } = 5;
    public string FfmpegPath { get; init; } = "ffmpeg";

    // Maximum length of one recording file.
    // RecordingWorker will later use this for automatic segmentation.
    public int SegmentMinutes { get; init; } = 15;

    // Direct FFmpeg Desktop Duplication capture.
    // Avoids huge temporary BGRA video.raw files.
    public bool UseDirectFfmpegCapture { get; init; } = true;
}
