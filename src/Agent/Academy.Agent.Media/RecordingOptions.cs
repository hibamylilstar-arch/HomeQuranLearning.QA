namespace Academy.Agent.Media;

public sealed class RecordingOptions
{
    public int FrameRate { get; init; } = 5;
    public int VideoCrf { get; init; } = 23;
    public string AudioBitrate { get; init; } = "128k";
    public string FfmpegPath { get; init; } = "ffmpeg";
}