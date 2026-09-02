using System.Threading.Channels;
using Academy.Agent.Audio;

namespace Academy.Agent.Tests;

public sealed class ClassroomAudioRuntimeTests
{
    [Fact]
    public void FirstLease_StartsSystemCapture_AndFinalLeaseStopsIt()
    {
        var fixture = new RuntimeFixture();

        using ClassroomAudioRuntime runtime =
            fixture.CreateRuntime();

        ClassroomAudioRuntimeLease lease =
            runtime.Acquire();

        Assert.Equal(
            1,
            fixture.System.StartCount);

        Assert.Equal(
            1,
            runtime.ActiveLeaseCount);

        lease.Dispose();

        Assert.Equal(
            1,
            fixture.System.StopCount);

        Assert.Equal(
            0,
            runtime.ActiveLeaseCount);
    }

    [Fact]
    public void MultipleLeases_ShareOnePhysicalSystemCapture()
    {
        var fixture = new RuntimeFixture();

        using ClassroomAudioRuntime runtime =
            fixture.CreateRuntime();

        ClassroomAudioRuntimeLease first =
            runtime.Acquire();

        ClassroomAudioRuntimeLease second =
            runtime.Acquire();

        Assert.Equal(
            1,
            fixture.System.StartCount);

        Assert.Equal(
            2,
            runtime.ActiveLeaseCount);

        first.Dispose();

        Assert.Equal(
            0,
            fixture.System.StopCount);

        Assert.Equal(
            1,
            runtime.ActiveLeaseCount);

        second.Dispose();

        Assert.Equal(
            1,
            fixture.System.StopCount);

        Assert.Equal(
            0,
            runtime.ActiveLeaseCount);
    }

    [Fact]
    public void Scheduler_AdvancesCanonicalTwentyMillisecondTimeline()
    {
        var fixture = new RuntimeFixture();

        using ClassroomAudioRuntime runtime =
            fixture.CreateRuntime();

        using ClassroomAudioSubscription subscription =
            fixture.Hub.Subscribe(
                "test",
                capacityFrames: 8);

        using ClassroomAudioRuntimeLease lease =
            runtime.Acquire();

        fixture.TickSource.Signal(3);

        Assert.True(
            SpinWait.SpinUntil(
                () =>
                    subscription.PendingFrames >= 3,
                TimeSpan.FromSeconds(2)));

        Assert.True(
            subscription.TryRead(
                out ClassroomAudioFrame? first));

        Assert.True(
            subscription.TryRead(
                out ClassroomAudioFrame? second));

        Assert.True(
            subscription.TryRead(
                out ClassroomAudioFrame? third));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(third);

        Assert.Equal(
            0,
            first.SequenceNumber);

        Assert.Equal(
            TimeSpan.Zero,
            first.MediaTime);

        Assert.Equal(
            1,
            second.SequenceNumber);

        Assert.Equal(
            TimeSpan.FromMilliseconds(20),
            second.MediaTime);

        Assert.Equal(
            2,
            third.SequenceNumber);

        Assert.Equal(
            TimeSpan.FromMilliseconds(40),
            third.MediaTime);
    }

    [Fact]
    public void CommunicationLifecycle_StartsAndStopsOneTeacherCapture()
    {
        var fixture = new RuntimeFixture();

        fixture.Detector.InUse = true;

        using ClassroomAudioRuntime runtime =
            fixture.CreateRuntime();

        using ClassroomAudioRuntimeLease lease =
            runtime.Acquire();

        Assert.True(
            SpinWait.SpinUntil(
                () =>
                    fixture.Teacher.StartCount >= 1,
                TimeSpan.FromSeconds(2)));

        Assert.Equal(
            1,
            fixture.Teacher.StartCount);

        fixture.Detector.InUse = false;

        fixture.TickSource.Signal(13);

        Assert.True(
            SpinWait.SpinUntil(
                () =>
                    fixture.Teacher.StopCount >= 1,
                TimeSpan.FromSeconds(2)));

        Assert.Equal(
            1,
            fixture.Teacher.StopCount);

        Assert.False(
            fixture.Coordinator
                .IsTeacherCaptureActive);
    }

