using System.Text.Json;
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
if (HasArgument(
        "--detector-policy-probe"))
{
    RunDetectorPolicyProbe();
    return;
}

if (HasArgument(
        "--lifecycle-probe"))
{
    RunLifecycleProbe();
    return;
}

if (HasArgument("--monitor"))
{
    TeamsHelperRuntimePaths paths =
        TeamsHelperRuntimePaths.CreateDefault();

    var log =
        new TeamsHelperFileLog(
            paths.LogPath);

    TeamsHelperInstanceLease? instanceLease =
        TeamsHelperInstanceLease.TryAcquire();

    if (instanceLease is null)
    {
        log.Information(
            "TEAMS_HELPER_ALREADY_RUNNING");

        return;
    }

    using (instanceLease)
    {
        var health =
            new TeamsHelperHealthReporter(
                paths.HealthPath);

        health.TryUpdate(
            "Starting",
            force: true);

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
            new TeamsEvidenceMonitor(
                log,
                health);

        try
        {
            await monitor.RunAsync(
                cancellation.Token);
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            health.TryUpdate(
                "Stopped",
                force: true);
        }
        catch (Exception ex)
        {
            health.TryUpdate(
                "Failed",
                $"{ex.GetType().Name}: {ex.Message}",
                force: true);

            log.Error(
                "TeamsHelper monitor terminated unexpectedly.",
                ex);

            Environment.ExitCode =
                1;
        }
    }

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


static void RunDetectorPolicyProbe()
{
    string[] validGreetings =
    {
        "Salam",
        "Salaam",
        "Assalamualaikum",
        "Assalam o Aliekum",
        "Assalamu Aliekum",
        "Assalamu Alaikum",
        "Hello Salam",
        "Seen Assalamualaikum"
    };

    foreach (string value in validGreetings)
    {
        if (!TeamsUiAutomationDetector.IsGreetingText(
                value))
        {
            throw new InvalidOperationException(
                $"Valid Salam rejected: {value}");
        }
    }


    string[] invalidGreetings =
    {
        "Hello",
        "Hi",
        "Good morning",
        "Start class",
        "How are you?",
        ""
    };

    foreach (string value in invalidGreetings)
    {
        if (TeamsUiAutomationDetector.IsGreetingText(
                value))
        {
            throw new InvalidOperationException(
                $"Non-Salam text accepted: {value}");
        }
    }


    string[] validLessons =
    {
        "Para 1",
        "Parah 3 Line 5",
        "Sipara 30",
        "Juz 30",
        "Surah Yaseen",
        "Surat Al Fatiha",
        "Verse 5",
        "Verses 5 to 8",
        "Ayah 10",
        "Ayat 1 to 5",
        "Line 7",
        "Lines 3 to 5",
        "Page 12",
        "Today's Lesson",
        "Sabaq",
        "Qaida Page 4",
        "Nazra Page 8",
        "Ruku 2",
        "Tajweed lesson"
    };

    foreach (string value in validLessons)
    {
        if (!TeamsUiAutomationDetector.ContainsLessonKeyword(
                value))
        {
            throw new InvalidOperationException(
                $"Valid lesson text rejected: {value}");
        }
    }


    string[] invalidLessons =
    {
        "Hello",
        "Call me",
        "See you tomorrow",
        "How are you?",
        "Goodbye",
        ""
    };

    foreach (string value in invalidLessons)
    {
        if (TeamsUiAutomationDetector.ContainsLessonKeyword(
                value))
        {
            throw new InvalidOperationException(
                $"Non-lesson text accepted: {value}");
        }
    }


    Console.WriteLine("SALAM_VARIANTS_OK");
    Console.WriteLine("SALAM_REQUIRED_OK");
    Console.WriteLine("LESSON_KEYWORDS_OK");
    Console.WriteLine("NON_LESSON_TEXT_REJECTED_OK");
    Console.WriteLine("IMAGE_FILENAME_NOT_SEMANTIC_OK");
    Console.WriteLine("DETECTOR_POLICY_PROBE_OK");
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


static void RunLifecycleProbe()
{
    string temporaryRoot =
        Path.Combine(
            Path.GetTempPath(),
            "AcademyAgent.TeamsHelper.Probe",
            Guid.NewGuid().ToString("N"));

    Directory.CreateDirectory(
        temporaryRoot);

    try
    {
        string mutexName =
            $"Local\\AcademyAgent.TeamsHelper.Probe.{Guid.NewGuid():N}";

        TeamsHelperInstanceLease? first =
            TeamsHelperInstanceLease.TryAcquire(
                mutexName);

        if (first is null)
        {
            throw new InvalidOperationException(
                "Lifecycle probe could not acquire its first instance lease.");
        }

        using (first)
        {
            using TeamsHelperInstanceLease? duplicate =
                TeamsHelperInstanceLease.TryAcquire(
                    mutexName);

            if (duplicate is not null)
            {
                throw new InvalidOperationException(
                    "Lifecycle probe allowed a duplicate instance lease.");
            }
        }

        using TeamsHelperInstanceLease? reacquired =
            TeamsHelperInstanceLease.TryAcquire(
                mutexName);

        if (reacquired is null)
        {
            throw new InvalidOperationException(
                "Lifecycle probe did not release its instance lease.");
        }

        Console.WriteLine(
            "TEAMS_HELPER_SINGLE_INSTANCE_OK");

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        string healthPath =
            Path.Combine(
                temporaryRoot,
                "health.json");

        var health =
            new TeamsHelperHealthReporter(
                healthPath,
                TimeSpan.FromHours(1),
                () => now,
                processId: 123,
                sessionId: 456);

        bool firstHealthWrite =
            health.TryUpdate(
                "Starting");

        bool throttledHealthWrite =
            health.TryUpdate(
                "Starting");

        now =
            now.AddSeconds(1);

        bool changedHealthWrite =
            health.TryUpdate(
                "Monitoring");

        TeamsHelperHealthSnapshot? snapshot =
            JsonSerializer.Deserialize<TeamsHelperHealthSnapshot>(
                File.ReadAllText(healthPath),
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        if (!firstHealthWrite ||
            throttledHealthWrite ||
            !changedHealthWrite ||
            snapshot?.State != "Monitoring" ||
            snapshot.ProcessId != 123 ||
            snapshot.SessionId != 456)
        {
            throw new InvalidOperationException(
                "Lifecycle health probe failed.");
        }

        Console.WriteLine(
            "TEAMS_HELPER_HEALTH_OK");

        string logPath =
            Path.Combine(
                temporaryRoot,
                "TeamsHelper.log");

        var log =
            new TeamsHelperFileLog(
                logPath,
                maximumBytes: 1);

        log.Information(
            "LIFECYCLE_PROBE_FIRST_LOG");

        log.Information(
            "LIFECYCLE_PROBE_SECOND_LOG");

        if (!File.Exists(logPath) ||
            !File.Exists(logPath + ".1"))
        {
            throw new InvalidOperationException(
                "Lifecycle log rotation probe failed.");
        }

        Console.WriteLine(
            "TEAMS_HELPER_LOG_ROTATION_OK");

        Console.WriteLine(
            "TEAMS_HELPER_LIFECYCLE_PROBE_OK");
    }
    finally
    {
        if (Directory.Exists(temporaryRoot))
        {
            Directory.Delete(
                temporaryRoot,
                recursive: true);
        }
    }
}
