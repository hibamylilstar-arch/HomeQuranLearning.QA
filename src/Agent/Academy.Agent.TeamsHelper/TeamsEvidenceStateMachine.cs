using Academy.Agent.Teams;

namespace Academy.Agent.TeamsHelper;

internal sealed class TeamsEvidenceStateMachine
{
    private readonly HashSet<string> _emittedKeys =
        new(
            StringComparer.Ordinal);

    private Guid? _activeSessionId;

    private bool _hasInitialCallSnapshot;

    private string _previousCallState =
        "Unknown";

    private string? _callCycleId;

    public void Reset()
    {
        _activeSessionId =
            null;

        _emittedKeys.Clear();

        _hasInitialCallSnapshot =
            false;

        _previousCallState =
            "Unknown";

        _callCycleId =
            null;
    }

    public IReadOnlyList<TeamsEvidenceEnvelope> Evaluate(
        TeamsObservationTarget target,
        TeamsUiSnapshot snapshot,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        ArgumentNullException.ThrowIfNull(
            snapshot);

        if (_activeSessionId !=
            target.SessionId)
        {
            Reset();

            _activeSessionId =
                target.SessionId;
        }

        var output =
            new List<TeamsEvidenceEnvelope>();

        if (!snapshot.ChatBound)
        {
            return output;
        }

        AddMessageEvidence(
            target,
            snapshot.Greetings,
            TeamsEvidenceType.TeacherGreetingSent,
            "greeting",
            output);

        AddMessageEvidence(
            target,
            snapshot.Lessons,
            TeamsEvidenceType.LessonShared,
            "lesson",
            output);

        AddCallEvidence(
            target,
            snapshot,
            nowUtc,
            output);

        return output;
    }

    private void AddMessageEvidence(
        TeamsObservationTarget target,
        IReadOnlyList<TeamsDetectedMessage> messages,
        TeamsEvidenceType type,
        string keyType,
        List<TeamsEvidenceEnvelope> output)
    {
        foreach (TeamsDetectedMessage message in messages)
        {
            if (!message.OccurredAtUtc.HasValue)
            {
                continue;
            }

            DateTimeOffset occurredAtUtc =
                message.OccurredAtUtc.Value;

            if (!IsInsideEvidenceWindow(
                    target,
                    occurredAtUtc))
            {
                continue;
            }

            string key =
                $"teams:{keyType}:{target.SessionId:D}:{message.MessageId}";

            if (!_emittedKeys.Add(
                    key))
            {
                continue;
            }

            output.Add(
                new TeamsEvidenceEnvelope
                {
                    IdempotencyKey =
                        key,

                    Type =
                        type,

                    OccurredAtUtc =
                        occurredAtUtc,

                    SessionId =
                        target.SessionId,

                    DeviceId =
                        target.DeviceId,

                    TeacherId =
                        target.TeacherId,

                    StudentId =
                        target.StudentId,

                    StudentDisplayName =
                        target.StudentFullName,

                    MessageId =
                        message.MessageId,

                    AttachmentName =
                        message.AttachmentName,

                    Details =
                        type ==
                            TeamsEvidenceType.LessonShared
                            ? "Source=TeamsUIAutomation;Signal=LessonShared;Attachment=true"
                            : "Source=TeamsUIAutomation;Signal=TeacherGreeting"
                });
        }
    }

    private void AddCallEvidence(
        TeamsObservationTarget target,
        TeamsUiSnapshot snapshot,
        DateTimeOffset nowUtc,
        List<TeamsEvidenceEnvelope> output)
    {
        string currentState =
            snapshot.CallState;

        if (!_hasInitialCallSnapshot)
        {
            _hasInitialCallSnapshot =
                true;

            _previousCallState =
                currentState;

            return;
        }

        if (
            string.Equals(
                currentState,
                "Attempting",
                StringComparison.Ordinal) &&
            !string.Equals(
                _previousCallState,
                "Attempting",
                StringComparison.Ordinal) &&
            !string.Equals(
                _previousCallState,
                "Connected",
                StringComparison.Ordinal)
        )
        {
            BeginCallCycle(
                nowUtc);

            AddCallEvent(
                target,
                TeamsEvidenceType.CallAttempted,
                "attempt",
                nowUtc,
                output);
        }

        if (
            string.Equals(
                currentState,
                "Connected",
                StringComparison.Ordinal) &&
            !string.Equals(
                _previousCallState,
                "Connected",
                StringComparison.Ordinal)
        )
        {
            if (string.IsNullOrWhiteSpace(
                    _callCycleId))
            {
                BeginCallCycle(
                    nowUtc);

                AddCallEvent(
                    target,
                    TeamsEvidenceType.CallAttempted,
                    "attempt",
                    nowUtc,
                    output);
            }

            AddCallEvent(
                target,
                TeamsEvidenceType.StudentCallConnected,
                "connected",
                nowUtc,
                output);
        }

        bool previousWasActive =
            string.Equals(
                _previousCallState,
                "Attempting",
                StringComparison.Ordinal) ||
            string.Equals(
                _previousCallState,
                "Connected",
                StringComparison.Ordinal);

        bool currentIsEnded =
            string.Equals(
                currentState,
                "Available",
                StringComparison.Ordinal) ||
            string.Equals(
                currentState,
                "Idle",
                StringComparison.Ordinal);

        if (
            previousWasActive &&
            currentIsEnded &&
            !snapshot.CallingControlsVisible &&
            !string.IsNullOrWhiteSpace(
                _callCycleId)
        )
        {
            AddCallEvent(
                target,
                TeamsEvidenceType.CallEnded,
                "ended",
                nowUtc,
                output);

            _callCycleId =
                null;
        }

        _previousCallState =
            currentState;
    }

    private void BeginCallCycle(
        DateTimeOffset nowUtc)
    {
        _callCycleId =
            nowUtc.ToUnixTimeMilliseconds()
                .ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
            "-" +
            Guid.NewGuid().ToString("N");
    }

    private void AddCallEvent(
        TeamsObservationTarget target,
        TeamsEvidenceType type,
        string keyType,
        DateTimeOffset nowUtc,
        List<TeamsEvidenceEnvelope> output)
    {
        if (string.IsNullOrWhiteSpace(
                _callCycleId))
        {
            return;
        }

        string key =
            $"teams:call:{keyType}:{target.SessionId:D}:{_callCycleId}";

        if (!_emittedKeys.Add(
                key))
        {
            return;
        }

        output.Add(
            new TeamsEvidenceEnvelope
            {
                IdempotencyKey =
                    key,

                Type =
                    type,

                OccurredAtUtc =
                    nowUtc,

                SessionId =
                    target.SessionId,

                DeviceId =
                    target.DeviceId,

                TeacherId =
                    target.TeacherId,

                StudentId =
                    target.StudentId,

                StudentDisplayName =
                    target.StudentFullName,

                Details =
                    $"Source=TeamsUIAutomation;CallCycle={_callCycleId}"
            });
    }

    private static bool IsInsideEvidenceWindow(
        TeamsObservationTarget target,
        DateTimeOffset occurredAtUtc)
    {
        DateTimeOffset earliest =
            target.ScheduledStartUtc.AddMinutes(-5);

        DateTimeOffset latest =
            target.ScheduledEndUtc.AddMinutes(15);

        return
            occurredAtUtc >= earliest &&
            occurredAtUtc <= latest;
    }
}