namespace Academy.Agent.Teams;

public static class TeamsEvidenceProtocol
{
    public const int Version = 1;

    public const string PipeName =
        "AcademyAgent.TeamsEvidence.v1";

    public const string GetTarget =
        "GetTarget";

    public const string PublishEvidence =
        "PublishEvidence";
}

public sealed class TeamsPipeRequest
{
    public int Version { get; init; } =
        TeamsEvidenceProtocol.Version;

    public string Kind { get; init; } =
        string.Empty;

    public TeamsEvidenceEnvelope? Evidence { get; init; }
}

public sealed class TeamsPipeResponse
{
    public int Version { get; init; } =
        TeamsEvidenceProtocol.Version;

    public bool Ok { get; init; }

    public string? Error { get; init; }

    public TeamsObservationTarget? Target { get; init; }
}