namespace Academy.Application.Contracts;

public sealed class DeviceTeacherInfoDto
{
    public Guid TeacherId { get; init; }

    public string TeacherFullName { get; init; } = string.Empty;
}