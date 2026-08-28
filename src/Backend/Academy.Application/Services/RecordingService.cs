using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Exceptions;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class RecordingService
{
    private readonly IRecordingRepository _recordingRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _bucketName;

    public RecordingService(
        IRecordingRepository recordingRepository,
        IDeviceRepository deviceRepository,
        ISessionRepository sessionRepository,
        IStorageService storageService,
        IUnitOfWork unitOfWork,
        string bucketName)
    {
        _recordingRepository = recordingRepository;
        _deviceRepository = deviceRepository;
        _sessionRepository = sessionRepository;
        _storageService = storageService;
        _unitOfWork = unitOfWork;
        _bucketName = bucketName;
    }

    public async Task<RecordingResponse> SubmitRecordingAsync(
        RecordingSubmittedRequest request,
        CancellationToken cancellationToken = default)
    {
        TeacherAudioProvenanceStatus provenanceStatus =
            ValidateAndResolveProvenanceStatus(request);

        var device = await _deviceRepository.GetByDeviceIdAsync(request.DeviceId, cancellationToken)
            ?? throw new InvalidOperationException("Unknown device.");

        var duration = request.EndedAtUtc - request.StartedAtUtc;

        var existingRecording =
            await _recordingRepository.GetByDeviceAndFileNameAsync(
                device.Id,
                request.FileName,
                cancellationToken);

        if (existingRecording is not null)
        {
            EnsureIdenticalRetry(
                existingRecording,
                request,
                provenanceStatus);

            return new RecordingResponse
            {
                RecordingId = existingRecording.Id,
                Accepted = true,
                StorageKey = existingRecording.StorageKey
            };
        }

        var storageKey = $"recordings/{device.DeviceId}/{request.FileName}";

        var recording = new Recording
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            FileName = request.FileName,
            StorageKey = storageKey,
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            Duration = duration,
            SizeBytes = request.SizeBytes,
            AudioLayoutVersion = request.AudioLayoutVersion,
            TeacherAudioTrackIndex = request.TeacherAudioTrackIndex,
            TeacherAudioSourceKind =
                request.AudioLayoutVersion == 0
                    ? "Legacy"
                    : request.TeacherAudioSourceKind.Trim(),
            TeacherAudioEndpointId =
                NormalizeOptional(request.TeacherAudioEndpointId),
            TeacherAudioEndpointName =
                NormalizeOptional(request.TeacherAudioEndpointName),
            TeacherAudioCoverageStartedAtUtc =
                request.TeacherAudioCoverageStartedAtUtc,
            TeacherAudioProvenanceStatus = provenanceStatus,
            Status = RecordingStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        foreach (RecordingAudioCoverageGapRequest gap
                 in request.TeacherAudioCoverageGaps ?? [])
        {
            recording.TeacherAudioCoverageGaps.Add(
                new RecordingAudioCoverageGap
                {
                    Id = Guid.NewGuid(),
                    RecordingId = recording.Id,
                    StartedAtUtc = gap.StartedAtUtc,
                    EndedAtUtc = gap.EndedAtUtc,
                    Reason = gap.Reason.Trim(),
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
        }

        // Resolve active session for this device at the recording start time.
        var activeSession = await _sessionRepository.GetActiveSessionForDeviceAsync(
            device.Id,
            request.StartedAtUtc,
            cancellationToken);

        if (activeSession is not null)
        {
            recording.SessionId = activeSession.Id;
            recording.TeacherId = activeSession.TeacherId;
        }

        await _recordingRepository.AddAsync(recording, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RecordingResponse
        {
            RecordingId = recording.Id,
            Accepted = true,
            StorageKey = storageKey
        };
    }

    public async Task UploadRecordingAsync(
        Guid recordingId,
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var recording = await _recordingRepository.GetByIdAsync(recordingId, cancellationToken)
            ?? throw new InvalidOperationException("Recording not found.");

        await _storageService.UploadAsync(
            _bucketName,
            recording.StorageKey,
            fileStream,
            contentType,
            cancellationToken);

        recording.Status = RecordingStatus.Uploaded;
        recording.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecordingListItem>> GetRecordingListAsync(
        CancellationToken cancellationToken = default)
    {
        var recordings = await _recordingRepository.GetAllWithDeviceAsync(cancellationToken);

        return recordings
            .OrderByDescending(x => x.StartedAtUtc)
            .Select(x => new RecordingListItem
            {
                Id = x.Id,
                DeviceName = x.Device?.DeviceName ?? "Unknown",
                FileName = x.FileName,
                StorageKey = x.StorageKey,
                StartedAtUtc = x.StartedAtUtc,
                EndedAtUtc = x.EndedAtUtc,
                Duration = x.Duration,
                SizeBytes = x.SizeBytes,
                Status = x.Status.ToString(),
                AudioLayoutVersion = x.AudioLayoutVersion,
                TeacherAudioTrackIndex = x.TeacherAudioTrackIndex,
                TeacherAudioProvenanceStatus =
                    x.TeacherAudioProvenanceStatus.ToString(),
                TeacherAudioEndpointName =
                    x.TeacherAudioEndpointName
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PendingQaRecordingDto>> GetPendingQaRecordingsAsync(
        CancellationToken cancellationToken = default)
    {
        var recordings = await _recordingRepository.GetPendingQaAsync(cancellationToken);

        var result = new List<PendingQaRecordingDto>();

        foreach (var recording in recordings)
        {
            string presignedUrl = await _storageService.GetPresignedUrlAsync(
                _bucketName,
                recording.StorageKey,
                TimeSpan.FromMinutes(10),
                cancellationToken);

            result.Add(new PendingQaRecordingDto
            {
                RecordingId = recording.Id,
                FileName = recording.FileName,
                StorageKey = recording.StorageKey,
                PresignedUrl = presignedUrl,
                StartedAtUtc = recording.StartedAtUtc,
                AudioLayoutVersion = recording.AudioLayoutVersion,
                TeacherAudioTrackIndex =
                    recording.TeacherAudioTrackIndex!.Value,
                TeacherAudioProvenanceStatus =
                    recording.TeacherAudioProvenanceStatus.ToString()
            });
        }

        return result;
    }

    public async Task MarkQaProcessedAsync(
        Guid recordingId,
        CancellationToken cancellationToken = default)
    {
        var recording = await _recordingRepository.GetByIdAsync(recordingId, cancellationToken)
            ?? throw new InvalidOperationException("Recording not found.");

        recording.QaProcessedAtUtc = DateTimeOffset.UtcNow;
        recording.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _recordingRepository.Update(recording);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetPlaybackUrlAsync(
        Guid recordingId,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        var recording = await _recordingRepository.GetByIdAsync(recordingId, cancellationToken)
            ?? throw new InvalidOperationException("Recording not found.");

        if (recording.Status != RecordingStatus.Uploaded)
        {
            throw new RecordingUnavailableException(recording.Status);
        }

        return await _storageService.GetPresignedUrlAsync(
            _bucketName,
            recording.StorageKey,
            expiry,
            cancellationToken);
    }

    public async Task SetPreservedAsync(
        Guid recordingId,
        bool preserved,
        CancellationToken cancellationToken = default)
    {
        var recording =
            await _recordingRepository.GetByIdAsync(
                recordingId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Recording not found.");

        if (recording.Status == RecordingStatus.Deleted)
        {
            throw new InvalidOperationException(
                "Deleted recording cannot be preserved.");
        }

        recording.IsPreserved = preserved;
        recording.PreservedAtUtc =
            preserved ? DateTimeOffset.UtcNow : null;
        recording.UpdatedAtUtc =
            DateTimeOffset.UtcNow;

        _recordingRepository.Update(recording);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);
    }

    private static TeacherAudioProvenanceStatus
        ValidateAndResolveProvenanceStatus(
            RecordingSubmittedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DeviceId) ||
            string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException(
                "DeviceId and FileName are required.");
        }

        if (request.EndedAtUtc <= request.StartedAtUtc ||
            request.SizeBytes < 0)
        {
            throw new ArgumentException(
                "Recording timestamps and size are invalid.");
        }

        if (request.AudioLayoutVersion == 0)
        {
            if (request.TeacherAudioTrackIndex.HasValue ||
                request.TeacherAudioCoverageStartedAtUtc.HasValue ||
                (request.TeacherAudioCoverageGaps?.Count ?? 0) > 0)
            {
                throw new ArgumentException(
                    "Legacy recordings cannot declare teacher-audio provenance.");
            }

            return TeacherAudioProvenanceStatus.LegacyUnknown;
        }

        if (request.AudioLayoutVersion != 1 ||
            request.TeacherAudioTrackIndex != 1)
        {
            throw new ArgumentException(
                "Unsupported teacher-audio layout or track index.");
        }

        if (request.TeacherAudioSourceKind is not
            ("DefaultCommunicationsEndpoint" or
             "ConfiguredEndpoint"))
        {
            throw new ArgumentException(
                "TeacherAudioSourceKind is invalid.");
        }

        if (request.TeacherAudioEndpointId?.Length > 512 ||
            request.TeacherAudioEndpointName?.Length > 512)
        {
            throw new ArgumentException(
                "Teacher microphone endpoint metadata is too long.");
        }

        DateTimeOffset previousGapEnd =
            request.StartedAtUtc;

        foreach (RecordingAudioCoverageGapRequest gap
                 in (request.TeacherAudioCoverageGaps ?? [])
                     .OrderBy(x => x.StartedAtUtc))
        {
            if (gap.EndedAtUtc <= gap.StartedAtUtc ||
                gap.StartedAtUtc < request.StartedAtUtc ||
                gap.EndedAtUtc > request.EndedAtUtc ||
                gap.StartedAtUtc < previousGapEnd ||
                string.IsNullOrWhiteSpace(gap.Reason) ||
                gap.Reason.Length > 128)
            {
                throw new ArgumentException(
                    "Teacher-audio coverage gaps are invalid or overlapping.");
            }

            previousGapEnd = gap.EndedAtUtc;
        }

        TeacherAudioProvenanceStatus resolvedStatus;

        if (!request.TeacherAudioCoverageStartedAtUtc.HasValue)
        {
            resolvedStatus =
                TeacherAudioProvenanceStatus.Unavailable;
        }
        else
        {
            if (request.TeacherAudioCoverageStartedAtUtc <
                    request.StartedAtUtc ||
                request.TeacherAudioCoverageStartedAtUtc >
                    request.EndedAtUtc ||
                string.IsNullOrWhiteSpace(
                    request.TeacherAudioEndpointId) ||
                string.IsNullOrWhiteSpace(
                    request.TeacherAudioEndpointName))
            {
                throw new ArgumentException(
                    "Teacher microphone provenance metadata is incomplete.");
            }

            resolvedStatus =
                (request.TeacherAudioCoverageGaps?.Count ?? 0) == 0
                    ? TeacherAudioProvenanceStatus.Proven
                    : TeacherAudioProvenanceStatus.Partial;
        }

        if (!Enum.TryParse(
                request.TeacherAudioProvenanceStatus,
                ignoreCase: true,
                out TeacherAudioProvenanceStatus reportedStatus) ||
            reportedStatus != resolvedStatus)
        {
            throw new ArgumentException(
                "Teacher-audio provenance status does not match its evidence.");
        }

        return resolvedStatus;
    }

    private static void EnsureIdenticalRetry(
        Recording existing,
        RecordingSubmittedRequest request,
        TeacherAudioProvenanceStatus provenanceStatus)
    {
        bool scalarMatch =
            TimestampsEquivalent(
                existing.StartedAtUtc,
                request.StartedAtUtc) &&
            TimestampsEquivalent(
                existing.EndedAtUtc,
                request.EndedAtUtc) &&
            existing.SizeBytes == request.SizeBytes &&
            existing.AudioLayoutVersion == request.AudioLayoutVersion &&
            existing.TeacherAudioTrackIndex ==
                request.TeacherAudioTrackIndex &&
            string.Equals(
                existing.TeacherAudioSourceKind,
                request.AudioLayoutVersion == 0
                    ? "Legacy"
                    : request.TeacherAudioSourceKind.Trim(),
                StringComparison.Ordinal) &&
            string.Equals(
                existing.TeacherAudioEndpointId,
                NormalizeOptional(
                    request.TeacherAudioEndpointId),
                StringComparison.Ordinal) &&
            string.Equals(
                existing.TeacherAudioEndpointName,
                NormalizeOptional(
                    request.TeacherAudioEndpointName),
                StringComparison.Ordinal) &&
            NullableTimestampsEquivalent(
                existing.TeacherAudioCoverageStartedAtUtc,
                request.TeacherAudioCoverageStartedAtUtc) &&
            existing.TeacherAudioProvenanceStatus ==
                provenanceStatus;

        RecordingAudioCoverageGap[] existingGaps =
            existing.TeacherAudioCoverageGaps
                .OrderBy(x => x.StartedAtUtc)
                .ToArray();

        RecordingAudioCoverageGapRequest[] requestGaps =
            (request.TeacherAudioCoverageGaps ?? [])
                .OrderBy(x => x.StartedAtUtc)
                .ToArray();

        bool gapsMatch =
            existingGaps.Length == requestGaps.Length &&
            existingGaps.Zip(requestGaps)
                .All(pair =>
                    TimestampsEquivalent(
                        pair.First.StartedAtUtc,
                        pair.Second.StartedAtUtc) &&
                    TimestampsEquivalent(
                        pair.First.EndedAtUtc,
                        pair.Second.EndedAtUtc) &&
                    string.Equals(
                        pair.First.Reason,
                        pair.Second.Reason.Trim(),
                        StringComparison.Ordinal));

        if (!scalarMatch || !gapsMatch)
        {
            throw new InvalidOperationException(
                "Recording submission conflicts with existing idempotency state.");
        }
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool TimestampsEquivalent(
        DateTimeOffset left,
        DateTimeOffset right)
    {
        // PostgreSQL timestamp precision is one microsecond while .NET keeps
        // 100-nanosecond ticks. A retry after restart may round-trip through
        // PostgreSQL before the same pending sidecar is submitted again.
        return Math.Abs(
            (left.ToUniversalTime() - right.ToUniversalTime()).Ticks)
            < TimeSpan.TicksPerMicrosecond;
    }

    private static bool NullableTimestampsEquivalent(
        DateTimeOffset? left,
        DateTimeOffset? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return left.HasValue == right.HasValue;
        }

        return TimestampsEquivalent(
            left.Value,
            right.Value);
    }
}

