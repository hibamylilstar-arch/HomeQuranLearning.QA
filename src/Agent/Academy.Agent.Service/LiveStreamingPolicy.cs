namespace Academy.Agent.Service;

public static class LiveStreamingPolicy
{
    public static readonly TimeSpan RealAudioHoldoff =
        TimeSpan.FromMilliseconds(250);

    public static bool NeedsPipelineRestart(
        string? currentStreamKey,
        string requestedStreamKey,
        bool ffmpegNotRunning,
        bool videoCaptureFailed)
    {
        return
            !string.Equals(
                currentStreamKey,
                requestedStreamKey,
                StringComparison.Ordinal) ||
            ffmpegNotRunning ||
            videoCaptureFailed;
    }

    public static bool ShouldSendSilence(
        DateTimeOffset lastRealAudioPacketUtc,
        DateTimeOffset nowUtc)
    {
        return
            lastRealAudioPacketUtc == DateTimeOffset.MinValue ||
            nowUtc - lastRealAudioPacketUtc >= RealAudioHoldoff;
    }

    public static int CalculateSilenceChunkBytes(
        int sampleRate,
        int channels,
        int bitsPerSample,
        int intervalMilliseconds = 50)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleRate));
        }

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(channels));
        }

        if (bitsPerSample <= 0 ||
            bitsPerSample % 8 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitsPerSample));
        }

        if (intervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMilliseconds));
        }

        int bytesPerFrame =
            checked(channels * (bitsPerSample / 8));

        long frames =
            checked(
                (long)sampleRate *
                intervalMilliseconds /
                1000L);

        if (frames <= 0)
        {
            frames = 1;
        }

        long bytes =
            checked(frames * bytesPerFrame);

        if (bytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMilliseconds));
        }

        return (int)bytes;
    }
}
