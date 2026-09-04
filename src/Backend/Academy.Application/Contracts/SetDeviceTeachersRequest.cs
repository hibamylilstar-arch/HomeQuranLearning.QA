namespace Academy.Application.Contracts;

public sealed class SetDeviceTeachersRequest
{
    public IReadOnlyList<Guid> TeacherIds { get; init; } =
        Array.Empty<Guid>();
}