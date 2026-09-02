using Academy.Agent.Audio;

namespace Academy.Agent.Tests;

public sealed class ClassroomAudioCaptureCoordinatorTests
{
    [Fact]
    public void StartSystemCapture_IsIdempotentAndFeedsSharedHub()
    {
        var hub = new ClassroomAudioHub();
        var system = new FakeAudioCaptureService();
        var teacher = new FakeAudioCaptureService();

        using var coordinator =
            ClassroomAudioCaptureCoordinator.CreateForTesting(
                hub,
                () => system,
                () => teacher);

        coordinator.StartSystemCapture();
        coordinator.StartSystemCapture();

        Assert.True(coordinator.IsSystemCaptureActive);
        Assert.Equal(1, system.StartCount);

        byte[] pcm =
            Enumerable.Repeat(
                    (byte)0x31,
                    hub.SystemFormat.FrameBytes)
                .ToArray();

        system.RaiseData(pcm);

        Assert.Equal(
            1,
            coordinator.SystemFramesWritten);

        ClassroomAudioFrame frame =
            hub.AdvanceOneFrame();

        Assert.Equal(
            pcm,
            frame.SystemPcm.ToArray());

        coordinator.StopAll();

        Assert.False(
            coordinator.IsSystemCaptureActive);

        Assert.Equal(
            1,
            system.StopCount);
    }

    [Fact]
    public void SystemCapture_SynchronousStartupCallback_DoesNotDeadlock()
    {
        var hub = new ClassroomAudioHub();

        byte[] pcm =
            Enumerable.Repeat(
                    (byte)0x51,
                    hub.SystemFormat.FrameBytes)
                .ToArray();

        var system =
            new FakeAudioCaptureService
            {
                DataToRaiseOnStart = pcm
            };

        var teacher =
            new FakeAudioCaptureService();

        using var coordinator =
            ClassroomAudioCaptureCoordinator.CreateForTesting(
                hub,
                () => system,
                () => teacher);

        coordinator.StartSystemCapture();

        Assert.Equal(
            1,
            coordinator.SystemFramesWritten);

        ClassroomAudioFrame frame =
            hub.AdvanceOneFrame();

        Assert.Equal(
            pcm,
            frame.SystemPcm.ToArray());
    }

    [Fact]
    public void TeacherCapture_EnableDisable_IsIdempotentAndFeedsSharedHub()
    {
        var hub = new ClassroomAudioHub();
        var system = new FakeAudioCaptureService();
        var teacher = new FakeAudioCaptureService();

        using var coordinator =
            ClassroomAudioCaptureCoordinator.CreateForTesting(
                hub,
                () => system,
                () => teacher);

        coordinator.SetTeacherCaptureEnabled(true);
        coordinator.SetTeacherCaptureEnabled(true);

        Assert.True(
            coordinator.IsTeacherCaptureActive);

        Assert.Equal(
            1,
            teacher.StartCount);

        byte[] pcm =
            Enumerable.Repeat(
                    (byte)0x42,
                    hub.TeacherFormat.FrameBytes)
                .ToArray();

        teacher.RaiseData(pcm);

        Assert.Equal(
            1,
            coordinator.TeacherFramesWritten);

        ClassroomAudioFrame frame =
            hub.AdvanceOneFrame();

        Assert.Equal(
            pcm,
            frame.TeacherPcm.ToArray());

        coordinator.SetTeacherCaptureEnabled(false);
        coordinator.SetTeacherCaptureEnabled(false);

        Assert.False(
            coordinator.IsTeacherCaptureActive);

        Assert.Equal(
            1,
            teacher.StopCount);
    }

