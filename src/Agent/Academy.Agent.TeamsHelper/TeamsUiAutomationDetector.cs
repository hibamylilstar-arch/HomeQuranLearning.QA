using System.Globalization;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace Academy.Agent.TeamsHelper;

internal sealed record TeamsDetectedMessage(
    string MessageId,
    DateTimeOffset? OccurredAtUtc,
    string? AttachmentName);

internal sealed record TeamsUiSnapshot(
    int TeamsWebViewCount,
    int? SelectedProcessId,
    bool ChatBound,
    string CallState,
    bool CallingControlsVisible,
    bool MicrophoneControlVisible,
    IReadOnlyList<TeamsDetectedMessage> Greetings,
    IReadOnlyList<TeamsDetectedMessage> Lessons);

internal static class TeamsUiAutomationDetector
{
    private static readonly Regex MessageIdRegex =
        new(
            @"^message-body-(\d+)$",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);

    private static readonly Regex LessonKeywordRegex =
        new(
            @"\b(?:" +
            @"para|parah|sipara|siparah|" +
            @"juz|" +
            @"surah|surahs|surat|" +
            @"verse|verses|" +
            @"ayah|ayahs|ayat|ayats|" +
            @"line|lines|" +
            @"page|pages|" +
            @"lesson|lessons|" +
            @"sabaq|" +
            @"qaida|" +
            @"nazra|" +
            @"ruku|" +
            @"tajweed" +
            @")\b",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);
public static TeamsUiSnapshot Scan(
        string studentName,
        string? teacherName)
    {
        if (string.IsNullOrWhiteSpace(
                studentName))
        {
            throw new ArgumentException(
                "Student name is required.",
                nameof(studentName));
        }

        studentName =
            studentName.Trim();

        teacherName =
            string.IsNullOrWhiteSpace(
                teacherName)
                ? null
                : teacherName.Trim();

        IReadOnlyList<int> webViewPids =
            FindTeamsWebViewProcessIds();

        var selectedCandidates =
            new List<ProcessElements>();

        foreach (int processId in webViewPids)
        {
            IReadOnlyList<AutomationElement> elements =
                ReadProcessElements(
                    processId);

            if (elements.Count == 0)
            {
                continue;
            }

            bool exactActiveChat =
                elements.Any(
                    element =>
                    {
                        string name =
                            GetName(
                                element);

                        ControlType? controlType =
                            GetControlType(
                                element);

                        return
                            controlType ==
                                ControlType.Document &&
                            string.Equals(
                                name,
                                $"Chat | {studentName} | Microsoft Teams",
                                StringComparison.OrdinalIgnoreCase);
                    });

            if (!exactActiveChat)
            {
                continue;
            }

            selectedCandidates.Add(
                new ProcessElements(
                    processId,
                    elements));
        }

        ProcessElements? selected =
            selectedCandidates
                .OrderByDescending(
                    x => x.Elements.Count)
                .FirstOrDefault();

        if (selected is null)
        {
            return new TeamsUiSnapshot(
                TeamsWebViewCount:
                    webViewPids.Count,

                SelectedProcessId:
                    null,

                ChatBound:
                    false,

                CallState:
                    "Unknown",

                CallingControlsVisible:
                    false,

                MicrophoneControlVisible:
                    false,

                Greetings:
                    Array.Empty<TeamsDetectedMessage>(),

                Lessons:
                    Array.Empty<TeamsDetectedMessage>());
        }

        IReadOnlyList<AutomationElement> selectedElements =
            selected.Elements;

        bool callingControls =
            selectedElements.Any(
                element =>
                {
                    string name =
                        GetName(
                            element);

                    return
                        string.Equals(
                            name,
                            "Calling controls",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            name,
                            "Calling indicators",
                            StringComparison.OrdinalIgnoreCase);
                });

        bool microphoneControl =
            selectedElements.Any(
                element =>
                    string.Equals(
                        GetAutomationId(
                            element),
                        "microphone-button",
                        StringComparison.OrdinalIgnoreCase));

        bool studentInCall =
            selectedElements.Any(
                element =>
                    string.Equals(
                        GetName(
                            element),
                        $"{studentName} In a call",
                        StringComparison.OrdinalIgnoreCase));

        bool studentAvailable =
            selectedElements.Any(
                element =>
                    string.Equals(
                        GetName(
                            element),
                        $"{studentName} Available",
                        StringComparison.OrdinalIgnoreCase));

        string callState;

        if (studentInCall &&
            callingControls)
        {
            callState =
                "Connected";
        }
        else if (callingControls)
        {
            callState =
                "Attempting";
        }
        else if (studentAvailable)
        {
            callState =
                "Available";
        }
        else
        {
            callState =
                "Idle";
        }

        List<TeamsDetectedMessage> greetings =
            DetectMessages(
                selectedElements,
                MessageKind.Greeting);

        List<TeamsDetectedMessage> lessons =
            DetectMessages(
                selectedElements,
                MessageKind.Lesson);

        return new TeamsUiSnapshot(
            TeamsWebViewCount:
                webViewPids.Count,

            SelectedProcessId:
                selected.ProcessId,

            ChatBound:
                true,

            CallState:
                callState,

            CallingControlsVisible:
                callingControls,

            MicrophoneControlVisible:
                microphoneControl,

            Greetings:
                greetings,

            Lessons:
                lessons);
    }

