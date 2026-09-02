using Academy.Agent.Audio;

namespace Academy.Agent.Tests;

public sealed class BoundedPcmFrameBufferTests
{
    [Fact]
    public void FloatMono48k20Ms_HasExpectedFrameSize()
    {
        PcmFrameFormat format =
            PcmFrameFormat.FloatMono48k20Ms;

        Assert.Equal(48000, format.SampleRate);
        Assert.Equal(1, format.Channels);
        Assert.Equal(32, format.BitsPerSample);
        Assert.Equal(20, format.FrameDurationMilliseconds);
        Assert.Equal(3840, format.FrameBytes);
    }

    [Fact]
    public void FloatStereo48k20Ms_HasExpectedFrameSize()
    {
        Assert.Equal(
            7680,
            PcmFrameFormat.FloatStereo48k20Ms.FrameBytes);
    }

    [Fact]
    public void Write_CombinesPartialCallbacksIntoOneExactFrame()
    {
        PcmFrameFormat format =
            PcmFrameFormat.FloatMono48k20Ms;

        var buffer =
            new BoundedPcmFrameBuffer(
                format,
                capacityFrames: 4);

        byte[] first =
            Enumerable.Repeat(
                    (byte)0x11,
                    1000)
                .ToArray();

        byte[] second =
            Enumerable.Repeat(
                    (byte)0x22,
                    format.FrameBytes - first.Length)
                .ToArray();

        Assert.Equal(0, buffer.Write(first));
        Assert.Equal(1000, buffer.PartialFrameBytes);

        Assert.Equal(1, buffer.Write(second));
        Assert.Equal(0, buffer.PartialFrameBytes);
        Assert.Equal(1, buffer.PendingFrames);

        Assert.True(buffer.TryRead(out byte[] frame));
        Assert.Equal(format.FrameBytes, frame.Length);

        Assert.All(
            frame.Take(first.Length),
            value => Assert.Equal((byte)0x11, value));

        Assert.All(
            frame.Skip(first.Length),
            value => Assert.Equal((byte)0x22, value));
    }

    [Fact]
    public void Write_CanProduceMultipleFramesFromOneCallback()
    {
        PcmFrameFormat format =
            PcmFrameFormat.FloatMono48k20Ms;

        var buffer =
            new BoundedPcmFrameBuffer(
                format,
                capacityFrames: 8);

        byte[] data =
            new byte[format.FrameBytes * 3];

        Assert.Equal(3, buffer.Write(data));
        Assert.Equal(3, buffer.PendingFrames);
        Assert.Equal(0, buffer.PartialFrameBytes);
    }

    [Fact]
    public void Overflow_DropsOldestFrameInsteadOfGrowingLatency()
    {
        PcmFrameFormat format =
            PcmFrameFormat.FloatMono48k20Ms;

        var buffer =
            new BoundedPcmFrameBuffer(
                format,
                capacityFrames: 2);

        byte[] first =
            Enumerable.Repeat(
                    (byte)1,
                    format.FrameBytes)
                .ToArray();

        byte[] second =
            Enumerable.Repeat(
                    (byte)2,
                    format.FrameBytes)
                .ToArray();

        byte[] third =
            Enumerable.Repeat(
                    (byte)3,
                    format.FrameBytes)
                .ToArray();

        buffer.Write(first);
        buffer.Write(second);
        buffer.Write(third);

        Assert.Equal(2, buffer.PendingFrames);
        Assert.Equal(1, buffer.DroppedFrames);

        Assert.True(buffer.TryRead(out byte[] retainedFirst));
        Assert.True(buffer.TryRead(out byte[] retainedSecond));

        Assert.All(
            retainedFirst,
            value => Assert.Equal((byte)2, value));

        Assert.All(
            retainedSecond,
            value => Assert.Equal((byte)3, value));
    }

    [Fact]
    public void CreateSilenceFrame_ReturnsExactlyOneZeroedFrame()
    {
        PcmFrameFormat format =
            PcmFrameFormat.FloatMono48k20Ms;

        var buffer =
            new BoundedPcmFrameBuffer(
                format,
                capacityFrames: 2);

        byte[] silence =
            buffer.CreateSilenceFrame();

        Assert.Equal(format.FrameBytes, silence.Length);
        Assert.All(
            silence,
            value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void TryRead_WhenEmpty_IsNonBlockingAndReturnsFalse()
    {
        var buffer =
            new BoundedPcmFrameBuffer(
                PcmFrameFormat.FloatMono48k20Ms,
                capacityFrames: 2);

        Assert.False(buffer.TryRead(out byte[] frame));
        Assert.Empty(frame);
    }

    [Fact]
    public void Constructor_RejectsInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BoundedPcmFrameBuffer(
                PcmFrameFormat.FloatMono48k20Ms,
                capacityFrames: 0));
    }

    [Fact]
    public void Constructor_RejectsDefaultFormat()
    {
        Assert.Throws<ArgumentException>(
            () => new BoundedPcmFrameBuffer(
                default,
                capacityFrames: 2));
    }

    [Theory]
    [InlineData(0, 1, 32, 20)]
    [InlineData(48000, 0, 32, 20)]
    [InlineData(48000, 1, 0, 20)]
    [InlineData(48000, 1, 24, 0)]
    public void PcmFrameFormat_RejectsInvalidArguments(
        int sampleRate,
        int channels,
        int bitsPerSample,
        int frameDurationMilliseconds)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new PcmFrameFormat(
                sampleRate,
                channels,
                bitsPerSample,
                frameDurationMilliseconds));
    }
}