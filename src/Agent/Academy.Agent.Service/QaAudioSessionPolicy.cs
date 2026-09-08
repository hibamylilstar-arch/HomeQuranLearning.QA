using Academy.Agent.Teams;

namespace Academy.Agent.Service;

public static class QaAudioSessionPolicy
{
    /// <summary>
    /// Direct QA capture is allowed only while the backend-authoritative
    /// Session is Live and wall clock remains inside its scheduled window.
    ///
    /// Scheduled sessions are intentionally rejected here as well as by
    /// the backend. Completed sessions may still accept already-produced
    /// chunk retries server-side, but the Agent must not capture new audio
    /// for them.
    /// </summary>
    public static bool IsEligible(
        TeamsObservationTarget? target,
        DateTimeOffset nowUtc)
    {
        return
            target is not null &&
            string.Equals(
                target.Status,
                "Live",
                StringComparison.OrdinalIgnoreCase) &&
            nowUtc >=
                target.ScheduledStartUtc &&
            nowUtc <
                target.ScheduledEndUtc;
    }

    public static bool IsSameEligibleSession(
        TeamsObservationTarget? target,
        Guid sessionId,
        DateTimeOffset nowUtc)
    {
        return
            target is not null &&
            target.SessionId ==
                sessionId &&
            IsEligible(
                target,
                nowUtc);
    }
}
