using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class RecordingService
{
    private readonly IRecordingRepository _recordingRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _bucketName;

    public RecordingService(
        IRecordingRepository recordingRepository,
        IDeviceRepository deviceRepository,
        IStorageService storageService,
        IUnitOfWork unitOfWork,
        string bucketName)
    {
        _recordingRepository = recordingRepository;
        _deviceRepository = deviceRepository;
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
}