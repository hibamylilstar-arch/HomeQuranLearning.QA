namespace Academy.Application.Contracts;

public sealed class ServerArchiveCompletedRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset EndedAtUtc { get; set; }
    public long SizeBytes { get; set; }
    public string ContainerFormat { get; set; } = "fmp4";
    public string VideoCodec { get; set; } = "h264";
    public bool VideoStreamCopyVerified { get; set; }
}

public sealed class ServerArchiveRegistrationResponse
{
    public Guid RecordingId { get; set; }
    public bool Accepted { get; set; }
    public bool AlreadyRegistered { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public Guid? TeacherId { get; set; }
    public int OverlapSessionCount { get; set; }
    public int DistinctTeacherCount { get; set; }
    public bool ManagerSafeWholeSegment { get; set; }
}

public sealed class ServerArchiveDeviceResolveRequest
{
    public string StreamKey { get; set; } = string.Empty;
}

public sealed class ServerArchiveDeviceResolveResponse
{
    public string DeviceId { get; set; } = string.Empty;
}
