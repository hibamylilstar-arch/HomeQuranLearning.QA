using System.Buffers.Binary;
using System.Text;
using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class QaDirectAlertService
{
    private const string RestrictedRuleReason =
        "Restricted Rule";

    private const int SampleRate =
        16000;

    private const int Channels =
        1;

    private const int BitsPerSample =
        16;

    private const int WaveHeaderBytes =
        44;

    private static readonly TimeSpan HistoryContext =
        TimeSpan.FromSeconds(10);

    private static readonly TimeSpan FutureContext =
        TimeSpan.FromSeconds(20);

    private static readonly TimeSpan EvidenceRetention =
        TimeSpan.FromDays(7);

    private static readonly TimeSpan MaximumTriggerDuration =
        TimeSpan.FromSeconds(10);

    private static readonly TimeSpan MaximumEvidenceDuration =
        TimeSpan.FromSeconds(40);

    private readonly IQaAlertRepository
        _alertRepository;

    private readonly IQaAudioChunkRepository
        _chunkRepository;

    private readonly ISessionRepository
        _sessionRepository;

    private readonly IStorageService
        _storageService;

    private readonly IUnitOfWork
        _unitOfWork;

    private readonly string
        _bucketName;

    public QaDirectAlertService(
        IQaAlertRepository alertRepository,
        IQaAudioChunkRepository chunkRepository,
        ISessionRepository sessionRepository,
        IStorageService storageService,
        IUnitOfWork unitOfWork,
        string bucketName)
    {
        _alertRepository =
            alertRepository;

        _chunkRepository =
            chunkRepository;

        _sessionRepository =
            sessionRepository;

        _storageService =
            storageService;

        _unitOfWork =
            unitOfWork;

        _bucketName =
            string.IsNullOrWhiteSpace(bucketName)
                ? throw new ArgumentException(
                    "Storage bucket is required.",
                    nameof(bucketName))
                : bucketName.Trim();
    }

    public async Task<DirectQaAlertResponse>
        CreateRestrictedAsync(
            CreateDirectRestrictedQaAlertRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.FocusChunkId == Guid.Empty)
        {
            throw new ArgumentException(
                "FocusChunkId is required.");
        }

        if (request.ClaimedAtUtc == default)
        {
            throw new ArgumentException(
                "ClaimedAtUtc is required.");
        }

        if (request.QaRuleId == Guid.Empty)
        {
            throw new ArgumentException(
                "QaRuleId is required.");
        }

        string matchedPhrase =
            RequireText(
                request.MatchedPhrase,
                nameof(request.MatchedPhrase),
                512);

        string transcript =
            RequireText(
                request.Transcript,
                nameof(request.Transcript),
                4096);

        string policyVersion =
            RequireText(
                request.PolicyVersion,
                nameof(request.PolicyVersion),
                128);

        string analysisVersion =
            RequireText(
                request.AnalysisVersion,
                nameof(request.AnalysisVersion),
                128);

        string idempotencyKey =
            RequireText(
                request.AnalysisIdempotencyKey,
                nameof(request.AnalysisIdempotencyKey),
                512);

        DateTimeOffset triggerStartUtc =
            request.TriggerStartUtc
                .ToUniversalTime();

        DateTimeOffset triggerEndUtc =
            request.TriggerEndUtc
                .ToUniversalTime();

        if (request.TriggerStartUtc == default ||
            request.TriggerEndUtc == default ||
            triggerEndUtc <= triggerStartUtc)
        {
            throw new ArgumentException(
                "Trigger interval is invalid.");
        }

        if (triggerEndUtc - triggerStartUtc >
            MaximumTriggerDuration)
        {
            throw new ArgumentException(
                "Direct restricted trigger duration is too large.");
        }

        // Idempotent retry is checked before active-claim validation.
        // This covers the case where alert creation succeeded but the
        // worker lost the HTTP response and the focus chunk was completed.
        QaAlert? existing =
            await _alertRepository
                .GetByAnalysisIdempotencyKeyAsync(
                    idempotencyKey,
                    cancellationToken);

        if (existing is not null)
        {
            EnsureExistingDirectAlertMatches(
                existing,
                request.FocusChunkId,
                request.QaRuleId,
                matchedPhrase,
                transcript,
                policyVersion,
                analysisVersion,
                triggerStartUtc);

            return
                new DirectQaAlertResponse
                {
                    AlertId =
                        existing.Id,

                    Duplicate =
                        true
                };
        }

        QaAudioChunk focus =
            await _chunkRepository
                .GetByIdAsync(
                    request.FocusChunkId,
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                "Focus QA audio chunk was not found.");

        ValidateClaimOwnership(
            focus,
            request.ClaimedAtUtc);

        Session session =
            await _sessionRepository
                .GetByIdWithDetailsAsync(
                    focus.SessionId,
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                "QA evidence Session was not found.");

        if (session.Status != SessionStatus.Live &&
            session.Status != SessionStatus.Completed)
        {
            throw new InvalidOperationException(
                "Direct QA evidence requires a Live or Completed Session.");
        }

        if (session.DeviceId != focus.DeviceId)
        {
            throw new InvalidOperationException(
                "QA focus chunk belongs to a different Session device.");
        }

        var (
            sessionStartUtc,
            sessionEndUtc) =
                SessionWindowResolver.Resolve(
                    session);

        if (triggerStartUtc < sessionStartUtc ||
            triggerEndUtc > sessionEndUtc)
        {
            throw new InvalidOperationException(
                "Direct QA trigger is outside the scheduled Session window.");
        }

        if (!IntervalsOverlap(
                triggerStartUtc,
                triggerEndUtc,
                focus.StartedAtUtc,
                focus.EndedAtUtc))
        {
            throw new InvalidOperationException(
                "Direct QA trigger must overlap its claimed focus chunk.");
        }

        DateTimeOffset evidenceStartUtc =
            Max(
                sessionStartUtc,
                triggerStartUtc -
                    HistoryContext);

        DateTimeOffset requestedEvidenceEndUtc =
            Min(
                sessionEndUtc,
                triggerEndUtc +
                    FutureContext);

        if (requestedEvidenceEndUtc <=
            evidenceStartUtc)
        {
            throw new InvalidOperationException(
                "Direct QA evidence window is empty.");
        }

        if (requestedEvidenceEndUtc -
            evidenceStartUtc >
            MaximumEvidenceDuration)
        {
            throw new InvalidOperationException(
                "Direct QA evidence window exceeds the maximum duration.");
        }

        IReadOnlyList<QaAudioChunk> context =
            await _chunkRepository
                .GetContextAsync(
                    session.Id,
                    evidenceStartUtc,
                    requestedEvidenceEndUtc,
                    cancellationToken);

        if (!context.Any(
                x =>
                    x.Id ==
                        focus.Id))
        {
            throw new InvalidOperationException(
                "Focus chunk is missing from direct QA evidence context.");
        }

        EvidenceWave evidence =
            await BuildEvidenceAsync(
                context,
                focus.Id,
                evidenceStartUtc,
                requestedEvidenceEndUtc,
                cancellationToken);

        Guid alertId =
            Guid.NewGuid();

        string storageKey =
            $"qa/evidence/{session.Id}/{alertId}.wav";

        DateTimeOffset nowUtc =
            DateTimeOffset.UtcNow;

        await using var evidenceStream =
            new MemoryStream(
                evidence.WaveBytes,
                writable: false);

        await _storageService
            .UploadAsync(
                _bucketName,
                storageKey,
                evidenceStream,
                "audio/wav",
                cancellationToken);

        string? laptopName =
            !string.IsNullOrWhiteSpace(
                session.Device?
                    .RecordingDisplayName)
                ? session.Device!
                    .RecordingDisplayName
                : session.Device?
                    .DeviceName;

        var alert =
            new QaAlert
            {
                Id =
                    alertId,

                // Direct evidence is intentionally independent
                // from finalized MP4 recording lifecycle.
                RecordingId =
                    null,

                SourceQaAudioChunkId =
                    focus.Id,

                QaRuleId =
                    request.QaRuleId,

                MatchedPhrase =
                    matchedPhrase,

                TimestampUtc =
                    triggerStartUtc,

                DetectionReason =
                    RestrictedRuleReason,

                Transcript =
                    transcript,

                PolicyVersion =
                    policyVersion,

                AnalysisVersion =
                    analysisVersion,

                SourceTrackIndex =
                    null,

                AudioLayoutVersion =
                    1,

                TriggerStartSeconds =
                    (
                        triggerStartUtc -
                        evidence.StartUtc
                    )
                    .TotalSeconds,

                TriggerEndSeconds =
                    (
                        triggerEndUtc -
                        evidence.StartUtc
                    )
                    .TotalSeconds,

                EvidenceStartSeconds =
                    0,

                EvidenceEndSeconds =
                    evidence.DurationSeconds,

                EvidenceStorageKey =
                    storageKey,

                EvidenceContentType =
                    "audio/wav",

                EvidenceSizeBytes =
                    evidence.WaveBytes.LongLength,

                EvidenceDurationSeconds =
                    evidence.DurationSeconds,

                EvidenceStartUtc =
                    evidence.StartUtc,

                EvidenceEndUtc =
                    evidence.EndUtc,

                EvidenceDeleteAfterUtc =
                    nowUtc +
                    EvidenceRetention,

                AnalysisIdempotencyKey =
                    idempotencyKey,

                DeviceId =
                    focus.DeviceId,

                SessionId =
                    session.Id,

                TeacherId =
                    session.TeacherId,

                StudentId =
                    session.StudentId,

                CourseId =
                    session.CourseId,

                LaptopName =
                    laptopName,

                ActualDeviceName =
                    session.Device?
                        .DeviceName,

                TeacherName =
                    session.Teacher?
                        .FullName,

                StudentName =
                    session.Student?
                        .FullName,

                CourseName =
                    session.Course?
                        .Name,

                Status =
                    QaAlertStatus.Open,

                CreatedAtUtc =
                    nowUtc,

                UpdatedAtUtc =
                    nowUtc
            };

        try
        {
            await _alertRepository
                .AddAsync(
                    alert,
                    cancellationToken);

            await _unitOfWork
                .SaveChangesAsync(
                    cancellationToken);
        }
        catch
        {
            // Avoid orphaned final evidence if persistence fails.
            try
            {
                await _storageService
                    .DeleteAsync(
                        _bucketName,
                        storageKey,
                        cancellationToken);
            }
            catch
            {
                // Original persistence failure remains authoritative.
            }

            throw;
        }

        return
            new DirectQaAlertResponse
            {
                AlertId =
                    alert.Id,

                Duplicate =
                    false
            };
    }

    private async Task<EvidenceWave>
        BuildEvidenceAsync(
            IReadOnlyList<QaAudioChunk> chunks,
            Guid focusChunkId,
            DateTimeOffset evidenceStartUtc,
            DateTimeOffset evidenceEndUtc,
            CancellationToken cancellationToken)
    {
        double requestedDurationSeconds =
            (
                evidenceEndUtc -
                evidenceStartUtc
            )
            .TotalSeconds;

        int totalSamples =
            checked(
                (int)Math.Round(
                    requestedDurationSeconds *
                    SampleRate,
                    MidpointRounding.AwayFromZero));

        if (totalSamples <= 0)
        {
            throw new InvalidOperationException(
                "Direct QA evidence contains no timeline samples.");
        }

        if (totalSamples >
            SampleRate *
            (int)MaximumEvidenceDuration.TotalSeconds)
        {
            throw new InvalidOperationException(
                "Direct QA evidence sample count exceeds the limit.");
        }

        short[] timeline =
            new short[
                totalSamples];

        bool focusCopied =
            false;

        foreach (
            QaAudioChunk chunk
            in chunks
                .OrderBy(
                    x =>
                        x.StartedAtUtc)
                .ThenBy(
                    x =>
                        x.CaptureId)
                .ThenBy(
                    x =>
                        x.SequenceNumber))
        {
            ValidateCanonicalChunkMetadata(
                chunk);

            using var downloaded =
                new MemoryStream();

            await _storageService
                .DownloadAsync(
                    _bucketName,
                    chunk.StorageKey,
                    downloaded,
                    cancellationToken);

            short[] pcm =
                ParseCanonicalWave(
                    downloaded.ToArray(),
                    chunk);

            DateTimeOffset overlapStartUtc =
                Max(
                    evidenceStartUtc,
                    chunk.StartedAtUtc);

            DateTimeOffset overlapEndUtc =
                Min(
                    evidenceEndUtc,
                    chunk.EndedAtUtc);

            if (overlapEndUtc <=
                overlapStartUtc)
            {
                continue;
            }

            int sourceStart =
                checked(
                    (int)Math.Round(
                        (
                            overlapStartUtc -
                            chunk.StartedAtUtc
                        )
                        .TotalSeconds *
                        SampleRate,
                        MidpointRounding.AwayFromZero));

            int destinationStart =
                checked(
                    (int)Math.Round(
                        (
                            overlapStartUtc -
                            evidenceStartUtc
                        )
                        .TotalSeconds *
                        SampleRate,
                        MidpointRounding.AwayFromZero));

            int requestedCount =
                checked(
                    (int)Math.Round(
                        (
                            overlapEndUtc -
                            overlapStartUtc
                        )
                        .TotalSeconds *
                        SampleRate,
                        MidpointRounding.AwayFromZero));

            sourceStart =
                Math.Clamp(
                    sourceStart,
                    0,
                    pcm.Length);

            destinationStart =
                Math.Clamp(
                    destinationStart,
                    0,
                    timeline.Length);

            int count =
                Math.Min(
                    requestedCount,
                    Math.Min(
                        pcm.Length -
                            sourceStart,
                        timeline.Length -
                            destinationStart));

            if (count <= 0)
            {
                continue;
            }

            Array.Copy(
                pcm,
                sourceStart,
                timeline,
                destinationStart,
                count);

            if (chunk.Id ==
                focusChunkId)
            {
                focusCopied =
                    true;
            }
        }

        if (!focusCopied)
        {
            throw new InvalidOperationException(
                "Focus chunk audio could not be copied into direct QA evidence.");
        }

        byte[] wave =
            CreateCanonicalWave(
                timeline);

        double durationSeconds =
            timeline.Length /
            (double)SampleRate;

        DateTimeOffset actualEndUtc =
            evidenceStartUtc
                .AddSeconds(
                    durationSeconds);

        return
            new EvidenceWave(
                wave,
                evidenceStartUtc,
                actualEndUtc,
                durationSeconds);
    }

    private static void
        ValidateCanonicalChunkMetadata(
            QaAudioChunk chunk)
    {
        if (chunk.FormatVersion != 1 ||
            chunk.SampleRate != SampleRate ||
            chunk.Channels != Channels ||
            chunk.BitsPerSample != BitsPerSample ||
            !string.Equals(
                chunk.ContentType,
                "audio/wav",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Direct QA context contains a non-canonical audio chunk.");
        }
    }

    private static short[]
        ParseCanonicalWave(
            byte[] wave,
            QaAudioChunk chunk)
    {
        if (wave.Length <
            WaveHeaderBytes)
        {
            throw new InvalidOperationException(
                "Direct QA WAV is truncated.");
        }

        if (Encoding.ASCII.GetString(
                wave,
                0,
                4) != "RIFF" ||
            Encoding.ASCII.GetString(
                wave,
                8,
                4) != "WAVE" ||
            Encoding.ASCII.GetString(
                wave,
                12,
                4) != "fmt " ||
            Encoding.ASCII.GetString(
                wave,
                36,
                4) != "data")
        {
            throw new InvalidOperationException(
                "Direct QA WAV has an unsupported header.");
        }

        int formatSize =
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    wave.AsSpan(
                        16,
                        4));

        short audioFormat =
            BinaryPrimitives
                .ReadInt16LittleEndian(
                    wave.AsSpan(
                        20,
                        2));

        short channels =
            BinaryPrimitives
                .ReadInt16LittleEndian(
                    wave.AsSpan(
                        22,
                        2));

        int sampleRate =
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    wave.AsSpan(
                        24,
                        4));

        short bitsPerSample =
            BinaryPrimitives
                .ReadInt16LittleEndian(
                    wave.AsSpan(
                        34,
                        2));

        int dataBytes =
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    wave.AsSpan(
                        40,
                        4));

        if (formatSize != 16 ||
            audioFormat != 1 ||
            channels != Channels ||
            sampleRate != SampleRate ||
            bitsPerSample != BitsPerSample ||
            dataBytes < 0 ||
            dataBytes % 2 != 0 ||
            dataBytes !=
                wave.Length -
                WaveHeaderBytes)
        {
            throw new InvalidOperationException(
                "Direct QA WAV does not match canonical PCM16 mono format.");
        }

        if (chunk.SizeBytes !=
            wave.LongLength)
        {
            throw new InvalidOperationException(
                "Direct QA chunk storage size does not match metadata.");
        }

        int samples =
            dataBytes /
            sizeof(short);

        double actualDuration =
            samples /
            (double)SampleRate;

        double metadataDuration =
            (
                chunk.EndedAtUtc -
                chunk.StartedAtUtc
            )
            .TotalSeconds;

        if (Math.Abs(
                actualDuration -
                metadataDuration) >
            0.05)
        {
            throw new InvalidOperationException(
                "Direct QA chunk duration does not match metadata.");
        }

        var pcm =
            new short[
                samples];

        for (
            int i = 0;
            i < samples;
            i++)
        {
            pcm[i] =
                BinaryPrimitives
                    .ReadInt16LittleEndian(
                        wave.AsSpan(
                            WaveHeaderBytes +
                            i * 2,
                            2));
        }

        return
            pcm;
    }

    private static byte[]
        CreateCanonicalWave(
            ReadOnlySpan<short> pcm)
    {
        int dataBytes =
            checked(
                pcm.Length *
                sizeof(short));

        byte[] wave =
            new byte[
                WaveHeaderBytes +
                dataBytes];

        Encoding.ASCII
            .GetBytes("RIFF")
            .CopyTo(
                wave,
                0);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(4, 4),
                36 +
                dataBytes);

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
                Channels);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(24, 4),
                SampleRate);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(28, 4),
                SampleRate *
                Channels *
                (BitsPerSample / 8));

        BinaryPrimitives
            .WriteInt16LittleEndian(
                wave.AsSpan(32, 2),
                (short)(
                    Channels *
                    (BitsPerSample / 8)));

        BinaryPrimitives
            .WriteInt16LittleEndian(
                wave.AsSpan(34, 2),
                BitsPerSample);

        Encoding.ASCII
            .GetBytes("data")
            .CopyTo(
                wave,
                36);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(40, 4),
                dataBytes);

        for (
            int i = 0;
            i < pcm.Length;
            i++)
        {
            BinaryPrimitives
                .WriteInt16LittleEndian(
                    wave.AsSpan(
                        WaveHeaderBytes +
                        i * 2,
                        2),
                    pcm[i]);
        }

        return
            wave;
    }

    private static void
        ValidateClaimOwnership(
            QaAudioChunk focus,
            DateTimeOffset requestedClaimUtc)
    {
        if (focus.ProcessedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Focus QA chunk is already processed.");
        }

        if (!focus.ClaimedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Focus QA chunk has no active claim.");
        }

        double deltaMilliseconds =
            Math.Abs(
                (
                    focus.ClaimedAtUtc.Value
                        .ToUniversalTime() -
                    requestedClaimUtc
                        .ToUniversalTime()
                )
                .TotalMilliseconds);

        if (deltaMilliseconds > 1.0)
        {
            throw new InvalidOperationException(
                "Focus QA chunk claim is no longer current.");
        }
    }

    private static void
        EnsureExistingDirectAlertMatches(
            QaAlert existing,
            Guid focusChunkId,
            Guid qaRuleId,
            string matchedPhrase,
            string transcript,
            string policyVersion,
            string analysisVersion,
            DateTimeOffset triggerStartUtc)
    {
        bool identical =
            existing.RecordingId is null &&
            existing.SourceQaAudioChunkId ==
                focusChunkId &&
            existing.QaRuleId ==
                qaRuleId &&
            string.Equals(
                existing.MatchedPhrase,
                matchedPhrase,
                StringComparison.Ordinal) &&
            existing.DetectionReason ==
                RestrictedRuleReason &&
            existing.Transcript ==
                transcript &&
            existing.PolicyVersion ==
                policyVersion &&
            existing.AnalysisVersion ==
                analysisVersion &&
            existing.TimestampUtc
                .ToUniversalTime() ==
                triggerStartUtc;

        if (!identical)
        {
            throw new InvalidOperationException(
                "Direct QA idempotency key belongs to different evidence.");
        }
    }

    private static bool IntervalsOverlap(
        DateTimeOffset leftStart,
        DateTimeOffset leftEnd,
        DateTimeOffset rightStart,
        DateTimeOffset rightEnd)
    {
        return
            leftEnd >
                rightStart &&
            leftStart <
                rightEnd;
    }

    private static DateTimeOffset Max(
        DateTimeOffset left,
        DateTimeOffset right)
    {
        return
            left >= right
                ? left
                : right;
    }

    private static DateTimeOffset Min(
        DateTimeOffset left,
        DateTimeOffset right)
    {
        return
            left <= right
                ? left
                : right;
    }

    private static string RequireText(
        string? value,
        string name,
        int maximumLength)
    {
        string normalized =
            value?.Trim() ??
            string.Empty;

        if (normalized.Length == 0 ||
            normalized.Length >
                maximumLength)
        {
            throw new ArgumentException(
                $"{name} is required and must be at most {maximumLength} characters.");
        }

        return
            normalized;
    }

    private sealed record EvidenceWave(
        byte[] WaveBytes,
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc,
        double DurationSeconds);
}
