using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Academy.Agent.Teams;

namespace Academy.Agent.TeamsHelper;

internal sealed class TeamsEvidencePipeClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<TeamsObservationTarget?> GetTargetAsync(
        CancellationToken cancellationToken)
    {
        TeamsPipeResponse response =
            await SendAsync(
                new TeamsPipeRequest
                {
                    Kind =
                        TeamsEvidenceProtocol.GetTarget
                },
                cancellationToken);

        if (!response.Ok)
        {
            throw new InvalidOperationException(
                response.Error ??
                "Agent rejected target request.");
        }

        return response.Target;
    }

    public async Task PublishEvidenceAsync(
        TeamsEvidenceEnvelope evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            evidence);

        TeamsPipeResponse response =
            await SendAsync(
                new TeamsPipeRequest
                {
                    Kind =
                        TeamsEvidenceProtocol.PublishEvidence,

                    Evidence =
                        evidence
                },
                cancellationToken);

        if (!response.Ok)
        {
            throw new InvalidOperationException(
                response.Error ??
                "Agent rejected Teams evidence.");
        }
    }

    private static async Task<TeamsPipeResponse> SendAsync(
        TeamsPipeRequest request,
        CancellationToken cancellationToken)
    {
        await using var pipe =
            new NamedPipeClientStream(
                ".",
                TeamsEvidenceProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

        await pipe.ConnectAsync(
            3000,
            cancellationToken);

        using var reader =
            new StreamReader(
                pipe,
                new UTF8Encoding(false),
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

        using var writer =
            new StreamWriter(
                pipe,
                new UTF8Encoding(false),
                bufferSize: 4096,
                leaveOpen: true)
            {
                AutoFlush = true
            };

        string requestJson =
            JsonSerializer.Serialize(
                request,
                JsonOptions);

        await writer.WriteLineAsync(
            requestJson.AsMemory(),
            cancellationToken);

        string? responseJson =
            await reader.ReadLineAsync(
                cancellationToken);

        if (string.IsNullOrWhiteSpace(
                responseJson))
        {
            throw new InvalidOperationException(
                "Empty response from Academy Agent Teams IPC.");
        }

        return
            JsonSerializer.Deserialize<TeamsPipeResponse>(
                responseJson,
                JsonOptions)
            ??
            throw new InvalidOperationException(
                "Invalid Academy Agent Teams IPC response.");
    }
}