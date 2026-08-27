using Academy.Agent.Cloud;
using Academy.Agent.Teams;

namespace Academy.Agent.Service;

public sealed class TeamsObservationTargetState
{
    private readonly object _sync =
        new();

    private TeamsObservationTarget? _current;

    public void Set(
        AgentClassWindowItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var target =
            new TeamsObservationTarget
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

        lock (_sync)
        {
            _current =
                target;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _current =
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
}