    private static List<TeamsDetectedMessage> DetectMessages(
        IReadOnlyList<AutomationElement> elements,
        MessageKind kind)
    {
        var result =
            new Dictionary<string, TeamsDetectedMessage>(
                StringComparer.Ordinal);

        foreach (AutomationElement element in elements)
        {
            string automationId =
                GetAutomationId(
                    element);

            Match idMatch =
                MessageIdRegex.Match(
                    automationId);

            if (!idMatch.Success)
            {
                continue;
            }

            string name =
                GetName(
                    element);

            if (string.IsNullOrWhiteSpace(
                    name))
            {
                continue;
            }

            if (!IsOutgoingMessageContainer(
                    element,
                    name))
            {
                continue;
            }

            bool matches =
                kind switch
                {
                    MessageKind.Greeting =>
                        IsGreetingText(
                            name),

                    MessageKind.Lesson =>
                        ContainsLessonKeyword(
                            name),

                    _ =>
                        false
                };

            if (!matches)
            {
                continue;
            }

            string messageId =
                idMatch.Groups[1].Value;

            string? attachmentName =
                kind == MessageKind.Lesson
                    ? FindAttachmentName(
                        element,
                        messageId)
                    : null;

            if (
                kind == MessageKind.Lesson &&
                attachmentName is null
            )
            {
                // SOP completion evidence requires the lesson
                // to include an attachment/image.
                continue;
            }

            result[messageId] =
                new TeamsDetectedMessage(
                    MessageId:
                        messageId,

                    OccurredAtUtc:
                        TryParseMessageTimestamp(
                            messageId),

                    AttachmentName:
                        attachmentName);
        }

        return result.Values
            .OrderBy(
                x =>
                    x.OccurredAtUtc ??
                    DateTimeOffset.MinValue)
            .ToList();
    }

