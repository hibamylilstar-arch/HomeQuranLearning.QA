using Academy.Agent.Media;

namespace Academy.Agent.Tests;

public sealed class RecordingServiceTests
{
    [Fact]
    public void BuildFfmpegArguments_UsesBoundedStorageProfile()
    {
        string arguments =
            RecordingService.BuildFfmpegArguments(
                @"C:\Recordings\test.mp4",
                new RecordingOptions(),
                "f32le",
                inputAudioSampleRate: 48000,
                inputAudioChannels: 2);

        Assert.Contains("-preset veryfast", arguments);
        Assert.Contains("-crf 32", arguments);
        Assert.Contains("-maxrate 700k", arguments);
        Assert.Contains("-bufsize 1400k", arguments);
        Assert.Contains("-b:a 64k", arguments);
        Assert.Contains("-ar 32000 -ac 1", arguments);
        Assert.DoesNotContain("-preset ultrafast", arguments);
        Assert.DoesNotContain("-bf 0", arguments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void BuildFfmpegArguments_RejectsInvalidFrameRate(
        int frameRate)
    {
        var options =
            new RecordingOptions
            {
                FrameRate = frameRate
            };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => RecordingService.BuildFfmpegArguments(
                @"C:\Recordings\test.mp4",
                options,
                "s16le",
                inputAudioSampleRate: 48000,
                inputAudioChannels: 2));
    }

    [Fact]
    public void BuildFfmpegArguments_RejectsUnsupportedPreset()
    {
        var options =
            new RecordingOptions
            {
                VideoPreset = "veryfast -f data"
            };

        Assert.Throws<ArgumentException>(
            () => RecordingService.BuildFfmpegArguments(
                @"C:\Recordings\test.mp4",
                options,
                "s16le",
                inputAudioSampleRate: 48000,
                inputAudioChannels: 2));
    }
}
