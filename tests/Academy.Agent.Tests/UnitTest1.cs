using System.Text.Json;
using Academy.Agent.Teams;
using Academy.Agent.TeamsHelper;

namespace Academy.Agent.Tests;

public sealed class TeamsHelperLifecycleTests
{
    [Fact]
    public void InstanceLease_RejectsDuplicate_AndCanBeReacquired()
    {
        string name =
            $"Local\\AcademyAgent.TeamsHelper.Test.{Guid.NewGuid():N}";

        TeamsHelperInstanceLease? first =
            TeamsHelperInstanceLease.TryAcquire(name);

        Assert.NotNull(first);

        using (first)
        {
            using TeamsHelperInstanceLease? duplicate =
                TeamsHelperInstanceLease.TryAcquire(name);

            Assert.Null(duplicate);
        }

        using TeamsHelperInstanceLease? reacquired =
            TeamsHelperInstanceLease.TryAcquire(name);

        Assert.NotNull(reacquired);
    }

    [Fact]
    public void HealthReporter_WritesChangedState_AndThrottlesUnchangedState()
    {
        string root =
            CreateTemporaryRoot();

        try
        {
            DateTimeOffset now =
                new(
                    2026,
                    8,
                    27,
                    12,
                    0,
                    0,
                    TimeSpan.Zero);

            string path =
                Path.Combine(
                    root,
                    "health.json");

            var reporter =
                new TeamsHelperHealthReporter(
                    path,
                    TimeSpan.FromMinutes(1),
                    () => now,
                    processId: 123,
                    sessionId: 5);

            Assert.True(
                reporter.TryUpdate("Starting"));

            Assert.False(
                reporter.TryUpdate("Starting"));

            now =
                now.AddSeconds(1);

            Assert.True(
                reporter.TryUpdate("Monitoring"));

            TeamsHelperHealthSnapshot? snapshot =
                JsonSerializer.Deserialize<TeamsHelperHealthSnapshot>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions(
                        JsonSerializerDefaults.Web));

            Assert.NotNull(snapshot);
            Assert.Equal(123, snapshot.ProcessId);
            Assert.Equal(5, snapshot.SessionId);
            Assert.Equal("Monitoring", snapshot.State);
            Assert.Null(snapshot.LastError);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void FileLog_RotatesAtConfiguredLimit()
    {
        string root =
            CreateTemporaryRoot();

        try
        {
            string path =
                Path.Combine(
                    root,
                    "TeamsHelper.log");

            var log =
                new TeamsHelperFileLog(
                    path,
                    maximumBytes: 1);

            log.Information(
                "FIRST_TEST_ENTRY");

            log.Information(
                "SECOND_TEST_ENTRY");

            Assert.True(
                File.Exists(path));

            Assert.True(
                File.Exists(path + ".1"));

            Assert.Contains(
                "FIRST_TEST_ENTRY",
                File.ReadAllText(path + ".1"));

            Assert.Contains(
                "SECOND_TEST_ENTRY",
                File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void StateMachine_InitialConnectedSnapshot_EmitsCallLifecycleEvidence()
    {
        DateTimeOffset start =
            new(
                2026,
                9,
                6,
                1,
                0,
                0,
                TimeSpan.Zero);

        var target =
            new TeamsObservationTarget
            {
                SessionId =
                    Guid.NewGuid(),

                DeviceId =
                    Guid.NewGuid(),

                TeacherId =
                    Guid.NewGuid(),

                TeacherFullName =
                    "Teacher",

                StudentId =
                    Guid.NewGuid(),

                StudentFullName =
                    "Student",

                CourseId =
                    Guid.NewGuid(),

                CourseName =
                    "Qaida",

                ScheduledStartUtc =
                    start,

                ScheduledEndUtc =
                    start.AddMinutes(30)
            };

        var machine =
            new TeamsEvidenceStateMachine();

        var connected =
            new TeamsUiSnapshot(
                TeamsWebViewCount:
                    1,

                SelectedProcessId:
                    123,

                ChatBound:
                    true,

                CallState:
                    "Connected",

                CallingControlsVisible:
                    true,

                MicrophoneControlVisible:
                    true,

                Greetings:
                    Array.Empty<TeamsDetectedMessage>(),

                Lessons:
                    Array.Empty<TeamsDetectedMessage>());

        IReadOnlyList<TeamsEvidenceEnvelope> first =
            machine.Evaluate(
                target,
                connected,
                start.AddSeconds(5));

        Assert.Equal(
            2,
            first.Count);

        Assert.Contains(
            first,
            item =>
                item.Type ==
                    TeamsEvidenceType.CallAttempted);

        Assert.Contains(
            first,
            item =>
                item.Type ==
                    TeamsEvidenceType.StudentCallConnected);

        TeamsUiSnapshot ended =
            connected with
            {
                CallState =
                    "Idle",

                CallingControlsVisible =
                    false,

                MicrophoneControlVisible =
                    false
            };

        IReadOnlyList<TeamsEvidenceEnvelope> second =
            machine.Evaluate(
                target,
                ended,
                start.AddMinutes(2));

        TeamsEvidenceEnvelope endEvent =
            Assert.Single(
                second);

        Assert.Equal(
            TeamsEvidenceType.CallEnded,
            endEvent.Type);
    }

    [Fact]
    public void LessonSequence_ImageThenLessonText_ResolvesLessonEvidence()
    {
        DateTimeOffset start =
            new(
                2026,
                9,
                6,
                1,
                0,
                0,
                TimeSpan.Zero);

        IReadOnlyList<TeamsDetectedMessage> raw =
            new[]
            {
                new TeamsDetectedMessage(
                    MessageId:
                        "1788643380000",

                    OccurredAtUtc:
                        start,

                    AttachmentName:
                        "lesson-page.jpg",

                    MessageText:
                        "Sent image"),

                new TeamsDetectedMessage(
                    MessageId:
                        "1788643410000",

                    OccurredAtUtc:
                        start.AddSeconds(30),

                    AttachmentName:
                        null,

                    MessageText:
                        "Sent para 4 line 7")
            };

        IReadOnlyList<TeamsDetectedMessage> resolved =
            TeamsUiAutomationDetector
                .ResolveLessonMessageSequences(
                    raw);

        TeamsDetectedMessage lesson =
            Assert.Single(
                resolved);

        Assert.Equal(
            "lesson-page.jpg",
            lesson.AttachmentName);

        Assert.Contains(
            "para 4 line 7",
            lesson.MessageText
                .ToLowerInvariant());

        Assert.Equal(
            start.AddSeconds(30),
            lesson.OccurredAtUtc);
    }

    private static string CreateTemporaryRoot()
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "Academy.Agent.Tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        return root;
    }
}
