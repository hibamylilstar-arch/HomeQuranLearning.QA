using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Academy.Agent.Audio;

public static class QaCanonicalAudioEncoder
{
    public const int SourceSampleRate =
        48000;

    public const int SourceSamplesPerFrame =
        960;

    public const int OutputSampleRate =
        16000;

    public const int OutputChannels =
        1;

    public const int OutputBitsPerSample =
        16;

    public const int OutputSamplesPerFrame =
        320;

    public const int ExpectedSystemFrameBytes =
        SourceSamplesPerFrame * 2 * 4;

    public const int ExpectedTeacherFrameBytes =
        SourceSamplesPerFrame * 4;

    /// <summary>
    /// Converts one canonical 20 ms Hub frame to 16 kHz mono PCM16.
    ///
    /// System stereo is downmixed to mono, combined equally with the
    /// teacher microphone, then reduced 48 kHz -> 16 kHz using a
    /// deterministic 3-sample box average before PCM16 quantization.
    ///
    /// This performs no noise gate, VAD or dynamic normalization.
    /// Evidence remains faithful; STT normalization can happen later
    /// without changing what the Owner hears in the evidence player.
    /// </summary>
    public static int ConvertFrame(
        ReadOnlySpan<byte> systemPcm,
        ReadOnlySpan<byte> teacherPcm,
        Span<short> destination)
    {
        if (systemPcm.Length !=
            ExpectedSystemFrameBytes)
        {
            throw new ArgumentException(
                $"System PCM must contain exactly {ExpectedSystemFrameBytes} bytes.",
                nameof(systemPcm));
        }

        if (teacherPcm.Length !=
            ExpectedTeacherFrameBytes)
        {
            throw new ArgumentException(
                $"Teacher PCM must contain exactly {ExpectedTeacherFrameBytes} bytes.",
                nameof(teacherPcm));
        }

        if (destination.Length <
            OutputSamplesPerFrame)
        {
            throw new ArgumentException(
                $"Destination must contain at least {OutputSamplesPerFrame} samples.",
                nameof(destination));
        }

        ReadOnlySpan<float> system =
            MemoryMarshal.Cast<byte, float>(
                systemPcm);

        ReadOnlySpan<float> teacher =
            MemoryMarshal.Cast<byte, float>(
                teacherPcm);

        for (
            int outputIndex = 0;
            outputIndex <
                OutputSamplesPerFrame;
            outputIndex++)
        {
            int sourceBase =
                outputIndex * 3;

            float sum =
                0.0f;

            for (
                int offset = 0;
                offset < 3;
                offset++)
            {
                int sourceIndex =
                    sourceBase + offset;

                float left =
                    Sanitize(
                        system[
                            sourceIndex * 2]);

                float right =
                    Sanitize(
                        system[
                            sourceIndex * 2 + 1]);

                float teacherSample =
                    Sanitize(
                        teacher[sourceIndex]);

                float systemMono =
                    (left + right) *
                    0.5f;

                float mixed =
                    (systemMono +
                        teacherSample) *
                    0.5f;

                sum +=
                    mixed;
            }

            float sample =
                Math.Clamp(
                    sum / 3.0f,
                    -1.0f,
                    1.0f);

            int scaled =
                (int)MathF.Round(
                    sample *
                    short.MaxValue);

            destination[outputIndex] =
                (short)Math.Clamp(
                    scaled,
                    short.MinValue,
                    short.MaxValue);
        }

        return
            OutputSamplesPerFrame;
    }

    public static byte[] CreateWave(
        ReadOnlySpan<short> pcm)
    {
        if (pcm.Length == 0)
        {
            throw new ArgumentException(
                "PCM must contain at least one sample.",
                nameof(pcm));
        }

        int dataBytes =
            checked(
                pcm.Length *
                sizeof(short));

        byte[] wave =
            new byte[
                checked(
                    44 +
                    dataBytes)];

        Encoding.ASCII
            .GetBytes("RIFF")
            .CopyTo(
                wave,
                0);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(4, 4),
                36 + dataBytes);

        Encoding.ASCII
            .GetBytes("WAVE")
            .CopyTo(
                wave,
                8);

        Encoding.ASCII
            .GetBytes("fmt ")
            .CopyTo(
                wave,
                12);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(16, 4),
                16);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                wave.AsSpan(20, 2),
                1);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                wave.AsSpan(22, 2),
                OutputChannels);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(24, 4),
                OutputSampleRate);

        int byteRate =
            OutputSampleRate *
            OutputChannels *
            (OutputBitsPerSample / 8);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(28, 4),
                byteRate);

        short blockAlign =
            (short)(
                OutputChannels *
                (OutputBitsPerSample / 8));

        BinaryPrimitives
            .WriteInt16LittleEndian(
                wave.AsSpan(32, 2),
                blockAlign);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                wave.AsSpan(34, 2),
                OutputBitsPerSample);

        Encoding.ASCII
            .GetBytes("data")
            .CopyTo(
                wave,
                36);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(40, 4),
                dataBytes);

        Span<byte> audioBytes =
            wave.AsSpan(
                44,
                dataBytes);

        for (
            int i = 0;
            i < pcm.Length;
            i++)
        {
            BinaryPrimitives
                .WriteInt16LittleEndian(
                    audioBytes.Slice(
                        i * 2,
                        2),
                    pcm[i]);
        }

        return
            wave;
    }

    private static float Sanitize(
        float sample)
    {
        return
            float.IsNaN(sample) ||
            float.IsInfinity(sample)
                ? 0.0f
                : sample;
    }
}
