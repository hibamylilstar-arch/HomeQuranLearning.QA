namespace Academy.Agent.Cloud;

public sealed class AgentClassWindowResponse
{
    public DateTimeOffset ServerTimeUtc { get; init; }

    public AgentClassWindowItem? Current { get; init; }

    public AgentClassWindowItem? Next { get; init; }
}

public sealed class AgentClassWindowItem
{
    public Guid SessionId { get; init; }

    public Guid? ScheduleId { get; init; }

    public Guid TeacherId { get; init; }

    public string TeacherFullName { get; init; } = string.Empty;

    public Guid StudentId { get; init; }

    public string StudentFullName { get; init; } = string.Empty;

    public Guid CourseId { get; init; }

    public string CourseName { get; init; } = string.Empty;

    public Guid DeviceId { get; init; }

    public DateTimeOffset ScheduledStartUtc { get; init; }

    public DateTimeOffset ScheduledEndUtc { get; init; }

    public string Status { get; init; } = string.Empty;
}
