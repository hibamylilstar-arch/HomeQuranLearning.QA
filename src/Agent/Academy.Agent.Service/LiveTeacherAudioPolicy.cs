namespace Academy.Agent.Service;

public static class LiveTeacherAudioPolicy
{
    public const int TeacherSampleRate = 48000;
    public const int TeacherChannels = 1;
    public const int TeacherBitsPerSample = 32;

    public const string MissingStatus =
        "Teacher Mic Missing";

    public static TimeSpan RetryInterval =>
        TimeSpan.FromSeconds(5);

    public static bool ShouldRetryCapture(
        DateTimeOffset lastAttemptUtc,
        DateTimeOffset nowUtc)
    {
        return
            lastAttemptUtc == DateTimeOffset.MinValue ||
            nowUtc - lastAttemptUtc >= RetryInterval;
    }

    public static string BuildFilterComplex()
    {
        return
            "[1:a]asetpts=PTS-STARTPTS," +
            "aresample=48000:async=1000:first_pts=0," +
            "aformat=sample_rates=48000:channel_layouts=stereo" +
            "[system_audio];" +
            "[2:a]asetpts=PTS-STARTPTS," +
            "aresample=48000:async=1000:first_pts=0," +
            "aformat=sample_rates=48000:channel_layouts=stereo" +
            "[teacher_audio];" +
            "[system_audio][teacher_audio]" +
            "amix=inputs=2:duration=longest:dropout_transition=0" +
            "[live_audio]";
    }
}