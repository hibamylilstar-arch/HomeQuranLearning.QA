using System.Windows.Automation;
using Academy.Agent.Teams;
using Academy.Agent.TeamsHelper;

if (HasArgument("--uia-probe"))
{
    RunUiaProbe();
    return;
}

if (HasArgument("--state-machine-probe"))
{
    RunStateMachineProbe();
    return;
}

if (HasArgument("--monitor"))
{
    using var cancellation =
        new CancellationTokenSource();

    Console.CancelKeyPress +=
        (_, eventArgs) =>
        {
            eventArgs.Cancel =
                true;

            cancellation.Cancel();
        };

    var monitor =
        new TeamsEvidenceMonitor();

    await monitor.RunAsync(
        cancellation.Token);

    return;
}

string? manualStudent =
    GetArgumentValue(
        "--scan-student");

if (!string.IsNullOrWhiteSpace(
        manualStudent))
{
    string? manualTeacher =
        GetArgumentValue(
            "--teacher");

    TeamsUiSnapshot snapshot =
        TeamsUiAutomationDetector.Scan(
            manualStudent,
            manualTeacher);

    PrintSnapshot(
        manualStudent,
        manualTeacher,
        snapshot);

    Environment.ExitCode =
        snapshot.ChatBound
            ? 0
            : 3;

    return;
}

var client =
    new TeamsEvidencePipeClient();

TeamsObservationTarget? target =
    await client.GetTargetAsync(
        CancellationToken.None);

if (target is null)
{
    Console.WriteLine(
        "TARGET=NONE");

    return;
}

Console.WriteLine(
    $"TARGET_SESSION={target.SessionId:D}");

Console.WriteLine(
    $"TARGET_DEVICE={target.DeviceId:D}");

Console.WriteLine(
    $"TARGET_TEACHER={target.TeacherFullName}");

Console.WriteLine(
    $"TARGET_STUDENT={target.StudentFullName}");

TeamsUiSnapshot targetSnapshot =
    TeamsUiAutomationDetector.Scan(
        target.StudentFullName,
        target.TeacherFullName);

PrintSnapshot(
    target.StudentFullName,
    target.TeacherFullName,
    targetSnapshot);


bool HasArgument(
    string name)
{
    return args.Any(
        x =>
            string.Equals(
                x,
                name,
                StringComparison.OrdinalIgnoreCase));
}


