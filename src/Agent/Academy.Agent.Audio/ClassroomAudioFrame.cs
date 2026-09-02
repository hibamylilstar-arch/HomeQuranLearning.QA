namespace Academy.Agent.Audio;

/// <summary>
/// One canonical classroom-audio timeline slot.
///
/// MediaTime is derived only from SequenceNumber and the fixed frame
/// duration. It is not based on WASAPI callback arrival time.
/// </summary>
public sealed class ClassroomAudioFrame
{
    internal ClassroomAudioFrame(
        long sequenceNumber,
        TimeSpan mediaTime,
        byte[] systemPcm,
        byte[] teacherPcm)
    {
        ArgumentNullException.ThrowIfNull(systemPcm);
        ArgumentNullException.ThrowIfNull(teacherPcm);

        if (sequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber));
        }

        if (mediaTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mediaTime));
        }

        SequenceNumber = sequenceNumber;
        MediaTime = mediaTime;

        SystemPcm = systemPcm;
        TeacherPcm = teacherPcm;
    }

    public long SequenceNumber { get; }

    public TimeSpan MediaTime { get; }

    public ReadOnlyMemory<byte> SystemPcm { get; }

    public ReadOnlyMemory<byte> TeacherPcm { get; }
}