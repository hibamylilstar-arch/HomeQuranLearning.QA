namespace Academy.Agent.Audio;

public readonly record struct PcmFrameFormat
{
    public PcmFrameFormat(
        int sampleRate,
        int channels,
        int bitsPerSample,
        int frameDurationMilliseconds = 20)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleRate),
                "Sample rate must be greater than zero.");
        }

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(channels),
                "Channel count must be greater than zero.");
        }

        if (bitsPerSample <= 0 || bitsPerSample % 8 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitsPerSample),
                "Bits per sample must be a positive whole-byte value.");
        }

        if (frameDurationMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameDurationMilliseconds),
                "Frame duration must be greater than zero.");
        }

        long bytesPerSample = bitsPerSample / 8;

        long numerator = checked(
            (long)sampleRate *
            channels *
            bytesPerSample *
            frameDurationMilliseconds);

        if (numerator % 1000 != 0)
        {
            throw new ArgumentException(
                "PCM format does not produce an exact whole-byte frame.");
        }

        long frameBytes = numerator / 1000;

        if (frameBytes <= 0 || frameBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameDurationMilliseconds),
                "PCM frame size is outside the supported range.");
        }

        SampleRate = sampleRate;
        Channels = channels;
        BitsPerSample = bitsPerSample;
        FrameDurationMilliseconds = frameDurationMilliseconds;
        FrameBytes = (int)frameBytes;
    }

    public int SampleRate { get; }

    public int Channels { get; }

    public int BitsPerSample { get; }

    public int FrameDurationMilliseconds { get; }

    public int FrameBytes { get; }

    public int BytesPerSample => BitsPerSample / 8;

    public TimeSpan FrameDuration =>
        TimeSpan.FromMilliseconds(FrameDurationMilliseconds);

    public static PcmFrameFormat FloatMono48k20Ms =>
        new(
            sampleRate: 48000,
            channels: 1,
            bitsPerSample: 32,
            frameDurationMilliseconds: 20);

    public static PcmFrameFormat FloatStereo48k20Ms =>
        new(
            sampleRate: 48000,
            channels: 2,
            bitsPerSample: 32,
            frameDurationMilliseconds: 20);
}