using System.Buffers.Binary;

namespace Academy.Agent.Service;

public static class QaTeacherPcm16kConverter
{
    public const int InputBytesPerFrame = 960 * sizeof(float);
    public const int OutputSamplesPerFrame = 320;

    public static short[] ConvertFrame(ReadOnlySpan<byte> teacherPcm)
    {
        if (teacherPcm.Length != InputBytesPerFrame)
            throw new ArgumentException($"Expected {InputBytesPerFrame} bytes.", nameof(teacherPcm));

        short[] output = new short[OutputSamplesPerFrame];

        for (int i = 0; i < output.Length; i++)
        {
            int sample = i * 3;
            float a = ReadFloat(teacherPcm, sample);
            float b = ReadFloat(teacherPcm, sample + 1);
            float c = ReadFloat(teacherPcm, sample + 2);
            float value = (a + b + c) / 3f;

            if (!float.IsFinite(value))
                value = 0f;

            value = Math.Clamp(value, -1f, 1f);
            output[i] = (short)MathF.Round(value * short.MaxValue);
        }

        return output;
    }

    private static float ReadFloat(ReadOnlySpan<byte> pcm, int sampleIndex)
    {
        int bits = BinaryPrimitives.ReadInt32LittleEndian(
            pcm.Slice(sampleIndex * sizeof(float), sizeof(float)));

        return BitConverter.Int32BitsToSingle(bits);
    }
}
