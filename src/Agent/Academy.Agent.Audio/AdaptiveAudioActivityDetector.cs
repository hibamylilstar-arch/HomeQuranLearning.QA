using System.Runtime.InteropServices;

namespace Academy.Agent.Audio;

/// <summary>
/// Lightweight adaptive detector for meaningful activity in one canonical
/// float-PCM classroom audio route.
///
/// It is deliberately not biometric speaker identification.
///
/// A steady noise floor, steady hiss or a steady tone should not become
/// attendance evidence merely because the endpoint is open.
///
/// The detector requires activity to rise materially above a recent adaptive
/// noise floor and to accumulate for a minimum duration.
/// </summary>
public sealed class AdaptiveAudioActivityDetector
{
    private const double SilenceDbfs =
        -120.0;

    private readonly int
        _expectedSamplesPerFrame;

    private readonly int
        _noiseWindowFrames;

    private readonly int
        _noiseRecalculationIntervalFrames;

    private readonly int
        _requiredMeaningfulFrames;

    private readonly Queue<double>
        _recentFrameDbfs =
            new();

    private int
        _framesSinceNoiseRecalculation;

    private int
        _meaningfulFrames;

    private bool
        _confirmed;

    public AdaptiveAudioActivityDetector(
        int sampleRate,
        int channels,
        int frameDurationMilliseconds = 20,
        double absoluteFloorDbfs = -55.0,
        double noiseMarginDb = 9.0,
        double minimumMeaningfulSeconds = 5.0,
        double noiseWindowSeconds = 2.0)
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

        if (frameDurationMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameDurationMilliseconds));
        }

        if (minimumMeaningfulSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumMeaningfulSeconds));
        }

        if (noiseWindowSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(noiseWindowSeconds));
        }

        if (noiseMarginDb <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(noiseMarginDb));
        }

        SampleRate =
            sampleRate;

        Channels =
            channels;

        FrameDurationMilliseconds =
            frameDurationMilliseconds;

        MinimumMeaningfulSeconds =
            minimumMeaningfulSeconds;

        AbsoluteFloorDbfs =
            absoluteFloorDbfs;

        NoiseMarginDb =
            noiseMarginDb;

        _expectedSamplesPerFrame =
            checked(
                sampleRate *
                frameDurationMilliseconds *
                channels /
                1000);

        if (_expectedSamplesPerFrame <= 0)
        {
            throw new InvalidOperationException(
                "Calculated PCM frame size is invalid.");
        }

        _noiseWindowFrames =
            Math.Max(
                10,
                (int)Math.Round(
                    noiseWindowSeconds *
                    1000.0 /
                    frameDurationMilliseconds));

        // Recalculate the percentile roughly ten times per noise window
        // instead of sorting on every 20 ms frame.
        _noiseRecalculationIntervalFrames =
            Math.Max(
                1,
                _noiseWindowFrames / 10);

        _requiredMeaningfulFrames =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    minimumMeaningfulSeconds *
                    1000.0 /
                    frameDurationMilliseconds));

        LastNoiseFloorDbfs =
            SilenceDbfs;

        LastThresholdDbfs =
            absoluteFloorDbfs;

        LastFrameDbfs =
            SilenceDbfs;
    }

    public int SampleRate { get; }

    public int Channels { get; }

    public int FrameDurationMilliseconds { get; }

    public double MinimumMeaningfulSeconds { get; }

    public double AbsoluteFloorDbfs { get; }

    public double NoiseMarginDb { get; }

    public double LastFrameDbfs { get; private set; }

    public double LastNoiseFloorDbfs { get; private set; }

    public double LastThresholdDbfs { get; private set; }

    public bool IsConfirmed =>
        _confirmed;

    public double MeaningfulSeconds =>
        _meaningfulFrames *
        FrameDurationMilliseconds /
        1000.0;

    public bool ProcessFrame(
        ReadOnlySpan<byte> pcm)
    {
        int expectedBytes =
            checked(
                _expectedSamplesPerFrame *
                sizeof(float));

        if (pcm.Length != expectedBytes)
        {
            throw new ArgumentException(
                $"Expected {expectedBytes} PCM bytes but received {pcm.Length}.",
                nameof(pcm));
        }

        ReadOnlySpan<float> samples =
            MemoryMarshal.Cast<byte, float>(
                pcm);

        double sumSquares =
            0.0;

        int validSamples =
            0;

        foreach (float rawSample in samples)
        {
            if (!float.IsFinite(
                    rawSample))
            {
                continue;
            }

            double sample =
                Math.Clamp(
                    rawSample,
                    -1.0f,
                    1.0f);

            sumSquares +=
                sample *
                sample;

            validSamples++;
        }

        double rms =
            validSamples == 0
                ? 0.0
                : Math.Sqrt(
                    sumSquares /
                    validSamples);

        double frameDbfs =
            rms <= 0.000001
                ? SilenceDbfs
                : Math.Max(
                    SilenceDbfs,
                    20.0 *
                    Math.Log10(
                        rms));

        LastFrameDbfs =
            frameDbfs;

        _recentFrameDbfs.Enqueue(
            frameDbfs);

        while (
            _recentFrameDbfs.Count >
            _noiseWindowFrames)
        {
            _recentFrameDbfs.Dequeue();
        }

        // Warm-up is deliberately observation-only. This prevents startup
        // noise, endpoint activation or a short tone from immediately
        // becoming attendance evidence.
        if (
            _recentFrameDbfs.Count <
            _noiseWindowFrames)
        {
            return _confirmed;
        }

        _framesSinceNoiseRecalculation++;

        if (
            _framesSinceNoiseRecalculation >=
                _noiseRecalculationIntervalFrames ||
            LastNoiseFloorDbfs <=
                SilenceDbfs)
        {
            LastNoiseFloorDbfs =
                CalculateNoiseFloor(
                    _recentFrameDbfs);

            _framesSinceNoiseRecalculation =
                0;
        }

        LastThresholdDbfs =
            Math.Max(
                AbsoluteFloorDbfs,
                LastNoiseFloorDbfs +
                NoiseMarginDb);

        bool meaningfulFrame =
            frameDbfs >=
            LastThresholdDbfs;

        if (meaningfulFrame)
        {
            _meaningfulFrames++;
        }

        if (
            !_confirmed &&
            _meaningfulFrames >=
                _requiredMeaningfulFrames)
        {
            _confirmed =
                true;
        }

        return _confirmed;
    }

    private static double CalculateNoiseFloor(
        IEnumerable<double> values)
    {
        double[] ordered =
            values
                .OrderBy(x => x)
                .ToArray();

        if (ordered.Length == 0)
        {
            return SilenceDbfs;
        }

        // Low percentile approximates the stable background/noise floor
        // while allowing speech syllables to sit well above it.
        int index =
            (int)Math.Floor(
                (ordered.Length - 1) *
                0.20);

        return ordered[
            Math.Clamp(
                index,
                0,
                ordered.Length - 1)];
    }
}
