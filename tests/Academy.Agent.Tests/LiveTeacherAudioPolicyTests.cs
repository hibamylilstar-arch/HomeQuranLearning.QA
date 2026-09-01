using Academy.Agent.Service;

namespace Academy.Agent.Tests;

public sealed class LiveTeacherAudioPolicyTests
{
    [Fact]
    public void LiveFilter_MixesSystemAndTeacherInputs()
    {
        string filter =
            LiveTeacherAudioPolicy.BuildFilterComplex();

        Assert.Contains(
            "[1:a]",
            filter,
            StringComparison.Ordinal);

        Assert.Contains(
            "[2:a]",
            filter,
            StringComparison.Ordinal);

        Assert.Contains(
            "amix=inputs=2",
            filter,
            StringComparison.Ordinal);

        Assert.Contains(
            "duration=longest",
            filter,
            StringComparison.Ordinal);

        Assert.EndsWith(
            "[live_audio]",
            filter,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LiveFilter_NormalizesBothAudioInputsBeforeMixing()
    {
        string filter =
            LiveTeacherAudioPolicy.BuildFilterComplex();

        Assert.Contains(
            "[1:a]asetpts=PTS-STARTPTS,",
            filter,
            StringComparison.Ordinal);

        Assert.Contains(
            "[2:a]asetpts=PTS-STARTPTS,",
            filter,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TeacherCapture_RetriesAtFiveSeconds()
    {
        DateTimeOffset now =
            new(
                2026,
                9,
                1,
                2,
                30,
                0,
                TimeSpan.Zero);

        Assert.True(
            LiveTeacherAudioPolicy.ShouldRetryCapture(
                DateTimeOffset.MinValue,
                now));

        Assert.False(
            LiveTeacherAudioPolicy.ShouldRetryCapture(
                now - TimeSpan.FromSeconds(4),
                now));

        Assert.True(
            LiveTeacherAudioPolicy.ShouldRetryCapture(
                now - TimeSpan.FromSeconds(5),
                now));
    }

    [Fact]
    public void TeacherLivePolicy_HasStableFormatAndMissingStatus()
    {
        Assert.Equal(
            48000,
            LiveTeacherAudioPolicy.TeacherSampleRate);

        Assert.Equal(
            1,
            LiveTeacherAudioPolicy.TeacherChannels);

        Assert.Equal(
            32,
            LiveTeacherAudioPolicy.TeacherBitsPerSample);

        Assert.Equal(
            "Teacher Mic Missing",
            LiveTeacherAudioPolicy.MissingStatus);
    }
}