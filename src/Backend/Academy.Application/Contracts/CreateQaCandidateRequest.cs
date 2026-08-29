namespace Academy.Application.Contracts;

public sealed class CreateQaCandidateRequest
{
    public Guid RecordingId { get; init; }

    public Guid? QaRuleId { get; init; }

    public string PolicyVersion { get; init; } = string.Empty;

    public string AnalysisVersion { get; init; } = string.Empty;

    public int SourceTrackIndex { get; init; }

    public int AudioLayoutVersion { get; init; }

    public double TriggerStartSeconds { get; init; }

    public double TriggerEndSeconds { get; init; }

    public string Transcript { get; init; } = string.Empty;

    public string LanguageFamily { get; init; } = string.Empty;

    public string IntentCategory { get; init; } = string.Empty;

    public double? TriggerConfidence { get; init; }

    public double? AsrConfidence { get; init; }

    public double? IntentConfidence { get; init; }

    public string AnalysisIdempotencyKey { get; init; } = string.Empty;
}
