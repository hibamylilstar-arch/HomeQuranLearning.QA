using Academy.Agent.Service;

namespace Academy.Agent.Tests;

public sealed class LiveStreamingPolicyTests
{
    [Fact]
    public void NeedsPipelineRestart_WhenStreamKeyChanges()
    {
        Assert.True(
            LiveStreamingPolicy.NeedsPipelineRestart(
                "old-key",
                "new-key",
                ffmpegNotRunning: false,
                videoCaptureFailed: false));
    }

    [Fact]
    public void NeedsPipelineRestart_WhenPipelineIsHealthyAndKeyIsSame()
    {
        Assert.False(
            LiveStreamingPolicy.NeedsPipelineRestart(
                "same-key",
                "same-key",
                ffmpegNotRunning: false,
                videoCaptureFailed: false));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void NeedsPipelineRestart_WhenRuntimeNeedsRecovery(
        bool ffmpegNotRunning,
        bool videoCaptureFailed)
    {
        Assert.True(
            LiveStreamingPolicy.NeedsPipelineRestart(
                "same-key",
                "same-key",
                ffmpegNotRunning,
                videoCaptureFailed));
    }

    [Theory]
    [InlineData(48000, 2, 16, 9600)]
    [InlineData(48000, 2, 32, 19200)]
    public void CalculateSilenceChunkBytes_MatchesCaptureFormat(
        int sampleRate,
        int channels,
        int bitsPerSample,
        int expectedBytes)
    {
        Assert.Equal(
            expectedBytes,
            LiveStreamingPolicy.CalculateSilenceChunkBytes(
                sampleRate,
                channels,
                bitsPerSample));
    }

    [Fact]
    public void ShouldSendSilence_UsesHoldoffAroundRealAudio()
    {
        DateTimeOffset now =
            new(2026, 8, 31, 22, 0, 0, TimeSpan.Zero);

        Assert.True(
            LiveStreamingPolicy.ShouldSendSilence(
                DateTimeOffset.MinValue,
                now));

        Assert.False(
            LiveStreamingPolicy.ShouldSendSilence(
                now - TimeSpan.FromMilliseconds(100),
                now));

        Assert.True(
            LiveStreamingPolicy.ShouldSendSilence(
                now - TimeSpan.FromMilliseconds(300),
                now));
    }
}
