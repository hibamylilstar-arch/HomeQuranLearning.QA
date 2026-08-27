using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Academy.Agent.Teams;

namespace Academy.Agent.Service;

public sealed class TeamsEvidencePipeServer :
    BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string ExpectedHelperProcessName =
        "Academy.Agent.TeamsHelper";

    private readonly ILogger<TeamsEvidencePipeServer> _logger;
    private readonly TeamsObservationTargetState _targetState;
    private readonly TeamsEvidenceInbox _inbox;

    public TeamsEvidencePipeServer(
        ILogger<TeamsEvidencePipeServer> logger,
        TeamsObservationTargetState targetState,
        TeamsEvidenceInbox inbox)
    {
        _logger =
            logger;

        _targetState =
            targetState;

        _inbox =
            inbox;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Teams evidence IPC server starting. Pipe={PipeName}",
            TeamsEvidenceProtocol.PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOneConnectionAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Teams evidence IPC connection failed.");

                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    stoppingToken);
            }
        }

        _logger.LogInformation(
            "Teams evidence IPC server stopped.");
    }

    private async Task RunOneConnectionAsync(
        CancellationToken cancellationToken)
    {
        PipeSecurity security =
            CreatePipeSecurity();

        await using NamedPipeServerStream pipe =
            NamedPipeServerStreamAcl.Create(
                TeamsEvidenceProtocol.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                4096,
                4096,
                security);

        await pipe.WaitForConnectionAsync(
            cancellationToken);

        if (!ValidateConnectedClient(
                pipe,
                out string? validationError))
        {
            _logger.LogWarning(
                "Rejected Teams evidence IPC client. Reason={Reason}",
                validationError);

            return;
        }

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

        string? line =
            await reader.ReadLineAsync(
                cancellationToken);

        TeamsPipeResponse response =
            string.IsNullOrWhiteSpace(line)
                ? Fail("Empty request.")
                : ProcessRequest(line);

        string json =
            JsonSerializer.Serialize(
                response,
                JsonOptions);

        await writer.WriteLineAsync(
            json.AsMemory(),
            cancellationToken);
    }

    private TeamsPipeResponse ProcessRequest(
        string json)
    {
        TeamsPipeRequest? request;

        try
        {
            request =
                JsonSerializer.Deserialize<TeamsPipeRequest>(
                    json,
                    JsonOptions);
        }
        catch (JsonException)
        {
            return Fail(
                "Invalid JSON.");
        }

        if (request is null)
        {
            return Fail(
                "Invalid request.");
        }

        if (request.Version !=
            TeamsEvidenceProtocol.Version)
        {
            return Fail(
                "Unsupported protocol version.");
        }

        if (string.Equals(
                request.Kind,
                TeamsEvidenceProtocol.GetTarget,
                StringComparison.Ordinal))
        {
            return new TeamsPipeResponse
            {
                Ok = true,
                Target =
                    _targetState.GetCurrent()
            };
        }

        if (string.Equals(
                request.Kind,
                TeamsEvidenceProtocol.PublishEvidence,
                StringComparison.Ordinal))
        {
            return AcceptEvidence(
                request.Evidence);
        }

        return Fail(
            "Unsupported request kind.");
    }

    private TeamsPipeResponse AcceptEvidence(
        TeamsEvidenceEnvelope? evidence)
    {
        if (evidence is null)
        {
            return Fail(
                "Evidence payload missing.");
        }

        if (string.IsNullOrWhiteSpace(
                evidence.IdempotencyKey) ||
            evidence.IdempotencyKey.Length > 256)
        {
            return Fail(
                "Evidence idempotency key is invalid.");
        }

        if (evidence.StudentDisplayName.Length > 200)
        {
            return Fail(
                "Student display name is too long.");
        }

        if (evidence.MessageId?.Length > 100)
        {
            return Fail(
                "Message ID is too long.");
        }

        if (evidence.AttachmentName?.Length > 260)
        {
            return Fail(
                "Attachment name is too long.");
        }

        if (evidence.Details?.Length > 512)
        {
            return Fail(
                "Evidence details are too long.");
        }

        TeamsObservationTarget? target =
            _targetState.GetCurrent();

        if (target is null)
        {
            return Fail(
                "No scheduled class is currently being observed.");
        }

        if (evidence.SessionId != target.SessionId ||
            evidence.DeviceId != target.DeviceId ||
            evidence.TeacherId != target.TeacherId ||
            evidence.StudentId != target.StudentId)
        {
            _logger.LogWarning(
                "Rejected mismatched Teams evidence. " +
                "EvidenceSession={EvidenceSession}, " +
                "ExpectedSession={ExpectedSession}, " +
                "EvidenceStudent={EvidenceStudent}, " +
                "ExpectedStudent={ExpectedStudent}",
                evidence.SessionId,
                target.SessionId,
                evidence.StudentId,
                target.StudentId);

            return Fail(
                "Evidence does not match the active scheduled class.");
        }

        if (!string.Equals(
                evidence.StudentDisplayName.Trim(),
                target.StudentFullName.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return Fail(
                "Student display name does not match the scheduled class.");
        }

        DateTimeOffset earliest =
            target.ScheduledStartUtc.AddMinutes(-5);

        DateTimeOffset latest =
            target.ScheduledEndUtc.AddMinutes(15);

        if (evidence.OccurredAtUtc < earliest ||
            evidence.OccurredAtUtc > latest)
        {
            return Fail(
                "Evidence timestamp is outside the allowed class window.");
        }

        if (!_inbox.TryPublish(
                evidence))
        {
            return Fail(
                "Evidence inbox unavailable.");
        }

        _logger.LogInformation(
            "Teams evidence accepted locally. " +
            "SessionId={SessionId}, StudentId={StudentId}, " +
            "Type={Type}, EvidenceId={EvidenceId}",
            evidence.SessionId,
            evidence.StudentId,
            evidence.Type,
            evidence.EvidenceId);

        return new TeamsPipeResponse
        {
            Ok = true
        };
    }

    private static PipeSecurity CreatePipeSecurity()
    {
        WindowsIdentity identity =
            WindowsIdentity.GetCurrent();

        SecurityIdentifier currentSid =
            identity.User
            ??
            throw new InvalidOperationException(
                "Agent Windows identity SID is unavailable.");

        var security =
            new PipeSecurity();

        security.SetOwner(
            currentSid);

        security.SetAccessRuleProtection(
            isProtected: true,
            preserveInheritance: false);

        security.AddAccessRule(
            new PipeAccessRule(
                currentSid,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

        var systemSid =
            new SecurityIdentifier(
                WellKnownSidType.LocalSystemSid,
                null);

        security.AddAccessRule(
            new PipeAccessRule(
                systemSid,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

        var interactiveSid =
            new SecurityIdentifier(
                WellKnownSidType.InteractiveSid,
                null);

        security.AddAccessRule(
            new PipeAccessRule(
                interactiveSid,
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));

        return security;
    }

    private static bool ValidateConnectedClient(
        NamedPipeServerStream pipe,
        out string? error)
    {
        error =
            null;

        if (!GetNamedPipeClientProcessId(
                pipe.SafePipeHandle,
                out uint clientPid))
        {
            error =
                $"Could not resolve named-pipe client PID. Win32={Marshal.GetLastWin32Error()}";

            return false;
        }

        Process process;

        try
        {
            process =
                Process.GetProcessById(
                    checked((int)clientPid));
        }
        catch
        {
            error =
                "Named-pipe client process no longer exists.";

            return false;
        }

        using (process)
        {
            if (!string.Equals(
                    process.ProcessName,
                    ExpectedHelperProcessName,
                    StringComparison.OrdinalIgnoreCase))
            {
                error =
                    $"Unexpected client process: {process.ProcessName}";

                return false;
            }

            uint activeConsoleSession =
                WTSGetActiveConsoleSessionId();

            if (activeConsoleSession != uint.MaxValue &&
                process.SessionId !=
                    checked((int)activeConsoleSession))
            {
                error =
                    $"Helper session {process.SessionId} is not active console session {activeConsoleSession}.";

                return false;
            }
        }

        return true;
    }

    private static TeamsPipeResponse Fail(
        string error)
    {
        return new TeamsPipeResponse
        {
            Ok = false,
            Error = error
        };
    }

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern bool GetNamedPipeClientProcessId(
        Microsoft.Win32.SafeHandles.SafePipeHandle Pipe,
        out uint ClientProcessId);

    [DllImport(
        "kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();
}