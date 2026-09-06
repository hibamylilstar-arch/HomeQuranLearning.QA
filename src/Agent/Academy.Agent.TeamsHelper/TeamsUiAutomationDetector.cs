using System.Globalization;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace Academy.Agent.TeamsHelper;

internal sealed record TeamsDetectedMessage(
    string MessageId,
    DateTimeOffset? OccurredAtUtc,
    string? AttachmentName,
    string MessageText = "");

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
            @"sabaq|sabak|" +
            @"qaida|qaidah|" +
            @"nazra|" +
            @"ruku|rukoo|" +
            @"tajweed|" +
            @"hifz|" +
            @"manzil|" +
            @"sabaqi|sabqi|" +
            @"revision|" +
            @"makhraj|makharij" +
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

        var allElements =
            new List<AutomationElement>();

        foreach (int processId in webViewPids)
        {
            IReadOnlyList<AutomationElement> elements =
                ReadProcessElements(
                    processId);

            if (elements.Count == 0)
            {
                continue;
            }

            allElements.AddRange(
                elements);

            bool activeStudentChat =
                elements.Any(
                    element =>
                        GetControlType(
                            element) ==
                            ControlType.Document &&
                        IsTeamsChatDocumentName(
                            GetName(
                                element)));

            if (!activeStudentChat)
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
                    item =>
                        item.Elements.Count)
                .FirstOrDefault();

        IReadOnlyList<AutomationElement> selectedElements =
            selected?.Elements ??
            Array.Empty<AutomationElement>();

        IReadOnlyList<AutomationElement> callElements =
            allElements;

        bool callingControls =
            callElements.Any(
                element =>
                {
                    string name =
                        GetName(
                            element);

                    return
                        name.Contains(
                            "Calling controls",
                            StringComparison.OrdinalIgnoreCase) ||
                        name.Contains(
                            "Calling indicators",
                            StringComparison.OrdinalIgnoreCase) ||
                        name.Contains(
                            "Call controls",
                            StringComparison.OrdinalIgnoreCase);
                });

        bool microphoneControl =
            callElements.Any(
                element =>
                    string.Equals(
                        GetAutomationId(
                            element),
                        "microphone-button",
                        StringComparison.OrdinalIgnoreCase));

        string callState;

        if (
            callingControls &&
            microphoneControl
        )
        {
            callState =
                "Connected";
        }
        else if (callingControls)
        {
            callState =
                "Attempting";
        }
        else if (selected is not null)
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
            selected is null
                ? new List<TeamsDetectedMessage>()
                : DetectMessages(
                    selectedElements,
                    MessageKind.Greeting);

        List<TeamsDetectedMessage> lessons =
            selected is null
                ? new List<TeamsDetectedMessage>()
                : DetectMessages(
                    selectedElements,
                    MessageKind.Lesson);

        return new TeamsUiSnapshot(
            TeamsWebViewCount:
                webViewPids.Count,

            SelectedProcessId:
                selected?.ProcessId,

            ChatBound:
                selected is not null,

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

    public static IReadOnlyList<TeamsDetectedMessage>
        ScanLessonMessagesForStudent(
            string studentName)
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

        var result =
            new Dictionary<string, TeamsDetectedMessage>(
                StringComparer.Ordinal);

        foreach (
            int processId in
            FindTeamsWebViewProcessIds())
        {
            IReadOnlyList<AutomationElement> elements =
                ReadProcessElements(
                    processId);

            if (elements.Count == 0)
            {
                continue;
            }

            bool activeStudentChat =
                elements.Any(
                    element =>
                        GetControlType(
                            element) ==
                            ControlType.Document &&
                        IsTeamsChatDocumentName(
                            GetName(
                                element)));

            if (!activeStudentChat)
            {
                continue;
            }

            foreach (
                TeamsDetectedMessage message in
                DetectMessages(
                    elements,
                    MessageKind.Lesson))
            {
                result[message.MessageId] =
                    message;
            }
        }

        return result.Values
            .OrderBy(
                message =>
                    message.OccurredAtUtc ??
                    DateTimeOffset.MinValue)
            .ToList();
    }

    private static List<TeamsDetectedMessage> DetectMessages(
        IReadOnlyList<AutomationElement> elements,
        MessageKind kind)
    {
        if (kind == MessageKind.Lesson)
        {
            return DetectLessonMessages(
                elements);
        }

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

            if (!IsGreetingText(
                    name))
            {
                continue;
            }

            string messageId =
                idMatch.Groups[1].Value;

            result[messageId] =
                new TeamsDetectedMessage(
                    MessageId:
                        messageId,

                    OccurredAtUtc:
                        TryParseMessageTimestamp(
                            messageId),

                    AttachmentName:
                        null,

                    MessageText:
                        name);
        }

        return result.Values
            .OrderBy(
                x =>
                    x.OccurredAtUtc ??
                    DateTimeOffset.MinValue)
            .ToList();
    }

    private static List<TeamsDetectedMessage>
        DetectLessonMessages(
            IReadOnlyList<AutomationElement> elements)
    {
        var raw =
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

            if (!IsOutgoingMessageContainer(
                    element,
                    name))
            {
                continue;
            }

            string messageId =
                idMatch.Groups[1].Value;

            string? attachmentName =
                FindAttachmentName(
                    element,
                    messageId,
                    elements);

            bool hasLessonText =
                ContainsLessonKeyword(
                    name);

            if (
                attachmentName is null &&
                !hasLessonText
            )
            {
                continue;
            }

            raw[messageId] =
                new TeamsDetectedMessage(
                    MessageId:
                        messageId,

                    OccurredAtUtc:
                        TryParseMessageTimestamp(
                            messageId),

                    AttachmentName:
                        attachmentName,

                    MessageText:
                        name);
        }

        return ResolveLessonMessageSequences(
                raw.Values)
            .ToList();
    }

    internal static IReadOnlyList<TeamsDetectedMessage>
        ResolveLessonMessageSequences(
            IEnumerable<TeamsDetectedMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(
            messages);

        // Final product rule:
        //
        // IMAGE/PAGE alone = LessonShared.
        //
        // Recognized lesson text alone = LessonShared fallback.
        //
        // Pairing is never required.
        return messages
            .Where(
                message =>
                    !string.IsNullOrWhiteSpace(
                        message.MessageId) &&
                    (
                        message.AttachmentName is not null ||
                        ContainsLessonKeyword(
                            message.MessageText)
                    ))
            .OrderBy(
                message =>
                    message.OccurredAtUtc ??
                    DateTimeOffset.MinValue)
            .ToList();
    }

    internal static bool IsTeamsChatDocumentName(
        string? documentName)
    {
        if (string.IsNullOrWhiteSpace(
                documentName))
        {
            return false;
        }

        // Temporary operational rule:
        // the active scheduled laptop/session owns the evidence.
        // Academy Student.FullName is NOT required to match
        // the visible Microsoft Teams chat/account name.
        return documentName.Contains(
            "Microsoft Teams",
            StringComparison.OrdinalIgnoreCase);
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

    internal static bool ContainsStudentName(
        string? text,
        string? studentName)
    {
        if (
            string.IsNullOrWhiteSpace(text) ||
            string.IsNullOrWhiteSpace(studentName)
        )
        {
            return false;
        }

        static string Normalize(
            string value)
        {
            var chars =
                value
                    .ToLowerInvariant()
                    .Select(
                        ch =>
                            char.IsLetterOrDigit(ch)
                                ? ch
                                : ' ')
                    .ToArray();

            return string.Join(
                " ",
                new string(chars)
                    .Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries));
        }

        string normalizedText =
            Normalize(text);

        string normalizedName =
            Normalize(studentName);

        if (normalizedName.Length == 0)
        {
            return false;
        }

        return
            $" {normalizedText} ".Contains(
                $" {normalizedName} ",
                StringComparison.Ordinal);
    }

    internal static bool IsLessonImageSignal(
        bool attachmentContainerFound,
        bool imageFound)
    {
        // Teams may expose a shared Quran/Qaida page as either:
        //
        // - the message attachment container, or
        // - an Image UI Automation element.
        //
        // Either signal is sufficient. Requiring both caused valid
        // lesson pages to be missed in real Teams.
        return
            attachmentContainerFound ||
            imageFound;
    }

    private static string? FindAttachmentName(
        AutomationElement messageElement,
        string messageId,
        IReadOnlyList<AutomationElement> chatElements)
    {
        string expectedAttachmentId =
            $"attachments-{messageId}";

        // The Teams attachment container is commonly a sibling of
        // message-body-<id>, not its descendant. The id itself is
        // message-specific, so searching the bound student chat is safe.
        bool attachmentContainerFound =
            chatElements.Any(
                element =>
                    string.Equals(
                        GetAutomationId(
                            element),
                        expectedAttachmentId,
                        StringComparison.OrdinalIgnoreCase));

        bool imageFound =
            false;

        string? detectedImageName =
            null;

        void ScanScope(
            AutomationElement scope)
        {
            AutomationElementCollection descendants;

            try
            {
                descendants =
                    scope.FindAll(
                        TreeScope.Descendants,
                        Condition.TrueCondition);
            }
            catch
            {
                return;
            }

            for (
                int i = 0;
                i < descendants.Count;
                i++)
            {
                AutomationElement element =
                    descendants[i];

                if (
                    GetControlType(
                        element) !=
                    ControlType.Image
                )
                {
                    continue;
                }

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

        // First preserve the old descendant path.
        ScanScope(
            messageElement);

        // Real Teams often places the media thumbnail beside the
        // message-body node inside the surrounding outgoing message card.
        // Walk upward only a few levels and stop at the first outgoing
        // ChatMyMessage-style container.
        if (!imageFound)
        {
            try
            {
                AutomationElement? current =
                    TreeWalker.RawViewWalker.GetParent(
                        messageElement);

                for (
                    int depth = 0;
                    current is not null &&
                    depth < 8;
                    depth++)
                {
                    string className =
                        GetClassName(
                            current);

                    if (
                        className.Contains(
                            "ChatMyMessage",
                            StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        ScanScope(
                            current);

                        break;
                    }

                    if (
                        GetControlType(
                            current) ==
                        ControlType.Document
                    )
                    {
                        break;
                    }

                    current =
                        TreeWalker.RawViewWalker.GetParent(
                            current);
                }
            }
            catch
            {
                // Attachment-id search above remains authoritative.
            }
        }

        if (!IsLessonImageSignal(
                attachmentContainerFound,
                imageFound))
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