    private static bool IsOutgoingMessageContainer(
        AutomationElement element,
        string name)
    {
        string className =
            GetClassName(
                element);

        if (className.Contains(
                "ChatMyMessage",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return
            name.StartsWith(
                "Sent ",
                StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith(
                "Seen ",
                StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith(
                "Sending ",
                StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith(
                "Delivered ",
                StringComparison.OrdinalIgnoreCase);
    }


    internal static bool IsGreetingText(
        string? text)
    {
        if (string.IsNullOrWhiteSpace(
                text))
        {
            return false;
        }

        // Teacher wording can vary. Spaces/punctuation/spelling around
        // Alaikum are irrelevant; the Salam/Salaam core is mandatory.
        string normalized =
            Regex.Replace(
                text.ToLowerInvariant(),
                @"[^a-z]+",
                string.Empty);

        return
            normalized.Contains(
                "salam",
                StringComparison.Ordinal) ||
            normalized.Contains(
                "salaam",
                StringComparison.Ordinal);
    }


    internal static bool ContainsLessonKeyword(
        string? text)
    {
        if (string.IsNullOrWhiteSpace(
                text))
        {
            return false;
        }

        return LessonKeywordRegex.IsMatch(
            text);
    }

    private static string? FindAttachmentName(
        AutomationElement messageElement,
        string messageId)
    {
        AutomationElementCollection descendants;

        try
        {
            descendants =
                messageElement.FindAll(
                    TreeScope.Descendants,
                    Condition.TrueCondition);
        }
        catch
        {
            return null;
        }

        bool attachmentContainerFound =
            false;

        bool imageFound =
            false;

        string? detectedImageName =
            null;

        string expectedAttachmentId =
            $"attachments-{messageId}";

        for (
            int i = 0;
            i < descendants.Count;
            i++)
        {
            AutomationElement element =
                descendants[i];

            string automationId =
                GetAutomationId(
                    element);

            if (string.Equals(
                    automationId,
                    expectedAttachmentId,
                    StringComparison.OrdinalIgnoreCase))
            {
                attachmentContainerFound =
                    true;
            }

            if (GetControlType(
                    element) ==
                ControlType.Image)
            {
                imageFound =
                    true;

                string name =
                    GetName(
                        element);

                if (
                    detectedImageName is null &&
                    !string.IsNullOrWhiteSpace(
                        name)
                )
                {
                    detectedImageName =
                        name.Trim();
                }
            }
        }

        // Business rule:
        // same outgoing Teams message must contain an attachment
        // container plus an actual image UI element.
        //
        // Filename and extension are not attendance semantics.
        if (
            !attachmentContainerFound ||
            !imageFound
        )
        {
            return null;
        }

        return
            detectedImageName ??
            "image";
    }

    private static IReadOnlyList<int> FindTeamsWebViewProcessIds()
    {
        var result =
            new HashSet<int>();

        using var searcher =
            new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine " +
                "FROM Win32_Process " +
                "WHERE Name='msedgewebview2.exe'");

        using ManagementObjectCollection processes =
            searcher.Get();

        foreach (ManagementObject process in processes)
        {
            using (process)
            {
                string commandLine =
                    Convert.ToString(
                        process["CommandLine"],
                        CultureInfo.InvariantCulture)
                    ??
                    string.Empty;

                bool isTeams =
                    commandLine.Contains(
                        "ms-teams",
                        StringComparison.OrdinalIgnoreCase) ||
                    commandLine.Contains(
                        "MSTeams_8wekyb3d8bbwe",
                        StringComparison.OrdinalIgnoreCase) ||
                    commandLine.Contains(
                        @"\MSTeams\",
                        StringComparison.OrdinalIgnoreCase) ||
                    (
                        commandLine.Contains(
                            "Teams",
                            StringComparison.OrdinalIgnoreCase) &&
                        commandLine.Contains(
                            "EBWebView",
                            StringComparison.OrdinalIgnoreCase)
                    );

                if (!isTeams)
                {
                    continue;
                }

                object? processIdValue =
                    process["ProcessId"];

                if (processIdValue is null)
                {
                    continue;
                }

                int processId =
                    checked(
                        Convert.ToInt32(
                            processIdValue,
                            CultureInfo.InvariantCulture));

                result.Add(
                    processId);
            }
        }

        return result
            .OrderBy(x => x)
            .ToArray();
    }

    private static IReadOnlyList<AutomationElement> ReadProcessElements(
        int processId)
    {
        try
        {
            var condition =
                new PropertyCondition(
                    AutomationElement.ProcessIdProperty,
                    processId);

            AutomationElementCollection collection =
                AutomationElement.RootElement.FindAll(
                    TreeScope.Descendants,
                    condition);

            var result =
                new List<AutomationElement>(
                    collection.Count);

            for (int i = 0;
                 i < collection.Count;
                 i++)
            {
                result.Add(
                    collection[i]);
            }

            return result;
        }
        catch
        {
            return Array.Empty<AutomationElement>();
        }
    }

    private static string GetName(
        AutomationElement element)
    {
        try
        {
            return
                element.Current.Name ??
                string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetAutomationId(
        AutomationElement element)
    {
        try
        {
            return
                element.Current.AutomationId ??
                string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetClassName(
        AutomationElement element)
    {
        try
        {
            return
                element.Current.ClassName ??
                string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }


    private static ControlType? GetControlType(
        AutomationElement element)
    {
        try
        {
            return
                element.Current.ControlType;
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset? TryParseMessageTimestamp(
        string messageId)
    {
        if (!long.TryParse(
                messageId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long value))
        {
            return null;
        }

        // Teams personal/free message IDs observed in the
        // live UI use Unix-millisecond-shaped IDs.
        // Treat parsing only as metadata assistance, not identity.
        if (value < 946684800000L ||
            value > 4102444800000L)
        {
            return null;
        }

        try
        {
            return
                DateTimeOffset.FromUnixTimeMilliseconds(
                    value);
        }
        catch
        {
            return null;
        }
    }

    private sealed record ProcessElements(
        int ProcessId,
        IReadOnlyList<AutomationElement> Elements);

    private enum MessageKind
    {
        Greeting = 0,
        Lesson = 1
    }
}