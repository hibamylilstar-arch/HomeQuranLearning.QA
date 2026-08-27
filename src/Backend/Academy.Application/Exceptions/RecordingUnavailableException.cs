using Academy.Domain.Enums;

namespace Academy.Application.Exceptions;

public sealed class RecordingUnavailableException : InvalidOperationException
{
    public RecordingUnavailableException(RecordingStatus status)
        : base($"Recording is not available for playback because its status is {status}.")
    {
        Status = status;
    }

    public RecordingStatus Status { get; }
}
