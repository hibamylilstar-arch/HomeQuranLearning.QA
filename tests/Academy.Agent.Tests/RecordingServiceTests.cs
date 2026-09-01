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
                systemAudioSampleRate: 48000,
                systemAudioChannels: 2,
                teacherAudioFormat: "f32le",
                teacherAudioSampleRate: 48000,
                teacherAudioChannels: 1);

        Assert.Contains("-preset ultrafast", arguments);
        Assert.Contains("-crf 35", arguments);
        Assert.Contains("-maxrate 250k", arguments);
        Assert.Contains("-bufsize 500k", arguments);
        Assert.Contains("-b:a 64k", arguments);
        Assert.Contains("-ar 48000 -ac 1", arguments);
        Assert.Contains("udp://127.0.0.1:5006", arguments);
        Assert.Contains("udp://127.0.0.1:5007", arguments);
        Assert.Contains("-thread_queue_size 16", arguments);
        Assert.DoesNotContain("-thread_queue_size 1024", arguments);
        Assert.Contains("buffer_size=65536&fifo_size=512&overrun_nonfatal=1", arguments);
        Assert.DoesNotContain("fifo_size=500000", arguments);
        Assert.Contains("scale=-2:240:flags=fast_bilinear", arguments);
        Assert.Contains("asplit=2[teacher_mix][teacher_qa]", arguments);
        Assert.Contains("-map 0:v:0 -map \"[mixed]\" -map \"[teacher_qa]\"", arguments);
        Assert.Contains("Academy Teacher Microphone QA v1", arguments);
        Assert.Contains("-disposition:a:0 default", arguments);
        Assert.Contains("-disposition:a:1 0", arguments);
        Assert.Contains("-tune zerolatency", arguments);
        Assert.Contains("-bf 0", arguments);
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
                systemAudioSampleRate: 48000,
                systemAudioChannels: 2,
                teacherAudioFormat: "f32le",
                teacherAudioSampleRate: 48000,
                teacherAudioChannels: 1));
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
                systemAudioSampleRate: 48000,
                systemAudioChannels: 2,
                teacherAudioFormat: "f32le",
                teacherAudioSampleRate: 48000,
                teacherAudioChannels: 1));

    }

    [Fact]
    public void BuildFfmpegArguments_RejectsInvalidTeacherAudioInput()
    {
        Assert.Throws<ArgumentException>(
            () => RecordingService.BuildFfmpegArguments(
                @"C:\Recordings\test.mp4",
                new RecordingOptions(),
                "f32le",
                systemAudioSampleRate: 48000,
                systemAudioChannels: 2,
                teacherAudioFormat: "invalid",
                teacherAudioSampleRate: 48000,
                teacherAudioChannels: 1));
    }

    [Fact]
    public void BuildTimelineFinalizationArguments_PadsBothAudioTracksToVideo()
    {
        string arguments =
            RecordingService.BuildTimelineFinalizationArguments(
                @"C:\Recordings\input.mp4",
                @"C:\Recordings\output.mp4",
                new RecordingOptions());

        Assert.Contains("[0:a:0]apad[mixed]", arguments);
        Assert.Contains("[0:a:1]apad[teacher]", arguments);
        Assert.Contains("-c:v copy", arguments);
        Assert.Contains("-shortest", arguments);
        Assert.Contains("Academy Teacher Microphone QA v1", arguments);
    }
}
