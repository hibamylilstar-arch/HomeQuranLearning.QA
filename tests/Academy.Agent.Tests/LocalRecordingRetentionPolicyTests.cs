using Academy.Agent.Media;

namespace Academy.Agent.Tests;

public sealed class LocalRecordingRetentionPolicyTests
{
    private const long Gb = 1024L * 1024L * 1024L;

    [Fact]
    public void GetWorkingPath_UsesPartMp4UntilFinalized()
    {
        Assert.Equal(
            @"C:\Recordings\Academy_Recording_1.part.mp4",
            LocalRecordingRetentionPolicy.GetWorkingPath(
                @"C:\Recordings\Academy_Recording_1.mp4"));
    }

    [Theory]
    [InlineData(@"C:\Recordings\one.mp4", true)]
    [InlineData(@"C:\Recordings\one.part.mp4", false)]
    [InlineData(@"C:\Recordings\one.finalizing.mp4", false)]
    public void IsFinalizedRecordingPath_ProtectsIncompleteFiles(
        string path,
        bool expected)
    {
        Assert.Equal(
            expected,
            LocalRecordingRetentionPolicy.IsFinalizedRecordingPath(path));
    }

    [Fact]
    public void Cleanup_DoesNotStartAboveFiveGb()
    {
        Assert.False(
            LocalRecordingRetentionPolicy.ShouldStartCleanup(6 * Gb, 5 * Gb));
    }

    [Fact]
    public void Cleanup_StartsBelowFiveGb()
    {
        Assert.True(
            LocalRecordingRetentionPolicy.ShouldStartCleanup(4 * Gb, 5 * Gb));
    }

    [Fact]
    public void Cleanup_ContinuesUntilSevenGbTarget()
    {
        Assert.True(
            LocalRecordingRetentionPolicy.ShouldContinueCleanup(6 * Gb, 7 * Gb));
        Assert.False(
            LocalRecordingRetentionPolicy.ShouldContinueCleanup(7 * Gb, 7 * Gb));
    }
}