using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class RecordingService
{
    private readonly IRecordingRepository _recordingRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordingService(
        IRecordingRepository recordingRepository,
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork)
    {
        _recordingRepository = recordingRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
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

    public async Task<IReadOnlyList<RecordingResponse>> GetRecordingsAsync(
        CancellationToken cancellationToken = default)
    {
        var recordings = await _recordingRepository.GetAllAsync(cancellationToken);

        return recordings
            .Select(x => new RecordingResponse
            {
                RecordingId = x.Id,
                Accepted = true,
                StorageKey = x.StorageKey
            })
            .ToList();
    }
}