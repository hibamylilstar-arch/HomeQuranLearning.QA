using Academy.Agent.Audio;

namespace Academy.Agent.Tests;

public sealed class ClassroomAudioHubTests
{
    [Fact]
    public void Formats_AreCanonicalTwentyMillisecondPcm()
    {
        var hub = new ClassroomAudioHub();

        Assert.Equal(48000, hub.SystemFormat.SampleRate);
        Assert.Equal(2, hub.SystemFormat.Channels);
        Assert.Equal(32, hub.SystemFormat.BitsPerSample);
        Assert.Equal(7680, hub.SystemFormat.FrameBytes);

        Assert.Equal(48000, hub.TeacherFormat.SampleRate);
        Assert.Equal(1, hub.TeacherFormat.Channels);
        Assert.Equal(32, hub.TeacherFormat.BitsPerSample);
        Assert.Equal(3840, hub.TeacherFormat.FrameBytes);

        Assert.Equal(
            TimeSpan.FromMilliseconds(20),
            hub.FrameDuration);
    }

    [Fact]
    public void AdvanceOneFrame_UsesCanonicalSequenceAndMediaTime()
    {
        var hub = new ClassroomAudioHub();

        ClassroomAudioFrame first =
            hub.AdvanceOneFrame();

        ClassroomAudioFrame second =
            hub.AdvanceOneFrame();

        ClassroomAudioFrame third =
            hub.AdvanceOneFrame();

        Assert.Equal(0, first.SequenceNumber);
        Assert.Equal(TimeSpan.Zero, first.MediaTime);

        Assert.Equal(1, second.SequenceNumber);
        Assert.Equal(
            TimeSpan.FromMilliseconds(20),
            second.MediaTime);

        Assert.Equal(2, third.SequenceNumber);
        Assert.Equal(
            TimeSpan.FromMilliseconds(40),
            third.MediaTime);

        Assert.Equal(3, hub.NextSequenceNumber);
    }

