using Academy.Agent.Audio;

namespace Academy.Agent.Tests;

public sealed class ClassroomAudioSubscriptionAsyncTests
{
    [Fact]
    public async Task ReadNextAsync_WaitsForCanonicalPublication()
    {
        var hub = new ClassroomAudioHub();

        using ClassroomAudioSubscription subscription =
            hub.Subscribe(
                "live",
                capacityFrames: 4);

        Task<ClassroomAudioFrame> pending =
            subscription
                .ReadNextAsync()
                .AsTask();

        Assert.False(
            pending.IsCompleted);

        hub.AdvanceOneFrame();

        ClassroomAudioFrame frame =
            await pending.WaitAsync(
                TimeSpan.FromSeconds(2));

        Assert.Equal(
            0,
            frame.SequenceNumber);

        Assert.Equal(
            TimeSpan.Zero,
            frame.MediaTime);
    }

    [Fact]
    public async Task ReadNextAsync_ReturnsQueuedFrameWithoutWaitingForAnotherTick()
    {
        var hub = new ClassroomAudioHub();

        using ClassroomAudioSubscription subscription =
            hub.Subscribe(
                "live",
                capacityFrames: 4);

        hub.AdvanceOneFrame();
        hub.AdvanceOneFrame();

        ClassroomAudioFrame first =
            await subscription.ReadNextAsync();

        ClassroomAudioFrame second =
            await subscription.ReadNextAsync();

        Assert.Equal(
            0,
            first.SequenceNumber);

        Assert.Equal(
            1,
            second.SequenceNumber);
    }

    [Fact]
    public async Task ReadNextAsync_DisposeWakesPendingReader()
    {
        var hub = new ClassroomAudioHub();

        ClassroomAudioSubscription subscription =
            hub.Subscribe(
                "live",
                capacityFrames: 4);

        Task<ClassroomAudioFrame> pending =
            subscription
                .ReadNextAsync()
                .AsTask();

        Assert.False(
            pending.IsCompleted);

        subscription.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () =>
                await pending.WaitAsync(
                    TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task ReadNextAsync_HonorsCancellation()
    {
        var hub = new ClassroomAudioHub();

        using ClassroomAudioSubscription subscription =
            hub.Subscribe(
                "live",
                capacityFrames: 4);

        using var cts =
            new CancellationTokenSource();

        Task<ClassroomAudioFrame> pending =
            subscription
                .ReadNextAsync(
                    cts.Token)
                .AsTask();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await pending.WaitAsync(
                    TimeSpan.FromSeconds(2)));
    }
}