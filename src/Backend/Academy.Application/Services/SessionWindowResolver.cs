using Academy.Domain.Entities;

namespace Academy.Application.Services;

internal static class SessionWindowResolver
{
    public static (
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc)
        Resolve(
            Session session)
    {
        ArgumentNullException.ThrowIfNull(
            session);

        DateTimeOffset startUtc =
            IsInfinitySentinel(
                session.ScheduledStartUtc)
                ? session.StartedAtUtc
                : session.ScheduledStartUtc;

        DateTimeOffset observedEndUtc =
            session.EndedAtUtc is
                DateTimeOffset endedAtUtc &&
            !IsInfinitySentinel(
                endedAtUtc)
                ? endedAtUtc
                : startUtc;

        DateTimeOffset endUtc =
            IsInfinitySentinel(
                session.ScheduledEndUtc)
                ? observedEndUtc
                : session.ScheduledEndUtc;

        if (endUtc < startUtc)
        {
            endUtc =
                startUtc;
        }

        return (
            startUtc,
            endUtc);
    }

    public static DateTimeOffset AddMinutesClamped(
        DateTimeOffset value,
        double minutes)
    {
        try
        {
            return value.AddMinutes(
                minutes);
        }
        catch (ArgumentOutOfRangeException)
        {
            return minutes < 0
                ? DateTimeOffset.MinValue
                : DateTimeOffset.MaxValue;
        }
    }

    private static bool IsInfinitySentinel(
        DateTimeOffset value)
    {
        return
            value == DateTimeOffset.MinValue ||
            value == DateTimeOffset.MaxValue;
    }
}
