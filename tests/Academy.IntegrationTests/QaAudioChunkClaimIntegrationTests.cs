using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.IntegrationTests;

public sealed class QaAudioChunkClaimIntegrationTests :
    IntegrationTestBase
{
    [Fact]
    public async Task ConcurrentWorkers_ExactlyOneClaimsFocusChunk()
    {
        DateTimeOffset nowUtc =
            DateTimeOffset.UtcNow;

        var teacher =
            new Teacher
            {
                Id =
                    Guid.NewGuid(),

                FullName =
                    "QA Claim Teacher",

                Email =
                    $"teacher-{Guid.NewGuid():N}@test.local",

                CreatedAtUtc =
                    nowUtc,

                UpdatedAtUtc =
                    nowUtc
            };

        var student =
            new Student
            {
                Id =
                    Guid.NewGuid(),

                FullName =
                    "QA Claim Student",

                Email =
                    $"student-{Guid.NewGuid():N}@test.local",

                CreatedAtUtc =
                    nowUtc,

                UpdatedAtUtc =
                    nowUtc
            };

        var course =
            new Course
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "QA Claim Course",

                CreatedAtUtc =
                    nowUtc,

                UpdatedAtUtc =
                    nowUtc
            };

        var device =
            new Device
            {
                Id =
                    Guid.NewGuid(),

                DeviceId =
                    $"qa-claim-{Guid.NewGuid():N}",

                DeviceName =
                    "QA Claim Laptop",

                AgentVersion =
                    "integration-test",

                Status =
                    DeviceStatus.Online,

                LastSeenUtc =
                    nowUtc,

                CreatedAtUtc =
                    nowUtc,

                UpdatedAtUtc =
                    nowUtc
            };

        var session =
            new Session
            {
                Id =
                    Guid.NewGuid(),

                TeacherId =
                    teacher.Id,

                StudentId =
                    student.Id,

                CourseId =
                    course.Id,

                DeviceId =
                    device.Id,

                ScheduledStartUtc =
                    nowUtc.AddMinutes(-10),

                ScheduledEndUtc =
                    nowUtc.AddMinutes(20),

                StartedAtUtc =
                    nowUtc.AddMinutes(-10),

                Status =
                    SessionStatus.Live,

                CreatedAtUtc =
                    nowUtc,

                UpdatedAtUtc =
                    nowUtc
            };

        var focus =
            new QaAudioChunk
            {
                Id =
                    Guid.NewGuid(),

                DeviceId =
                    device.Id,

                SessionId =
                    session.Id,

                CaptureId =
                    Guid.NewGuid(),

                SequenceNumber =
                    7,

                StartedAtUtc =
                    nowUtc.AddSeconds(-30),

                EndedAtUtc =
                    nowUtc.AddSeconds(-25),

                StorageKey =
                    $"qa/chunks/{session.Id}/focus.wav",

                ContentType =
                    "audio/wav",

                SizeBytes =
                    160044,

                FormatVersion =
                    1,

                SampleRate =
                    16000,

                Channels =
                    1,

                BitsPerSample =
                    16,

                DeleteAfterUtc =
                    nowUtc.AddHours(24),

                CreatedAtUtc =
                    nowUtc.AddSeconds(-30),

                UpdatedAtUtc =
                    nowUtc.AddSeconds(-30)
            };

        DbContext.AddRange(
            teacher,
            student,
            course,
            device,
            session,
            focus);

        await DbContext.SaveChangesAsync();

        using IServiceScope scope1 =
            ServiceProvider.CreateScope();

        using IServiceScope scope2 =
            ServiceProvider.CreateScope();

        AppDbContext context1 =
            scope1.ServiceProvider
                .GetRequiredService<AppDbContext>();

        AppDbContext context2 =
            scope2.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var repository1 =
            new QaAudioChunkRepository(
                context1);

        var repository2 =
            new QaAudioChunkRepository(
                context2);

        DateTimeOffset claimedAtUtc =
            DateTimeOffset.UtcNow;

        DateTimeOffset readyBeforeUtc =
            claimedAtUtc -
            QaAudioChunkWorkerService.FutureContext;

        DateTimeOffset staleClaimBeforeUtc =
            claimedAtUtc -
            QaAudioChunkWorkerService.ClaimLease;

        Task<IReadOnlyList<QaAudioChunk>> first =
            repository1.ClaimReadyAsync(
                readyBeforeUtc,
                staleClaimBeforeUtc,
                claimedAtUtc,
                QaAudioChunkWorkerService
                    .MaximumAttempts,
                1);

        Task<IReadOnlyList<QaAudioChunk>> second =
            repository2.ClaimReadyAsync(
                readyBeforeUtc,
                staleClaimBeforeUtc,
                claimedAtUtc,
                QaAudioChunkWorkerService
                    .MaximumAttempts,
                1);

        IReadOnlyList<QaAudioChunk>[] results =
            await Task.WhenAll(
                first,
                second);

        int totalClaims =
            results.Sum(
                x =>
                    x.Count);

        Assert.Equal(
            1,
            totalClaims);

        QaAudioChunk claimed =
            Assert.Single(
                results.SelectMany(
                    x =>
                        x));

        Assert.Equal(
            focus.Id,
            claimed.Id);

        using IServiceScope verifyScope =
            ServiceProvider.CreateScope();

        AppDbContext verifyContext =
            verifyScope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        QaAudioChunk persisted =
            await verifyContext
                .QaAudioChunks
                .AsNoTracking()
                .SingleAsync(
                    x =>
                        x.Id ==
                            focus.Id);

        Assert.Equal(
            1,
            persisted.AttemptCount);

        Assert.NotNull(
            persisted.ClaimedAtUtc);

        Assert.Null(
            persisted.ProcessedAtUtc);
    }
}