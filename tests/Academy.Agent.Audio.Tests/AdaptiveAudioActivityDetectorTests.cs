using Academy.Agent.Audio;

namespace Academy.Agent.Audio.Tests;

public sealed class AdaptiveAudioActivityDetectorTests
{
    [Fact]
    public void ConfiguredFloorAndMargin_AreActuallyUsedByThreshold()
    {
        var detector =
            CreateDetector();

        byte[] silence =
            FloatFrame(
                amplitude:
                    0.0f);

        for (int i = 0;
             i < 150;
             i++)
        {
            detector.ProcessFrame(
                silence);
        }

        Assert.Equal(
            -55.0,
            detector.AbsoluteFloorDbfs,
            precision:
                6);

        Assert.Equal(
            9.0,
            detector.NoiseMarginDb,
            precision:
                6);

        Assert.Equal(
            -55.0,
            detector.LastThresholdDbfs,
            precision:
                6);
    }

    [Fact]
    public void Silence_DoesNotBecomeMeaningfulActivity()
    {
        var detector =
            CreateDetector();

        byte[] frame =
            FloatFrame(
                amplitude:
                    0.0f);

        for (int i = 0;
             i < 1000;
             i++)
        {
            detector.ProcessFrame(
                frame);
        }

        Assert.False(
            detector.IsConfirmed);

        Assert.Equal(
            0.0,
            detector.MeaningfulSeconds);
    }

    [Fact]
    public void SteadyNoiseFloor_DoesNotBecomeMeaningfulActivity()
    {
        var detector =
            CreateDetector();

        var random =
            new Random(
                12345);

        for (int frameIndex = 0;
             frameIndex < 1000;
             frameIndex++)
        {
            byte[] frame =
                RandomNoiseFrame(
                    random,
                    amplitude:
                        0.012f);

            detector.ProcessFrame(
                frame);
        }

        Assert.False(
            detector.IsConfirmed);
    }

    [Fact]
    public void SteadyTone_DoesNotBecomeMeaningfulActivity()
    {
        var detector =
            CreateDetector();

        for (int frameIndex = 0;
             frameIndex < 1000;
             frameIndex++)
        {
            byte[] frame =
                SineFrame(
                    amplitude:
                        0.20f,
                    frequencyHz:
                        440.0,
                    phaseFrame:
                        frameIndex);

            detector.ProcessFrame(
                frame);
        }

        Assert.False(
            detector.IsConfirmed);
    }

    [Fact]
    public void SpeechLikeBursts_ConfirmAfterMinimumCumulativeActivity()
    {
        var detector =
            CreateDetector();

        byte[] silence =
            FloatFrame(
                amplitude:
                    0.0f);

        // Warm the adaptive noise floor with silence.
        for (int i = 0;
             i < 150;
             i++)
        {
            detector.ProcessFrame(
                silence);
        }

        for (int cycle = 0;
             cycle < 45;
             cycle++)
        {
            // 200 ms high-energy speech-like activity.
            for (int active = 0;
                 active < 10;
                 active++)
            {
                detector.ProcessFrame(
                    SineFrame(
                        amplitude:
                            0.20f,
                        frequencyHz:
                            260.0 +
                            (cycle % 5) * 40.0,
                        phaseFrame:
                            cycle * 10 +
                            active));
            }

            // 100 ms pause keeps the signal speech-like rather than a
            // continuous tone.
            for (int pause = 0;
                 pause < 5;
                 pause++)
            {
                detector.ProcessFrame(
                    silence);
            }
        }

        Assert.True(
            detector.IsConfirmed);

        Assert.True(
            detector.MeaningfulSeconds >=
                5.0);
    }

    [Fact]
    public void SpeechAboveStableHiss_StillConfirms()
    {
        var detector =
            CreateDetector();

        var random =
            new Random(
                98765);

        for (int i = 0;
             i < 150;
             i++)
        {
            detector.ProcessFrame(
                RandomNoiseFrame(
                    random,
                    amplitude:
                        0.010f));
        }

        for (int cycle = 0;
             cycle < 50;
             cycle++)
        {
            for (int active = 0;
                 active < 10;
                 active++)
            {
                detector.ProcessFrame(
                    SpeechPlusNoiseFrame(
                        random,
                        speechAmplitude:
                            0.18f,
                        noiseAmplitude:
                            0.010f,
                        frequencyHz:
                            300.0 +
                            (cycle % 4) * 55.0,
                        phaseFrame:
                            cycle * 10 +
                            active));
            }

            for (int pause = 0;
                 pause < 5;
                 pause++)
            {
                detector.ProcessFrame(
                    RandomNoiseFrame(
                        random,
                        amplitude:
                            0.010f));
            }
        }

        Assert.True(
            detector.IsConfirmed);
    }

    private static AdaptiveAudioActivityDetector
        CreateDetector()
    {
        return new AdaptiveAudioActivityDetector(
            sampleRate:
                48000,
            channels:
                1,
            frameDurationMilliseconds:
                20,
            absoluteFloorDbfs:
                -55.0,
            noiseMarginDb:
                9.0,
            minimumMeaningfulSeconds:
                5.0,
            noiseWindowSeconds:
                2.0);
    }

    private static byte[] FloatFrame(
        float amplitude)
    {
        const int samples =
            960;

        float[] values =
            Enumerable
                .Repeat(
                    amplitude,
                    samples)
                .ToArray();

        return ToBytes(
            values);
    }

    private static byte[] RandomNoiseFrame(
        Random random,
        float amplitude)
    {
        const int samples =
            960;

        float[] values =
            new float[samples];

        for (int i = 0;
             i < values.Length;
             i++)
        {
            values[i] =
                (
                    (float)random.NextDouble() *
                    2.0f -
                    1.0f
                ) *
                amplitude;
        }

        return ToBytes(
            values);
    }

    private static byte[] SineFrame(
        float amplitude,
        double frequencyHz,
        int phaseFrame)
    {
        const int sampleRate =
            48000;

        const int samples =
            960;

        float[] values =
            new float[samples];

        long sampleOffset =
            (long)phaseFrame *
            samples;

        for (int i = 0;
             i < samples;
             i++)
        {
            double phase =
                2.0 *
                Math.PI *
                frequencyHz *
                (sampleOffset + i) /
                sampleRate;

            values[i] =
                amplitude *
                (float)Math.Sin(
                    phase);
        }

        return ToBytes(
            values);
    }

    private static byte[] SpeechPlusNoiseFrame(
        Random random,
        float speechAmplitude,
        float noiseAmplitude,
        double frequencyHz,
        int phaseFrame)
    {
        const int sampleRate =
            48000;

        const int samples =
            960;

        float[] values =
            new float[samples];

        long sampleOffset =
            (long)phaseFrame *
            samples;

        for (int i = 0;
             i < samples;
             i++)
        {
            double phase =
                2.0 *
                Math.PI *
                frequencyHz *
                (sampleOffset + i) /
                sampleRate;

            float speech =
                speechAmplitude *
                (float)Math.Sin(
                    phase);

            float noise =
                (
                    (float)random.NextDouble() *
                    2.0f -
                    1.0f
                ) *
                noiseAmplitude;

            values[i] =
                Math.Clamp(
                    speech + noise,
                    -1.0f,
                    1.0f);
        }

        return ToBytes(
            values);
    }

    private static byte[] ToBytes(
        float[] values)
    {
        byte[] bytes =
            new byte[
                values.Length *
                sizeof(float)];

        Buffer.BlockCopy(
            values,
            0,
            bytes,
            0,
            bytes.Length);

        return bytes;
    }
}
