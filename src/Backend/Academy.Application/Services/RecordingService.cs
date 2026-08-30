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

        RecordingOverlapResolution overlap =
            await ResolveRecordingOverlapAsync(
                device.Id,
                request.StartedAtUtc,
                request.EndedAtUtc,
                cancellationToken);

        recording.SessionId = overlap.SessionId;
        recording.TeacherId = overlap.TeacherId;

        await _recordingRepository.AddAsync(recording, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RecordingResponse
        {
            RecordingId = recording.Id,
            Accepted = true,
            StorageKey = storageKey
        };
    }


    public async Task<ServerArchiveRegistrationResponse> RegisterServerArchiveAsync(
        ServerArchiveCompletedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DeviceId) ||
            string.IsNullOrWhiteSpace(request.FileName) ||
            string.IsNullOrWhiteSpace(request.StorageKey))
        {
            throw new ArgumentException(
                "DeviceId, FileName and StorageKey are required.");
        }

        if (request.FileName.Contains('/') ||
            request.FileName.Contains('\\') ||
            !request.FileName.EndsWith(
                ".mp4",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Server archive FileName must be a plain .mp4 file name.");
        }

        if (request.EndedAtUtc <= request.StartedAtUtc ||
            request.SizeBytes <= 0)
        {
            throw new ArgumentException(
                "Server archive timestamps and size are invalid.");
        }

        TimeSpan duration =
            request.EndedAtUtc - request.StartedAtUtc;

        if (duration > TimeSpan.FromMinutes(30))
        {
            throw new ArgumentException(
                "Server archive duration exceeds the supported pilot limit.");
        }

        string containerFormat =
            request.ContainerFormat?.Trim().ToLowerInvariant()
            ?? string.Empty;

        if (!request.VideoStreamCopyVerified ||
            !string.Equals(
                request.VideoCodec?.Trim(),
                "h264",
                StringComparison.OrdinalIgnoreCase) ||
            containerFormat is not ("fmp4" or "mp4"))
        {
            throw new ArgumentException(
                "Server archive must be verified H.264 stream-copy media.");
        }

        var device =
            await _deviceRepository.GetByDeviceIdAsync(
                request.DeviceId.Trim(),
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Unknown device.");

        string storagePrefix =
            $"server-recordings/{device.DeviceId}/";

        if (!request.StorageKey.StartsWith(
                storagePrefix,
                StringComparison.Ordinal) ||
            request.StorageKey.Contains(
                "..",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Server archive StorageKey is outside the allowed device prefix.");
        }

        var allRecordings =
            await _recordingRepository.GetAllAsync(
                cancellationToken);

        Recording? existing =
            allRecordings.FirstOrDefault(x =>
                x.DeviceId == device.Id &&
                TimestampsEquivalent(
                    x.StartedAtUtc,
                    request.StartedAtUtc) &&
                TimestampsEquivalent(
                    x.EndedAtUtc,
                    request.EndedAtUtc));

        RecordingOverlapResolution overlap =
            await ResolveRecordingOverlapAsync(
                device.Id,
                request.StartedAtUtc,
                request.EndedAtUtc,
                cancellationToken);

        if (existing is not null)
        {
            if (!IsIdenticalServerArchiveRetry(
                    existing,
                    request))
            {
                throw new InvalidOperationException(
                    "Server archive conflicts with existing absolute-time recording identity.");
            }

            return ToServerArchiveResponse(
                existing,
                overlap,
                alreadyRegistered: true);
        }

        var recording = new Recording
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            SessionId = overlap.SessionId,
            TeacherId = overlap.TeacherId,
            FileName = request.FileName.Trim(),
            StorageKey = request.StorageKey.Trim(),
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            Duration = duration,
            SizeBytes = request.SizeBytes,
            AudioLayoutVersion = 0,
            TeacherAudioTrackIndex = null,
            TeacherAudioSourceKind =
                "ServerArchiveMixedOnly",
            TeacherAudioEndpointId = null,
            TeacherAudioEndpointName = null,
            TeacherAudioCoverageStartedAtUtc = null,
            TeacherAudioProvenanceStatus =
                TeacherAudioProvenanceStatus.Unavailable,
            Status = RecordingStatus.Uploaded,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _recordingRepository.AddAsync(
            recording,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return ToServerArchiveResponse(
            recording,
            overlap,
            alreadyRegistered: false);
    }

    public async Task<ServerArchiveDeviceResolveResponse>
        ResolveServerArchiveDeviceAsync(
            ServerArchiveDeviceResolveRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string streamKey = request.StreamKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(streamKey))
        {
            throw new ArgumentException("StreamKey is required.");
        }

        var devices = await _deviceRepository.GetAllAsync(cancellationToken);

        Device[] matches = devices
            .Where(device =>
                !string.IsNullOrWhiteSpace(device.LiveKitStreamKey) &&
                string.Equals(device.LiveKitStreamKey, streamKey, StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        if (matches.Length == 0)
        {
            throw new InvalidOperationException("No device is mapped to the supplied stream key.");
        }

        if (matches.Length > 1)
        {
            throw new InvalidOperationException("Multiple devices are mapped to the supplied stream key.");
        }

        return new ServerArchiveDeviceResolveResponse
        {
            DeviceId = matches[0].DeviceId
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

    public async Task<bool> DeleteRecordingMediaAsync(
        Guid recordingId,
        Guid? deletedByUserId,
        string deletionReason,
        CancellationToken cancellationToken = default)
    {
        var recording =
            await _recordingRepository.GetByIdAsync(
                recordingId,
                cancellationToken);

        if (recording is null)
        {
            return false;
        }

        if (recording.Status == RecordingStatus.Deleted)
        {
            return true;
        }

        if (recording.Status != RecordingStatus.Deleting)
        {
            recording.Status = RecordingStatus.Deleting;
            recording.DeletedByUserId = deletedByUserId;
            recording.DeletionReason =
                string.IsNullOrWhiteSpace(deletionReason)
                    ? "Unknown"
                    : deletionReason.Trim();
            recording.UpdatedAtUtc = DateTimeOffset.UtcNow;

            _recordingRepository.Update(recording);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(recording.StorageKey))
        {
            await _storageService.DeleteAsync(
                _bucketName,
                recording.StorageKey,
                cancellationToken);
        }

        recording.Status = RecordingStatus.Deleted;
        recording.IsPreserved = false;
        recording.PreservedAtUtc = null;
        recording.DeletedAtUtc = DateTimeOffset.UtcNow;
        recording.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _recordingRepository.Update(recording);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
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


    private async Task<RecordingOverlapResolution>
        ResolveRecordingOverlapAsync(
            Guid deviceId,
            DateTimeOffset startedAtUtc,
            DateTimeOffset endedAtUtc,
            CancellationToken cancellationToken)
    {
        var allSessions =
            await _sessionRepository.GetAllWithDetailsAsync(
                cancellationToken)
            ?? Array.Empty<Session>();

        Session[] overlappingSessions =
            allSessions
                .Where(session =>
                    session.DeviceId == deviceId &&
                    session.Status is
                        SessionStatus.Live or
                        SessionStatus.Completed &&
                    session.StartedAtUtc < endedAtUtc &&
                    (!session.EndedAtUtc.HasValue ||
                     session.EndedAtUtc.Value > startedAtUtc))
                .OrderBy(session => session.StartedAtUtc)
                .ToArray();

        if (overlappingSessions.Length == 0)
        {
            Session? activeAtStart =
                await _sessionRepository
                    .GetActiveSessionForDeviceAsync(
                        deviceId,
                        startedAtUtc,
                        cancellationToken);

            if (activeAtStart is not null &&
                CoversWholeRecording(
                    activeAtStart,
                    startedAtUtc,
                    endedAtUtc))
            {
                overlappingSessions =
                    new[] { activeAtStart };
            }
        }

        int distinctTeacherCount =
            overlappingSessions
                .Select(x => x.TeacherId)
                .Distinct()
                .Count();

        bool managerSafeWholeSegment =
            overlappingSessions.Length == 1 &&
            CoversWholeRecording(
                overlappingSessions[0],
                startedAtUtc,
                endedAtUtc);

        return new RecordingOverlapResolution(
            managerSafeWholeSegment
                ? overlappingSessions[0].Id
                : null,
            managerSafeWholeSegment
                ? overlappingSessions[0].TeacherId
                : null,
            overlappingSessions.Length,
            distinctTeacherCount,
            managerSafeWholeSegment);
    }

    private static bool CoversWholeRecording(
        Session session,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc)
    {
        return
            session.StartedAtUtc <= startedAtUtc &&
            (!session.EndedAtUtc.HasValue ||
             session.EndedAtUtc.Value >= endedAtUtc);
    }

    private static bool IsIdenticalServerArchiveRetry(
        Recording existing,
        ServerArchiveCompletedRequest request)
    {
        return
            existing.Status == RecordingStatus.Uploaded &&
            existing.AudioLayoutVersion == 0 &&
            existing.TeacherAudioTrackIndex is null &&
            string.Equals(
                existing.TeacherAudioSourceKind,
                "ServerArchiveMixedOnly",
                StringComparison.Ordinal) &&
            existing.TeacherAudioProvenanceStatus ==
                TeacherAudioProvenanceStatus.Unavailable &&
            string.Equals(
                existing.FileName,
                request.FileName.Trim(),
                StringComparison.Ordinal) &&
            string.Equals(
                existing.StorageKey,
                request.StorageKey.Trim(),
                StringComparison.Ordinal) &&
            existing.SizeBytes == request.SizeBytes;
    }

    private static ServerArchiveRegistrationResponse
        ToServerArchiveResponse(
            Recording recording,
            RecordingOverlapResolution overlap,
            bool alreadyRegistered)
    {
        return new ServerArchiveRegistrationResponse
        {
            RecordingId = recording.Id,
            Accepted = true,
            AlreadyRegistered = alreadyRegistered,
            StorageKey = recording.StorageKey,
            SessionId = overlap.SessionId,
            TeacherId = overlap.TeacherId,
            OverlapSessionCount =
                overlap.OverlapSessionCount,
            DistinctTeacherCount =
                overlap.DistinctTeacherCount,
            ManagerSafeWholeSegment =
                overlap.ManagerSafeWholeSegment
        };
    }

    private sealed record RecordingOverlapResolution(
        Guid? SessionId,
        Guid? TeacherId,
        int OverlapSessionCount,
        int DistinctTeacherCount,
        bool ManagerSafeWholeSegment);

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

