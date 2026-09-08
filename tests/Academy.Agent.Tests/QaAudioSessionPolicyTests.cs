using Academy.Agent.Service;
using Academy.Agent.Teams;

namespace Academy.Agent.Tests;

public sealed class QaAudioSessionPolicyTests
{
    [Theory]
    [InlineData("Scheduled", false)]
    [InlineData("Live", true)]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    public void IsEligible_RequiresLiveStatus(
        string status,
        bool expected)
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        TeamsObservationTarget target =
            CreateTarget(
                status,
                now.AddMinutes(-1),
                now.AddMinutes(1));

        Assert.Equal(
            expected,
            QaAudioSessionPolicy
                .IsEligible(
                    target,
                    now));
    }

    [Fact]
    public void IsEligible_UsesStartInclusiveEndExclusiveWindow()
    {
        DateTimeOffset start =
            DateTimeOffset.UtcNow;

        DateTimeOffset end =
            start.AddMinutes(30);

        TeamsObservationTarget target =
            CreateTarget(
                "Live",
                start,
                end);

        Assert.False(
            QaAudioSessionPolicy
                .IsEligible(
                    target,
                    start.AddTicks(-1)));

        Assert.True(
            QaAudioSessionPolicy
                .IsEligible(
                    target,
                    start));

        Assert.False(
            QaAudioSessionPolicy
                .IsEligible(
                    target,
                    end));
    }

    [Fact]
    public void IsEligible_NullTargetIsRejected()
    {
        Assert.False(
            QaAudioSessionPolicy
                .IsEligible(
                    null,
                    DateTimeOffset.UtcNow));
    }

    private static TeamsObservationTarget
        CreateTarget(
            string status,
            DateTimeOffset start,
            DateTimeOffset end)
    {
        return
            new TeamsObservationTarget
            {
                SessionId =
                    Guid.NewGuid(),

                ScheduledStartUtc =
                    start,

                ScheduledEndUtc =
                    end,

                Status =
                    status
            };
    }
}
