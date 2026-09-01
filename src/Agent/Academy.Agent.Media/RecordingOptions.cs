namespace Academy.Agent.Media;

public sealed class RecordingOptions
{
    public int FrameRate { get; init; } = 5;
    public int VideoCrf { get; init; } = 35;
    public string VideoPreset { get; init; } = "ultrafast";
    public int VideoMaxBitrateKbps { get; init; } = 250;
    public int VideoBufferSizeKbps { get; init; } = 500;
    public int AudioBitrateKbps { get; init; } = 64;
    public int AudioSampleRate { get; init; } = 48000;
    public int AudioChannels { get; init; } = 1;
    public int TeacherMicrophoneRetrySeconds { get; init; } = 1;
    public string FfmpegPath { get; init; } = "ffmpeg";

    // Maximum length of one recording file.
    // RecordingWorker will later use this for automatic segmentation.
    public int SegmentMinutes { get; init; } = 15;

    // Direct FFmpeg Desktop Duplication capture.
    // Avoids huge temporary BGRA video.raw files.
    public bool UseDirectFfmpegCapture { get; init; } = true;
}