    [Fact]
    public void TeacherCapture_SynchronousStartupCallback_DoesNotDeadlock()
    {
        var hub = new ClassroomAudioHub();

        byte[] pcm =
            Enumerable.Repeat(
                    (byte)0x61,
                    hub.TeacherFormat.FrameBytes)
                .ToArray();

        var system =
            new FakeAudioCaptureService();

        var teacher =
            new FakeAudioCaptureService
            {
                DataToRaiseOnStart = pcm
            };

        using var coordinator =
            ClassroomAudioCaptureCoordinator.CreateForTesting(
                hub,
                () => system,
                () => teacher);

        coordinator.SetTeacherCaptureEnabled(true);

        Assert.Equal(
            1,
            coordinator.TeacherFramesWritten);

        ClassroomAudioFrame frame =
            hub.AdvanceOneFrame();

        Assert.Equal(
            pcm,
            frame.TeacherPcm.ToArray());
    }

    [Fact]
    public void TeacherRecordingStopped_ReleasesTeacherOwnership()
    {
        var hub = new ClassroomAudioHub();
        var system = new FakeAudioCaptureService();
        var teacher = new FakeAudioCaptureService();

        using var coordinator =
            ClassroomAudioCaptureCoordinator.CreateForTesting(
                hub,
                () => system,
                () => teacher);

        coordinator.SetTeacherCaptureEnabled(true);

        Assert.True(
            coordinator.IsTeacherCaptureActive);

        teacher.RaiseStopped();

        Assert.False(
            coordinator.IsTeacherCaptureActive);

        coordinator.SetTeacherCaptureEnabled(true);

        Assert.Equal(
            2,
            teacher.StartCount);
    }

    [Fact]
    public void StopAll_ReleasesBothPhysicalCaptureOwners()
    {
        var hub = new ClassroomAudioHub();
        var system = new FakeAudioCaptureService();
        var teacher = new FakeAudioCaptureService();

        using var coordinator =
            ClassroomAudioCaptureCoordinator.CreateForTesting(
                hub,
                () => system,
                () => teacher);

        coordinator.StartSystemCapture();
        coordinator.SetTeacherCaptureEnabled(true);

        coordinator.StopAll();
        coordinator.StopAll();

        Assert.False(
            coordinator.IsSystemCaptureActive);

        Assert.False(
            coordinator.IsTeacherCaptureActive);

        Assert.Equal(1, system.StopCount);
        Assert.Equal(1, teacher.StopCount);
    }

    [Fact]
    public void Dispose_ReleasesCapturesAndRejectsRestart()
    {
        var hub = new ClassroomAudioHub();
        var system = new FakeAudioCaptureService();
        var teacher = new FakeAudioCaptureService();

        var coordinator =
            ClassroomAudioCaptureCoordinator.CreateForTesting(
                hub,
                () => system,
                () => teacher);

        coordinator.StartSystemCapture();
        coordinator.SetTeacherCaptureEnabled(true);

        coordinator.Dispose();

        Assert.Equal(1, system.StopCount);
        Assert.Equal(1, teacher.StopCount);

        Assert.Throws<ObjectDisposedException>(
            coordinator.StartSystemCapture);

        Assert.Throws<ObjectDisposedException>(
            () =>
                coordinator.SetTeacherCaptureEnabled(true));
    }

    private sealed class FakeAudioCaptureService :
        IAudioCaptureService
    {
        public event EventHandler<AudioDataAvailableEventArgs>?
            DataAvailable;

        public event EventHandler?
            RecordingStopped;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public byte[]? DataToRaiseOnStart
        {
            get;
            init;
        }

        public void Start()
        {
            StartCount++;

            if (DataToRaiseOnStart is not null)
            {
                RaiseData(
                    DataToRaiseOnStart);
            }
        }

        public void Stop()
        {
            StopCount++;
        }

        public void RaiseData(
            byte[] pcm)
        {
            DataAvailable?.Invoke(
                this,
                new AudioDataAvailableEventArgs
                {
                    Buffer = pcm,
                    BytesRecorded = pcm.Length
                });
        }

        public void RaiseStopped()
        {
            RecordingStopped?.Invoke(
                this,
                EventArgs.Empty);
        }
    }
}