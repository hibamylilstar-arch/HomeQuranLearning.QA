using System.Buffers.Binary;
using System.Text;
using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class QaLocalAlertService
{
    private const string CanonicalPhrase = "whatsapp";
    private const string RestrictedRuleReason = "Restricted Rule";

    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;
    private const int WaveHeaderBytes = 44;

    private const long MaximumEvidenceBytes = 2L * 1024L * 1024L;

    private static readonly TimeSpan MaximumTriggerDuration =
        TimeSpan.FromSeconds(10);

    private static readonly TimeSpan MaximumEvidenceDuration =
        TimeSpan.FromSeconds(40);

    private static readonly TimeSpan EvidenceRetention =
        TimeSpan.FromDays(7);

    private static readonly TimeSpan TimelineTolerance =
        TimeSpan.FromMilliseconds(100);

    private readonly IQaAlertRepository _alertRepository;
    private readonly IQaRuleRepository _ruleRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _bucketName;

    public QaLocalAlertService(
        IQaAlertRepository alertRepository,
        IQaRuleRepository ruleRepository,
        IDeviceRepository deviceRepository,
        ISessionRepository sessionRepository,
        IStorageService storageService,
        IUnitOfWork unitOfWork,
        string bucketName)
    {
        _alertRepository = alertRepository;
        _ruleRepository = ruleRepository;
        _deviceRepository = deviceRepository;
        _sessionRepository = sessionRepository;
        _storageService = storageService;
        _unitOfWork = unitOfWork;
        _bucketName =
            string.IsNullOrWhiteSpace(bucketName)
                ? throw new ArgumentException(
                    "Storage bucket is required.",
                    nameof(bucketName))
                : bucketName.Trim();
    }

    public async Task<AgentQaRestrictedRuleResponse>
        GetActiveRestrictedRuleAsync(
            string deviceId,
            CancellationToken cancellationToken = default)
    {
        string canonicalDeviceId =
            RequireText(
                deviceId,
                nameof(deviceId),
                256);

        _ =
            await _deviceRepository.GetByDeviceIdAsync(
                canonicalDeviceId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Unknown device.");

        IReadOnlyList<QaRule> rules =
            await _ruleRepository.GetAllAsync(
                cancellationToken);

        QaRule[] active =
            rules
                .Where(
                    x =>
                        x.IsActive &&
                        string.Equals(
                            x.Phrase.Trim(),
                            CanonicalPhrase,
                            StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (active.Length == 0)
        {
            return new AgentQaRestrictedRuleResponse
            {
                Enabled = false,
                QaRuleId = null,
                Phrase = CanonicalPhrase
            };
        }

        if (active.Length > 1)
        {
            throw new InvalidOperationException(
                "Multiple active WhatsApp QA rules exist.");
        }

        return new AgentQaRestrictedRuleResponse
        {
            Enabled = true,
            QaRuleId = active[0].Id,
            Phrase = CanonicalPhrase
        };
    }

    public async Task<DirectQaAlertResponse>
        CreateRestrictedAsync(
            CreateAgentLocalRestrictedQaAlertRequest request,
            Stream audio,
            string? contentType,
            long declaredSizeBytes,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audio);

        string deviceId =
            RequireText(
                request.DeviceId,
                nameof(request.DeviceId),
                256);

        if (request.SessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "SessionId is required.");
        }

        if (request.QaRuleId == Guid.Empty)
        {
            throw new ArgumentException(
                "QaRuleId is required.");
        }

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
            request.TriggerStartUtc.ToUniversalTime();

        DateTimeOffset triggerEndUtc =
            request.TriggerEndUtc.ToUniversalTime();

        DateTimeOffset evidenceStartUtc =
            request.EvidenceStartUtc.ToUniversalTime();

        if (request.TriggerStartUtc == default ||
            request.TriggerEndUtc == default ||
            request.EvidenceStartUtc == default ||
            triggerEndUtc <= triggerStartUtc)
        {
            throw new ArgumentException(
                "Trigger/evidence timestamps are invalid.");
        }

        if (triggerEndUtc - triggerStartUtc >
            MaximumTriggerDuration)
        {
            throw new ArgumentException(
                "Local restricted trigger duration is too large.");
        }

        if (declaredSizeBytes < WaveHeaderBytes ||
            declaredSizeBytes > MaximumEvidenceBytes)
        {
            throw new ArgumentException(
                "Local QA evidence WAV size is invalid.");
        }

        string normalizedContentType =
            NormalizeContentType(contentType);

        if (!IsSupportedWaveContentType(
                normalizedContentType))
        {
            throw new ArgumentException(
                "Local QA evidence must use audio/wav.");
        }

        Device device =
            await _deviceRepository.GetByDeviceIdAsync(
                deviceId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Unknown device.");

        Session session =
            await _sessionRepository.GetByIdWithDetailsAsync(
                request.SessionId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "QA evidence Session was not found.");

        if (session.DeviceId != device.Id)
        {
            throw new UnauthorizedAccessException(
                "Session does not belong to this device.");
        }

        if (session.Status is not SessionStatus.Live
            and not SessionStatus.Completed)
        {
            throw new InvalidOperationException(
                "Local QA evidence requires a Live or Completed Session.");
        }

        QaRule rule =
            await _ruleRepository.GetByIdAsync(
                request.QaRuleId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "QA rule was not found.");

        if (!rule.IsActive ||
            !string.Equals(
                rule.Phrase.Trim(),
                CanonicalPhrase,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The WhatsApp QA rule is not active.");
        }

        QaAlert? existing =
            await _alertRepository.GetByAnalysisIdempotencyKeyAsync(
                idempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.SessionId != session.Id ||
                existing.DeviceId != device.Id ||
                existing.QaRuleId != rule.Id ||
                !string.Equals(
                    existing.MatchedPhrase,
                    CanonicalPhrase,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "AnalysisIdempotencyKey is already used by different QA evidence.");
            }

            return new DirectQaAlertResponse
            {
                AlertId = existing.Id,
                Duplicate = true
            };
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
                "Uploaded local QA evidence length does not match declared file size.");
        }

        byte[] waveBytes =
            buffer.ToArray();

        WaveInfo wave =
            ValidateCanonicalWave(
                waveBytes);

        DateTimeOffset evidenceEndUtc =
            evidenceStartUtc +
            wave.Duration;

        var (sessionStartUtc, sessionEndUtc) =
            SessionWindowResolver.Resolve(
                session);

        if (triggerStartUtc <
                sessionStartUtc - TimelineTolerance ||
            triggerEndUtc >
                sessionEndUtc + TimelineTolerance)
        {
            throw new InvalidOperationException(
                "Local QA trigger is outside the scheduled Session window.");
        }

        if (evidenceStartUtc <
                sessionStartUtc - TimelineTolerance ||
            evidenceEndUtc >
                sessionEndUtc + TimelineTolerance)
        {
            throw new InvalidOperationException(
                "Local QA evidence is outside the scheduled Session window.");
        }

        if (triggerStartUtc <
                evidenceStartUtc - TimelineTolerance ||
            triggerEndUtc >
                evidenceEndUtc + TimelineTolerance)
        {
            throw new InvalidOperationException(
                "Local QA evidence does not contain the trigger interval.");
        }

        if (triggerStartUtc >
            DateTimeOffset.UtcNow.AddMinutes(5))
        {
            throw new ArgumentException(
                "TriggerStartUtc is too far in the future.");
        }

        Guid alertId =
            Guid.NewGuid();

        string storageKey =
            $"qa/evidence/{session.Id:D}/{alertId:D}.wav";

        DateTimeOffset nowUtc =
            DateTimeOffset.UtcNow;

        buffer.Position = 0;

        await _storageService.UploadAsync(
            _bucketName,
            storageKey,
            buffer,
            "audio/wav",
            cancellationToken);

        string? laptopName =
            !string.IsNullOrWhiteSpace(
                session.Device?.RecordingDisplayName)
                ? session.Device!.RecordingDisplayName
                : session.Device?.DeviceName;

        var alert =
            new QaAlert
            {
                Id = alertId,
                RecordingId = null,
                SourceQaAudioChunkId = null,
                QaRuleId = rule.Id,
                MatchedPhrase = CanonicalPhrase,
                TimestampUtc = triggerStartUtc,
                DetectionReason = RestrictedRuleReason,
                Transcript = transcript,
                PolicyVersion = policyVersion,
                AnalysisVersion = analysisVersion,
                SourceTrackIndex = null,
                AudioLayoutVersion = 1,
                TriggerStartSeconds =
                    (triggerStartUtc - evidenceStartUtc)
                        .TotalSeconds,
                TriggerEndSeconds =
                    (triggerEndUtc - evidenceStartUtc)
                        .TotalSeconds,
                EvidenceStartSeconds = 0,
                EvidenceEndSeconds =
                    wave.Duration.TotalSeconds,
                AnalysisIdempotencyKey =
                    idempotencyKey,
                EvidenceStorageKey =
                    storageKey,
                EvidenceContentType =
                    "audio/wav",
                EvidenceSizeBytes =
                    waveBytes.LongLength,
                EvidenceDurationSeconds =
                    wave.Duration.TotalSeconds,
                EvidenceStartUtc =
                    evidenceStartUtc,
                EvidenceEndUtc =
                    evidenceEndUtc,
                EvidenceDeleteAfterUtc =
                    nowUtc + EvidenceRetention,
                DeviceId =
                    device.Id,
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
                    session.Device?.DeviceName,
                TeacherName =
                    session.Teacher?.FullName,
                StudentName =
                    session.Student?.FullName,
                CourseName =
                    session.Course?.Name,
                Status =
                    QaAlertStatus.Open,
                CreatedAtUtc =
                    nowUtc,
                UpdatedAtUtc =
                    nowUtc
            };

        try
        {
            await _alertRepository.AddAsync(
                alert,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
        catch
        {
            try
            {
                await _storageService.DeleteAsync(
                    _bucketName,
                    storageKey,
                    cancellationToken);
            }
            catch
            {
                // Preserve the persistence exception.
            }

            throw;
        }

        return new DirectQaAlertResponse
        {
            AlertId = alert.Id,
            Duplicate = false
        };
    }

    private static WaveInfo ValidateCanonicalWave(
        byte[] bytes)
    {
        if (bytes.Length < WaveHeaderBytes ||
            !HasTag(bytes, 0, "RIFF") ||
            !HasTag(bytes, 8, "WAVE") ||
            !HasTag(bytes, 12, "fmt ") ||
            BinaryPrimitives.ReadInt32LittleEndian(
                bytes.AsSpan(16, 4)) != 16 ||
            !HasTag(bytes, 36, "data"))
        {
            throw new ArgumentException(
                "Local QA evidence is not a canonical PCM WAV file.");
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

        int byteRate =
            BinaryPrimitives.ReadInt32LittleEndian(
                bytes.AsSpan(28, 4));

        ushort blockAlign =
            BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(32, 2));

        ushort bitsPerSample =
            BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(34, 2));

        int dataSize =
            BinaryPrimitives.ReadInt32LittleEndian(
                bytes.AsSpan(40, 4));

        if (audioFormat != 1 ||
            channels != Channels ||
            sampleRate != SampleRate ||
            bitsPerSample != BitsPerSample ||
            blockAlign != 2 ||
            byteRate != 32000)
        {
            throw new ArgumentException(
                "Local QA WAV must be PCM 16 kHz mono 16-bit.");
        }

        if (dataSize <= 0 ||
            dataSize != bytes.Length - WaveHeaderBytes ||
            dataSize % blockAlign != 0)
        {
            throw new ArgumentException(
                "Local QA WAV data length is invalid.");
        }

        double durationSeconds =
            dataSize /
            (double)byteRate;

        if (durationSeconds < 0.02 ||
            durationSeconds >
                MaximumEvidenceDuration.TotalSeconds)
        {
            throw new ArgumentException(
                "Local QA evidence duration must be between 20 ms and 40 seconds.");
        }

        return new WaveInfo(
            TimeSpan.FromSeconds(
                durationSeconds));
    }

    private static bool HasTag(
        byte[] bytes,
        int offset,
        string expected)
    {
        return
            Encoding.ASCII.GetString(
                bytes,
                offset,
                expected.Length) ==
            expected;
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

    private static string RequireText(
        string? value,
        string name,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{name} is required.");
        }

        string trimmed =
            value.Trim();

        if (trimmed.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{name} is too long.");
        }

        return trimmed;
    }

    private readonly record struct WaveInfo(
        TimeSpan Duration);
}