    [Fact]
    public void FailedTeacherStart_RetriesNoFasterThanCanonicalOneSecond()
    {
        var fixture = new RuntimeFixture();

        fixture.Detector.InUse = true;
        fixture.Teacher.FailStartAttempts = 1;

        using ClassroomAudioRuntime runtime =
            fixture.CreateRuntime();

        using ClassroomAudioRuntimeLease lease =
            runtime.Acquire();

        Assert.True(
            SpinWait.SpinUntil(
                () =>
                    fixture.Teacher.StartCount >= 1,
                TimeSpan.FromSeconds(2)));

        Assert.Equal(
            1,
            fixture.Teacher.StartCount);

        // Communication lifecycle checks occur every 13 frames.
        // At sequences 13, 26, and 39 the one-second retry boundary
        // (50 frames) has not yet been reached.
        fixture.TickSource.Signal(39);

        Assert.True(
            SpinWait.SpinUntil(
                () =>
                    fixture.Hub.NextSequenceNumber >= 39,
                TimeSpan.FromSeconds(2)));

        Assert.Equal(
            1,
            fixture.Teacher.StartCount);

        // The next communication check occurs at sequence 52,
        // which is beyond the 50-frame / one-second retry boundary.
        fixture.TickSource.Signal(13);

        Assert.True(
            SpinWait.SpinUntil(
                () =>
                    fixture.Teacher.StartCount >= 2,
                TimeSpan.FromSeconds(2)));

        Assert.Equal(
            2,
            fixture.Teacher.StartCount);

        Assert.True(
            fixture.Coordinator
                .IsTeacherCaptureActive);
    }

    [Fact]
    public void Dispose_RejectsFutureLeaseAcquisition()
    {
        var fixture = new RuntimeFixture();

        ClassroomAudioRuntime runtime =
            fixture.CreateRuntime();

        runtime.Dispose();

        Assert.Throws<ObjectDisposedException>(
            runtime.Acquire);
    }

    private sealed class RuntimeFixture
    {
        public RuntimeFixture()
        {
            Coordinator =
                ClassroomAudioCaptureCoordinator
                    .CreateForTesting(
                        Hub,
                        () => System,
                        () => Teacher);
        }

        public ClassroomAudioHub Hub { get; } =
            new();

        public FakeAudioCaptureService System { get; } =
            new();

        public FakeAudioCaptureService Teacher { get; } =
            new();

        public DetectorState Detector { get; } =
            new();

        public ManualTickSource TickSource { get; } =
            new();

        public ClassroomAudioCaptureCoordinator
            Coordinator { get; }

        public ClassroomAudioRuntime CreateRuntime()
        {
            return
                ClassroomAudioRuntime
                    .CreateForTesting(
                        Hub,
                        Coordinator,
                        () => Detector.InUse,
                        () => TickSource);
        }
    }

    private sealed class DetectorState
    {
        private int _inUse;

        public bool InUse
        {
            get =>
                Volatile.Read(
                    ref _inUse) != 0;

            set =>
                Volatile.Write(
                    ref _inUse,
                    value ? 1 : 0);
        }
    }

    private sealed class ManualTickSource :
        IClassroomAudioTickSource
    {
        private readonly Channel<bool>
            _ticks =
                Channel.CreateUnbounded<bool>(
                    new UnboundedChannelOptions
                    {
                        SingleReader = true,
                        SingleWriter = false
                    });

        public ValueTask<bool>
            WaitForNextTickAsync(
                CancellationToken cancellationToken)
        {
            return
                _ticks.Reader.ReadAsync(
                    cancellationToken);
        }

        public void Signal(
            int count = 1)
        {
            for (int index = 0;
                 index < count;
                 index++)
            {
                if (!_ticks.Writer
                    .TryWrite(true))
                {
                    throw new InvalidOperationException(
                        "Could not publish manual audio tick.");
                }
            }
        }

        public void Dispose()
        {
            _ticks.Writer.TryComplete();
        }
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

        public int FailStartAttempts
        {
            get;
            set;
        }

        public void Start()
        {
            StartCount++;

            if (StartCount <=
                FailStartAttempts)
            {
                throw new InvalidOperationException(
                    "Simulated microphone start failure.");
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