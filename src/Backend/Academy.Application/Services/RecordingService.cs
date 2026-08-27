using Academy.Application.Abstractions;
using Academy.Application.Contracts;
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
            Status = RecordingStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

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
                Status = x.Status.ToString()
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
                StartedAtUtc = recording.StartedAtUtc
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
            throw new InvalidOperationException("Recording is not uploaded yet.");
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
}