string? GetArgumentValue(
    string name)
{
    for (int i = 0;
         i < args.Length - 1;
         i++)
    {
        if (string.Equals(
                args[i],
                name,
                StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}


static void PrintSnapshot(
    string studentName,
    string? teacherName,
    TeamsUiSnapshot snapshot)
{
    Console.WriteLine(
        $"SCAN_STUDENT={studentName}");

    Console.WriteLine(
        $"SCAN_TEACHER={teacherName ?? "(not required)"}");

    Console.WriteLine(
        $"TEAMS_WEBVIEW_COUNT={snapshot.TeamsWebViewCount}");

    Console.WriteLine(
        $"SELECTED_RENDERER_PID={snapshot.SelectedProcessId?.ToString() ?? "NONE"}");

    Console.WriteLine(
        $"CHAT_BOUND={snapshot.ChatBound}");

    Console.WriteLine(
        $"CALL_STATE={snapshot.CallState}");

    Console.WriteLine(
        $"CALLING_CONTROLS={snapshot.CallingControlsVisible}");

    Console.WriteLine(
        $"MICROPHONE_CONTROL={snapshot.MicrophoneControlVisible}");

    Console.WriteLine(
        $"GREETING_COUNT={snapshot.Greetings.Count}");

    foreach (TeamsDetectedMessage greeting in snapshot.Greetings)
    {
        Console.WriteLine(
            "GREETING=" +
            greeting.MessageId +
            "|" +
            (
                greeting.OccurredAtUtc?.ToString("O") ??
                "timestamp-unknown"
            ));
    }

    Console.WriteLine(
        $"LESSON_COUNT={snapshot.Lessons.Count}");

    foreach (TeamsDetectedMessage lesson in snapshot.Lessons)
    {
        Console.WriteLine(
            "LESSON=" +
            lesson.MessageId +
            "|" +
            (
                lesson.OccurredAtUtc?.ToString("O") ??
                "timestamp-unknown"
            ) +
            "|" +
            (
                lesson.AttachmentName ??
                "attachment-unknown"
            ));
    }

    Console.WriteLine(
        "SCAN_COMPLETE");
}


static void RunUiaProbe()
{
    AutomationElement root =
        AutomationElement.RootElement;

    AutomationElementCollection children =
        root.FindAll(
            TreeScope.Children,
            Condition.TrueCondition);

    Console.WriteLine(
        $"DesktopChildren={children.Count}");

    Console.WriteLine(
        "UIA_HELPER_OK");
}


static void RunStateMachineProbe()
{
    DateTimeOffset now =
        DateTimeOffset.UtcNow;

    var target =
        new TeamsObservationTarget
        {
            SessionId = Guid.NewGuid(),
            ScheduleId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            TeacherFullName = "Probe Teacher",
            StudentId = Guid.NewGuid(),
            StudentFullName = "Probe Student",
            CourseId = Guid.NewGuid(),
            CourseName = "Probe Course",
            ScheduledStartUtc = now.AddMinutes(-2),
            ScheduledEndUtc = now.AddMinutes(30)
        };

    var greeting =
        new TeamsDetectedMessage(
            "1900000000001",
            now.AddMinutes(-1),
            null);

    var lesson =
        new TeamsDetectedMessage(
            "1900000000002",
            now.AddSeconds(-30),
            "probe.jpg");

    var machine =
        new TeamsEvidenceStateMachine();

    var all =
        new List<TeamsEvidenceEnvelope>();

    all.AddRange(
        machine.Evaluate(
            target,
            new TeamsUiSnapshot(
                1, 100, true,
                "Available",
                false, false,
                new[] { greeting },
                new[] { lesson }),
            now));

    all.AddRange(
        machine.Evaluate(
            target,
            new TeamsUiSnapshot(
                1, 100, true,
                "Attempting",
                true, true,
                new[] { greeting },
                new[] { lesson }),
            now.AddSeconds(1)));

    all.AddRange(
        machine.Evaluate(
            target,
            new TeamsUiSnapshot(
                1, 100, true,
                "Connected",
                true, true,
                new[] { greeting },
                new[] { lesson }),
            now.AddSeconds(2)));

    all.AddRange(
        machine.Evaluate(
            target,
            new TeamsUiSnapshot(
                1, 100, true,
                "Connected",
                true, true,
                new[] { greeting },
                new[] { lesson }),
            now.AddSeconds(3)));

    all.AddRange(
        machine.Evaluate(
            target,
            new TeamsUiSnapshot(
                1, 100, true,
                "Available",
                false, false,
                new[] { greeting },
                new[] { lesson }),
            now.AddSeconds(4)));

    all.AddRange(
        machine.Evaluate(
            target,
            new TeamsUiSnapshot(
                1, 100, true,
                "Available",
                false, false,
                new[] { greeting },
                new[] { lesson }),
            now.AddSeconds(5)));

    foreach (TeamsEvidenceEnvelope item in all)
    {
        Console.WriteLine(
            $"STATE_MACHINE_EVENT={item.Type}|{item.IdempotencyKey}");
    }

    int uniqueKeys =
        all
            .Select(
                x => x.IdempotencyKey)
            .Distinct(
                StringComparer.Ordinal)
            .Count();

    bool countsValid =
        all.Count(
            x => x.Type ==
                 TeamsEvidenceType.TeacherGreetingSent) == 1 &&
        all.Count(
            x => x.Type ==
                 TeamsEvidenceType.LessonShared) == 1 &&
        all.Count(
            x => x.Type ==
                 TeamsEvidenceType.CallAttempted) == 1 &&
        all.Count(
            x => x.Type ==
                 TeamsEvidenceType.StudentCallConnected) == 1 &&
        all.Count(
            x => x.Type ==
                 TeamsEvidenceType.CallEnded) == 1;

    if (
        all.Count != 5 ||
        uniqueKeys != 5 ||
        !countsValid
    )
    {
        throw new InvalidOperationException(
            $"State machine failure. Events={all.Count}, Unique={uniqueKeys}");
    }

    Console.WriteLine(
        "MESSAGE_DEDUPE_OK");

    Console.WriteLine(
        "CALL_TRANSITIONS_OK");

    Console.WriteLine(
        "STATE_MACHINE_PROBE_OK");
}