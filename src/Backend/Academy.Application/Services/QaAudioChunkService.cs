using System.Buffers.Binary;
using System.Text;
using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class QaAudioChunkService
{
    public const int FormatVersion = 1;
    public const int SampleRate = 16000;
    public const int Channels = 1;
    public const int BitsPerSample = 16;

    public const long MaxChunkBytes =
        512L * 1024L;

    private readonly IQaAudioChunkRepository _chunkRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _bucketName;

    public QaAudioChunkService(
        IQaAudioChunkRepository chunkRepository,
        IDeviceRepository deviceRepository,
        ISessionRepository sessionRepository,
        IStorageService storageService,
        IUnitOfWork unitOfWork,
        string bucketName)
    {
        _chunkRepository = chunkRepository;
        _deviceRepository = deviceRepository;
        _sessionRepository = sessionRepository;
        _storageService = storageService;
        _unitOfWork = unitOfWork;
        _bucketName = bucketName;
    }

    public async Task<AgentQaAudioChunkResponse> SubmitAsync(
        string deviceId,
        Guid sessionId,
        Guid captureId,
        long sequenceNumber,
        DateTimeOffset startedAtUtc,
        Stream audio,
        string? contentType,
        long declaredSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException(
                "DeviceId is required.");
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "SessionId is required.");
        }

        if (captureId == Guid.Empty)
        {
            throw new ArgumentException(
                "CaptureId is required.");
        }

        if (sequenceNumber < 0)
        {
            throw new ArgumentException(
                "SequenceNumber must be zero or greater.");
        }

        if (startedAtUtc == default)
        {
            throw new ArgumentException(
                "StartedAtUtc is required.");
        }

        Device device =
            await _deviceRepository.GetByDeviceIdAsync(
                deviceId.Trim(),
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Unknown device.");

        Session session =
            await _sessionRepository.GetByIdAsync(
                sessionId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Session not found.");

        // Agent evidence is never allowed to cross devices.
        if (session.DeviceId != device.Id)
        {
            throw new UnauthorizedAccessException(
                "Session does not belong to this device.");
        }

        DateTimeOffset canonicalStartedAtUtc =
            startedAtUtc.ToUniversalTime();

        QaAudioChunk? existing =
            await _chunkRepository.GetByIdentityAsync(
                device.Id,
                session.Id,
                captureId,
                sequenceNumber,
                cancellationToken);

        if (existing is not null)
        {
            double startDeltaMilliseconds =
                Math.Abs(
                    (
                        existing.StartedAtUtc -
                        canonicalStartedAtUtc
                    ).TotalMilliseconds);

            bool identicalIdentity =
                startDeltaMilliseconds <= 50;

            if (!identicalIdentity)
            {
                throw new InvalidOperationException(
                    "CaptureId/SequenceNumber collision with different QA evidence.");
            }

            return new AgentQaAudioChunkResponse
            {
                ChunkId = existing.Id,
                Accepted = true,
                Duplicate = true
            };
        }

        if (declaredSizeBytes < 44 ||
            declaredSizeBytes > MaxChunkBytes)
        {
            throw new ArgumentException(
                $"QA audio chunk must be between 44 and {MaxChunkBytes} bytes.");
        }

        string normalizedContentType =
            NormalizeContentType(
                contentType);

        if (!IsSupportedWaveContentType(
                normalizedContentType))
        {
            throw new ArgumentException(
                "QA audio chunk must use audio/wav.");
        }

        using var buffer =
            new MemoryStream(
                checked((int)declaredSizeBytes));

        await audio.CopyToAsync(
            buffer,
            cancellationToken);

        if (buffer.Length != declaredSizeBytes)
        {
            throw new ArgumentException(
                "Uploaded QA audio length does not match declared file size.");
        }

        WaveInfo wave =
            ValidateWave(
                buffer.ToArray());

        DateTimeOffset endedAtUtc =
            canonicalStartedAtUtc +
            wave.Duration;

        DateTimeOffset nowUtc =
            DateTimeOffset.UtcNow;

        if (canonicalStartedAtUtc >
            nowUtc.AddMinutes(5))
        {
            throw new ArgumentException(
                "StartedAtUtc is too far in the future.");
        }

        // QA evidence is only valid for a real class lifecycle.
        // Scheduled/Cancelled sessions must never create QA findings.
        if (session.Status is not SessionStatus.Live
            and not SessionStatus.Completed)
        {
            throw new ArgumentException(
                "QA audio is only accepted for Live or Completed sessions.");
        }

        // Schedule/Session is the authoritative class boundary.
        // The Agent must flush a partial final chunk rather than sending
        // unrelated audio beyond the immutable Session window.
        if (canonicalStartedAtUtc <
                session.ScheduledStartUtc ||
            endedAtUtc >
                session.ScheduledEndUtc
                    .AddMilliseconds(100))
        {
            throw new ArgumentException(
                "QA audio chunk must be inside the scheduled Session window.");
        }

        string storageKey =
            $"qa/chunks/{session.Id:D}/{captureId:D}/{sequenceNumber:D12}.wav";

        buffer.Position = 0;

        // Upload first using a deterministic object key. If persistence is
        // interrupted afterwards, an idempotent Agent retry simply overwrites
        // the same object and finishes the metadata transaction.
        await _storageService.UploadAsync(
            _bucketName,
            storageKey,
            buffer,
            "audio/wav",
            cancellationToken);

        DateTimeOffset retentionBaseUtc =
            session.ScheduledEndUtc > nowUtc
                ? session.ScheduledEndUtc
                : nowUtc;

        var chunk = new QaAudioChunk
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            SessionId = session.Id,
            CaptureId = captureId,
            SequenceNumber = sequenceNumber,
            StartedAtUtc = canonicalStartedAtUtc,
            EndedAtUtc = endedAtUtc,
            StorageKey = storageKey,
            ContentType = "audio/wav",
            SizeBytes = buffer.Length,
            FormatVersion = FormatVersion,
            SampleRate = wave.SampleRate,
            Channels = wave.Channels,
            BitsPerSample = wave.BitsPerSample,
            DeleteAfterUtc =
                retentionBaseUtc.AddHours(24),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        await _chunkRepository.AddAsync(
            chunk,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new AgentQaAudioChunkResponse
        {
            ChunkId = chunk.Id,
            Accepted = true,
            Duplicate = false
        };
    }

    private static string NormalizeContentType(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Split(';', 2)[0]
            .Trim()
            .ToLowerInvariant();
    }

    private static bool IsSupportedWaveContentType(
        string contentType)
    {
        return
            contentType == "audio/wav" ||
            contentType == "audio/x-wav" ||
            contentType == "audio/wave";
    }

    private static WaveInfo ValidateWave(
        byte[] bytes)
    {
        if (bytes.Length < 44 ||
            !HasTag(bytes, 0, "RIFF") ||
            !HasTag(bytes, 8, "WAVE") ||
            !HasTag(bytes, 12, "fmt ") ||
            !HasTag(bytes, 36, "data"))
        {
            throw new ArgumentException(
                "QA audio chunk is not a supported PCM WAV file.");
        }

        ushort audioFormat =
            BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(20, 2));

        ushort channels =
            BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(22, 2));

        int sampleRate =
            BinaryPrimitives.ReadInt32LittleEndian(
                bytes.AsSpan(24, 4));

        ushort bitsPerSample =
            BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(34, 2));

        int dataSize =
            BinaryPrimitives.ReadInt32LittleEndian(
                bytes.AsSpan(40, 4));

        if (audioFormat != 1 ||
            sampleRate != SampleRate ||
            channels != Channels ||
            bitsPerSample != BitsPerSample)
        {
            throw new ArgumentException(
                "QA WAV must be PCM 16 kHz mono 16-bit.");
        }

        if (dataSize <= 0 ||
            dataSize > bytes.Length - 44)
        {
            throw new ArgumentException(
                "QA WAV data length is invalid.");
        }

        double bytesPerSecond =
            sampleRate *
            channels *
            (bitsPerSample / 8.0);

        double durationSeconds =
            dataSize / bytesPerSecond;

        // Normal chunks will be 5 seconds. Short final chunks are valid
        // when a scheduled Session ends between chunk boundaries.
        if (durationSeconds < 0.02 ||
            durationSeconds > 10.0)
        {
            throw new ArgumentException(
                "QA WAV duration must be between 20 ms and 10 seconds.");
        }

        return new WaveInfo(
            sampleRate,
            channels,
            bitsPerSample,
            TimeSpan.FromSeconds(
                durationSeconds));
    }

    private static bool HasTag(
        byte[] bytes,
        int offset,
        string expected)
    {
        return Encoding.ASCII.GetString(
            bytes,
            offset,
            expected.Length) == expected;
    }

    private readonly record struct WaveInfo(
        int SampleRate,
        int Channels,
        int BitsPerSample,
        TimeSpan Duration);
}
