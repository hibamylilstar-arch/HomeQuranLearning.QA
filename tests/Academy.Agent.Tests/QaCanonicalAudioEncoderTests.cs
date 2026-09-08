using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Academy.Agent.Audio;

namespace Academy.Agent.Tests;

public sealed class QaCanonicalAudioEncoderTests
{
    [Fact]
    public void ConvertFrame_MixesAndDownsamplesCanonicalSources()
    {
        float[] system =
            new float[
                QaCanonicalAudioEncoder
                    .SourceSamplesPerFrame *
                2];

        float[] teacher =
            new float[
                QaCanonicalAudioEncoder
                    .SourceSamplesPerFrame];

        for (
            int i = 0;
            i <
                QaCanonicalAudioEncoder
                    .SourceSamplesPerFrame;
            i++)
        {
            system[i * 2] =
                0.6f;

            system[i * 2 + 1] =
                0.2f;

            teacher[i] =
                0.2f;
        }

        byte[] systemBytes =
            MemoryMarshal
                .AsBytes(
                    system.AsSpan())
                .ToArray();

        byte[] teacherBytes =
            MemoryMarshal
                .AsBytes(
                    teacher.AsSpan())
                .ToArray();

        short[] output =
            new short[
                QaCanonicalAudioEncoder
                    .OutputSamplesPerFrame];

        int written =
            QaCanonicalAudioEncoder
                .ConvertFrame(
                    systemBytes,
                    teacherBytes,
                    output);

        Assert.Equal(
            QaCanonicalAudioEncoder
                .OutputSamplesPerFrame,
            written);

        int expected =
            (int)MathF.Round(
                0.3f *
                short.MaxValue);

        Assert.All(
            output,
            sample =>
                Assert.InRange(
                    sample,
                    (short)(expected - 1),
                    (short)(expected + 1)));
    }

    [Fact]
    public void CreateWave_FiveSecondsHasCanonicalWaveContract()
    {
        short[] pcm =
            new short[
                QaCanonicalAudioEncoder
                    .OutputSampleRate *
                5];

        byte[] wave =
            QaCanonicalAudioEncoder
                .CreateWave(
                    pcm);

        Assert.Equal(
            44 +
                (
                    QaCanonicalAudioEncoder
                        .OutputSampleRate *
                    5 *
                    2
                ),
            wave.Length);

        Assert.Equal(
            "RIFF",
            Encoding.ASCII
                .GetString(
                    wave,
                    0,
                    4));

        Assert.Equal(
            "WAVE",
            Encoding.ASCII
                .GetString(
                    wave,
                    8,
                    4));

        Assert.Equal(
            1,
            BinaryPrimitives
                .ReadInt16LittleEndian(
                    wave.AsSpan(
                        20,
                        2)));

        Assert.Equal(
            1,
            BinaryPrimitives
                .ReadInt16LittleEndian(
                    wave.AsSpan(
                        22,
                        2)));

        Assert.Equal(
            16000,
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    wave.AsSpan(
                        24,
                        4)));

        Assert.Equal(
            16,
            BinaryPrimitives
                .ReadInt16LittleEndian(
                    wave.AsSpan(
                        34,
                        2)));

        Assert.Equal(
            "data",
            Encoding.ASCII
                .GetString(
                    wave,
                    36,
                    4));

        Assert.Equal(
            160000,
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    wave.AsSpan(
                        40,
                        4)));
    }

    [Fact]
    public void ConvertFrame_RejectsNonCanonicalFrameSizes()
    {
        short[] output =
            new short[
                QaCanonicalAudioEncoder
                    .OutputSamplesPerFrame];

        Assert.Throws<ArgumentException>(
            () =>
                QaCanonicalAudioEncoder
                    .ConvertFrame(
                        new byte[1],
                        new byte[
                            QaCanonicalAudioEncoder
                                .ExpectedTeacherFrameBytes],
                        output));

        Assert.Throws<ArgumentException>(
            () =>
                QaCanonicalAudioEncoder
                    .ConvertFrame(
                        new byte[
                            QaCanonicalAudioEncoder
                                .ExpectedSystemFrameBytes],
                        new byte[1],
                        output));
    }

    [Fact]
    public void ConvertFrame_SanitizesInvalidFloatSamples()
    {
        float[] system =
            new float[
                QaCanonicalAudioEncoder
                    .SourceSamplesPerFrame *
                2];

        float[] teacher =
            new float[
                QaCanonicalAudioEncoder
                    .SourceSamplesPerFrame];

        system[0] =
            float.NaN;

        system[1] =
            float.PositiveInfinity;

        teacher[0] =
            float.NegativeInfinity;

        byte[] systemBytes =
            MemoryMarshal
                .AsBytes(
                    system.AsSpan())
                .ToArray();

        byte[] teacherBytes =
            MemoryMarshal
                .AsBytes(
                    teacher.AsSpan())
                .ToArray();

        short[] output =
            new short[
                QaCanonicalAudioEncoder
                    .OutputSamplesPerFrame];

        QaCanonicalAudioEncoder
            .ConvertFrame(
                systemBytes,
                teacherBytes,
                output);

        Assert.Equal(
            (short)0,
            output[0]);
    }
}