    [Fact]
    public void AdvanceOneFrame_FillsMissingSourcesWithExactSilence()
    {
        var hub = new ClassroomAudioHub();

        ClassroomAudioFrame frame =
            hub.AdvanceOneFrame();

        byte[] system =
            frame.SystemPcm.ToArray();

        byte[] teacher =
            frame.TeacherPcm.ToArray();

        Assert.Equal(
            hub.SystemFormat.FrameBytes,
            system.Length);

        Assert.Equal(
            hub.TeacherFormat.FrameBytes,
            teacher.Length);

        Assert.All(
            system,
            value => Assert.Equal((byte)0, value));

        Assert.All(
            teacher,
            value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void ArbitrarySystemCallbacks_AreFramedBeforeTimelineConsumption()
    {
        var hub = new ClassroomAudioHub();

        byte[] first =
            Enumerable.Repeat(
                    (byte)0x31,
                    1000)
                .ToArray();

        byte[] second =
            Enumerable.Repeat(
                    (byte)0x42,
                    hub.SystemFormat.FrameBytes -
                    first.Length)
                .ToArray();

        Assert.Equal(
            0,
            hub.WriteSystemPcm(first));

        Assert.Equal(
            1,
            hub.WriteSystemPcm(second));

        ClassroomAudioFrame frame =
            hub.AdvanceOneFrame();

        byte[] system =
            frame.SystemPcm.ToArray();

        Assert.All(
            system.Take(first.Length),
            value => Assert.Equal(
                (byte)0x31,
                value));

        Assert.All(
            system.Skip(first.Length),
            value => Assert.Equal(
                (byte)0x42,
                value));
    }

    [Fact]
    public void SystemAndTeacherData_ShareSameTimelineSlot()
    {
        var hub = new ClassroomAudioHub();

        byte[] system =
            Enumerable.Repeat(
                    (byte)0x11,
                    hub.SystemFormat.FrameBytes)
                .ToArray();

        byte[] teacher =
            Enumerable.Repeat(
                    (byte)0x22,
                    hub.TeacherFormat.FrameBytes)
                .ToArray();

        hub.WriteSystemPcm(system);
        hub.WriteTeacherPcm(teacher);

        ClassroomAudioFrame frame =
            hub.AdvanceOneFrame();

        Assert.Equal(system, frame.SystemPcm.ToArray());
        Assert.Equal(teacher, frame.TeacherPcm.ToArray());
    }

    [Fact]
    public void SlowSubscriber_DoesNotBlockOrGrowLatencyForOtherSubscriber()
    {
        var hub = new ClassroomAudioHub();

        using ClassroomAudioSubscription fast =
            hub.Subscribe(
                "live",
                capacityFrames: 4);

        using ClassroomAudioSubscription slow =
            hub.Subscribe(
                "recording",
                capacityFrames: 2);

        hub.AdvanceOneFrame();
        hub.AdvanceOneFrame();
        hub.AdvanceOneFrame();

        Assert.Equal(3, fast.PendingFrames);
        Assert.Equal(0, fast.DroppedFrames);

        Assert.Equal(2, slow.PendingFrames);
        Assert.Equal(1, slow.DroppedFrames);

        Assert.True(
            fast.TryRead(
                out ClassroomAudioFrame? fast0));

        Assert.True(
            fast.TryRead(
                out ClassroomAudioFrame? fast1));

        Assert.True(
            fast.TryRead(
                out ClassroomAudioFrame? fast2));

        Assert.NotNull(fast0);
        Assert.NotNull(fast1);
        Assert.NotNull(fast2);

        Assert.Equal(0, fast0.SequenceNumber);
        Assert.Equal(1, fast1.SequenceNumber);
        Assert.Equal(2, fast2.SequenceNumber);

        Assert.True(
            slow.TryRead(
                out ClassroomAudioFrame? slow1));

        Assert.True(
            slow.TryRead(
                out ClassroomAudioFrame? slow2));

        Assert.NotNull(slow1);
        Assert.NotNull(slow2);

        Assert.Equal(1, slow1.SequenceNumber);
        Assert.Equal(2, slow2.SequenceNumber);
    }

    [Fact]
    public void InputOverflow_DropsOldestSourceFrame()
    {
        var hub =
            new ClassroomAudioHub(
                inputCapacityFrames: 2);

        byte[] first =
            Enumerable.Repeat(
                    (byte)1,
                    hub.SystemFormat.FrameBytes)
                .ToArray();

        byte[] second =
            Enumerable.Repeat(
                    (byte)2,
                    hub.SystemFormat.FrameBytes)
                .ToArray();

        byte[] third =
            Enumerable.Repeat(
                    (byte)3,
                    hub.SystemFormat.FrameBytes)
                .ToArray();

        hub.WriteSystemPcm(first);
        hub.WriteSystemPcm(second);
        hub.WriteSystemPcm(third);

        Assert.Equal(
            1,
            hub.DroppedSystemInputFrames);

        ClassroomAudioFrame frame =
            hub.AdvanceOneFrame();

        Assert.All(
            frame.SystemPcm.ToArray(),
            value => Assert.Equal(
                (byte)2,
                value));
    }

    [Fact]
    public void DisposeSubscription_RemovesItAndClearsPendingFrames()
    {
        var hub = new ClassroomAudioHub();

        ClassroomAudioSubscription subscription =
            hub.Subscribe(
                "live",
                capacityFrames: 2);

        Assert.Equal(1, hub.SubscriberCount);

        hub.AdvanceOneFrame();

        Assert.Equal(
            1,
            subscription.PendingFrames);

        subscription.Dispose();

        Assert.True(subscription.IsDisposed);
        Assert.Equal(0, subscription.PendingFrames);
        Assert.Equal(0, hub.SubscriberCount);

        hub.AdvanceOneFrame();

        Assert.False(
            subscription.TryRead(
                out _));
    }

    [Fact]
    public void Subscribe_RejectsInvalidArguments()
    {
        var hub = new ClassroomAudioHub();

        Assert.Throws<ArgumentException>(
            () => hub.Subscribe(
                "",
                capacityFrames: 2));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => hub.Subscribe(
                "live",
                capacityFrames: 0));
    }

    [Fact]
    public void Constructor_RejectsInvalidInputCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ClassroomAudioHub(
                inputCapacityFrames: 0));
    }
}