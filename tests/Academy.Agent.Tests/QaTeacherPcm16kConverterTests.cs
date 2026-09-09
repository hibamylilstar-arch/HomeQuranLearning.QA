using Academy.Agent.Service;

namespace Academy.Agent.Tests;

public sealed class QaTeacherPcm16kConverterTests
{
    [Fact]
    public void Silence_Produces320ZeroSamples()
    {
        byte[] input = new byte[QaTeacherPcm16kConverter.InputBytesPerFrame];
        short[] output = QaTeacherPcm16kConverter.ConvertFrame(input);

        Assert.Equal(320, output.Length);
        Assert.All(output, x => Assert.Equal((short)0, x));
    }

    [Fact]
    public void DownsamplesByAveragingEachThreeSamples()
    {
        float[] samples = new float[960];
        samples[0] = 0.3f;
        samples[1] = 0.6f;
        samples[2] = 0.9f;

        byte[] input = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, input, 0, input.Length);

        short[] output = QaTeacherPcm16kConverter.ConvertFrame(input);

        Assert.Equal((short)MathF.Round(0.6f * short.MaxValue), output[0]);
        Assert.All(output.Skip(1), x => Assert.Equal((short)0, x));
    }

    [Fact]
    public void RejectsWrongFrameSize()
    {
        Assert.Throws<ArgumentException>(
            () => QaTeacherPcm16kConverter.ConvertFrame(new byte[1]));
    }
}
