using Academy.Agent.Cloud;
using Academy.Agent.Teams;

namespace Academy.Agent.Service;

public sealed class TeamsObservationTargetState
{
    private readonly object _sync =
        new();

    private TeamsObservationTarget? _current;

    private TeamsObservationTarget? _lessonGrace;

    public void Set(
        AgentClassWindowItem item)
    {
        SetCurrent(item);
    }

    public void SetCurrent(
        AgentClassWindowItem item)
    {
        ArgumentNullException.ThrowIfNull(
            item);

        TeamsObservationTarget target =
            Map(item);

        lock (_sync)
        {
            _current =
                target;
        }
    }

    public void SetLessonGrace(
        AgentClassWindowItem? item)
    {
        TeamsObservationTarget? target =
            item is null
                ? null
                : Map(item);

        lock (_sync)
        {
            _lessonGrace =
                target;
        }
    }

    public void Clear()
    {
        ClearCurrent();
    }

    public void ClearCurrent()
    {
        lock (_sync)
        {
            _current =
                null;
        }
    }

    public void ClearLessonGrace()
    {
        lock (_sync)
        {
            _lessonGrace =
                null;
        }
    }

    public TeamsObservationTarget? GetCurrent()
    {
        lock (_sync)
        {
            return _current;
        }
    }

    public TeamsObservationTarget? GetLessonGrace()
    {
        lock (_sync)
        {
            return _lessonGrace;
        }
    }

    private static TeamsObservationTarget Map(
        AgentClassWindowItem item)
    {
        return new TeamsObservationTarget
        {
            SessionId =
                item.SessionId,

            ScheduleId =
                item.ScheduleId,

            DeviceId =
                item.DeviceId,

            TeacherId =
                item.TeacherId,

            TeacherFullName =
                item.TeacherFullName,

            StudentId =
                item.StudentId,

            StudentFullName =
                item.StudentFullName,

            CourseId =
                item.CourseId,

            CourseName =
                item.CourseName,

            ScheduledStartUtc =
                item.ScheduledStartUtc,

            ScheduledEndUtc =
                item.ScheduledEndUtc
        };
    }
}
