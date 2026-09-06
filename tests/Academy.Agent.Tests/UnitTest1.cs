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
    public void StateMachine_CallLifecycle_DoesNotRequireChatBinding()
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
                    "Hifz",

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
                    2,

                SelectedProcessId:
                    null,

                ChatBound:
                    false,

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

        Assert.Contains(
            second,
            item =>
                item.Type ==
                    TeamsEvidenceType.CallEnded);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LessonImageSignal_AcceptsEitherTeamsMediaSignal(
        bool attachmentContainerFound,
        bool imageFound)
    {
        Assert.True(
            TeamsUiAutomationDetector
                .IsLessonImageSignal(
                    attachmentContainerFound,
                    imageFound));
    }

    [Fact]
    public void LessonImageSignal_RejectsWhenNeitherSignalExists()
    {
        Assert.False(
            TeamsUiAutomationDetector
                .IsLessonImageSignal(
                    attachmentContainerFound:
                        false,

                    imageFound:
                        false));
    }

    [Fact]
    public void LessonEvidence_ImageAlone_IsAuthoritative()
    {
        DateTimeOffset now =
            new(
                2026,
                9,
                6,
                12,
                0,
                0,
                TimeSpan.Zero);

        IReadOnlyList<TeamsDetectedMessage> resolved =
            TeamsUiAutomationDetector
                .ResolveLessonMessageSequences(
                    new[]
                    {
                        new TeamsDetectedMessage(
                            MessageId:
                                "1788696000000",

                            OccurredAtUtc:
                                now,

                            AttachmentName:
                                "lesson-page.png",

                            MessageText:
                                "Sent image")
                    });

        TeamsDetectedMessage lesson =
            Assert.Single(
                resolved);

        Assert.Equal(
            "lesson-page.png",
            lesson.AttachmentName);

        Assert.Equal(
            now,
            lesson.OccurredAtUtc);
    }

    [Fact]
    public void LessonEvidence_TextAlone_IsValidFallback()
    {
        IReadOnlyList<TeamsDetectedMessage> resolved =
            TeamsUiAutomationDetector
                .ResolveLessonMessageSequences(
                    new[]
                    {
                        new TeamsDetectedMessage(
                            MessageId:
                                "1788696000001",

                            OccurredAtUtc:
                                DateTimeOffset.UtcNow,

                            AttachmentName:
                                null,

                            MessageText:
                                "Sent Hifz Surah Mulk ayah 1 to 5")
                    });

        TeamsDetectedMessage lesson =
            Assert.Single(
                resolved);

        Assert.Null(
            lesson.AttachmentName);
    }

    [Fact]
    public void LessonEvidence_ImageAndText_DoNotDependOnEachOther()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        IReadOnlyList<TeamsDetectedMessage> resolved =
            TeamsUiAutomationDetector
                .ResolveLessonMessageSequences(
                    new[]
                    {
                        new TeamsDetectedMessage(
                            MessageId:
                                "1788696000002",

                            OccurredAtUtc:
                                now,

                            AttachmentName:
                                "page.png",

                            MessageText:
                                "Sent image"),

                        new TeamsDetectedMessage(
                            MessageId:
                                "1788696000003",

                            OccurredAtUtc:
                                now.AddSeconds(20),

                            AttachmentName:
                                null,

                            MessageText:
                                "Sent Para 4 line 7")
                    });

        Assert.Equal(
            2,
            resolved.Count);
    }

    [Theory]
    [InlineData("Para 4 line 7")]
    [InlineData("Parah 4")]
    [InlineData("Sipara 12")]
    [InlineData("Juz 29")]
    [InlineData("Surah Mulk")]
    [InlineData("Ayah 1")]
    [InlineData("Verse 5")]
    [InlineData("Line 7")]
    [InlineData("Page 17")]
    [InlineData("Lesson 4")]
    [InlineData("Sabaq 3")]
    [InlineData("Sabak 3")]
    [InlineData("Qaida page 8")]
    [InlineData("Qaidah page 8")]
    [InlineData("Nazra page 20")]
    [InlineData("Ruku 2")]
    [InlineData("Rukoo 2")]
    [InlineData("Tajweed lesson")]
    [InlineData("Hifz new lesson")]
    [InlineData("Manzil revision")]
    [InlineData("Sabaqi revision")]
    [InlineData("Sabqi revision")]
    [InlineData("Makhraj practice")]
    [InlineData("Makharij revision")]
    public void LessonVocabulary_AcceptsAcademyLessonTerms(
        string value)
    {
        Assert.True(
            TeamsUiAutomationDetector
                .ContainsLessonKeyword(
                    value));
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("done")]
    [InlineData("good")]
    [InlineData("👍")]
    public void LessonVocabulary_RejectsGenericMessages(
        string value)
    {
        Assert.False(
            TeamsUiAutomationDetector
                .ContainsLessonKeyword(
                    value));
    }

    [Theory]
    [InlineData(
        "Chat | Student Test 2 | Microsoft Teams")]
    [InlineData(
        "Chat with Student Test 2 - Microsoft Teams")]
    [InlineData(
        "Student Test 2 | Microsoft Teams")]
    public void StudentChatBinding_AcceptsTeamsTitleVariants(
        string title)
    {
        Assert.True(
            TeamsUiAutomationDetector
                .IsStudentChatDocumentName(
                    title,
                    "Student Test 2"));
    }

    [Fact]
    public void StudentChatBinding_RejectsDifferentStudent()
    {
        Assert.False(
            TeamsUiAutomationDetector
                .IsStudentChatDocumentName(
                    "Chat | Student Test 3 | Microsoft Teams",
                    "Student Test 2"));
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
