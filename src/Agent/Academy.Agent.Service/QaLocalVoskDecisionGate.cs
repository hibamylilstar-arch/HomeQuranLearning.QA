using System.Text.Json;

namespace Academy.Agent.Service;

public static class QaLocalVoskDecisionGate
{
    public const double MinimumConfidence = 0.80;

    public const int MaximumActivePhrases = 64;

    public const int MaximumPhraseLength = 128;

    public const int MaximumPhraseWords = 6;

    public static string BuildGrammarJson(
        IEnumerable<string> phrases)
    {
        HashSet<string> normalized =
            NormalizePhrases(phrases);

        if (normalized.Count == 0)
        {
            throw new ArgumentException(
                "At least one supported QA phrase is required.",
                nameof(phrases));
        }

        string[] grammar =
            normalized
                .OrderBy(
                    x => x,
                    StringComparer.Ordinal)
                .Append("[unk]")
                .ToArray();

        return JsonSerializer.Serialize(grammar);
    }

    public static HashSet<string> NormalizePhrases(
        IEnumerable<string> phrases)
    {
        ArgumentNullException.ThrowIfNull(phrases);

        var normalized =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (string? phrase in phrases)
        {
            if (!IsSupportedPhrase(phrase))
            {
                continue;
            }

            normalized.Add(
                NormalizePhrase(phrase));

            if (normalized.Count >
                MaximumActivePhrases)
            {
                throw new ArgumentException(
                    $"At most {MaximumActivePhrases} active QA phrases are supported.",
                    nameof(phrases));
            }
        }

        return normalized;
    }

    public static bool IsSupportedPhrase(
        string? value)
    {
        string normalized =
            NormalizePhrase(value);

        if (string.IsNullOrWhiteSpace(normalized) ||
            string.Equals(
                normalized,
                "[unk]",
                StringComparison.Ordinal) ||
            normalized.Length >
                MaximumPhraseLength)
        {
            return false;
        }

        int wordCount =
            normalized.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .Length;

        return
            wordCount > 0 &&
            wordCount <= MaximumPhraseWords;
    }

    public static bool TryAccept(
        string? resultJson,
        IReadOnlySet<string> allowedPhrases,
        out string matchedText,
        out double minimumWordConfidence)
    {
        matchedText = string.Empty;
        minimumWordConfidence = 0.0;

        if (string.IsNullOrWhiteSpace(resultJson) ||
            allowedPhrases.Count == 0)
        {
            return false;
        }

        try
        {
            using JsonDocument document =
                JsonDocument.Parse(resultJson);

            JsonElement root =
                document.RootElement;

            if (!root.TryGetProperty(
                    "text",
                    out JsonElement textElement) ||
                textElement.ValueKind !=
                    JsonValueKind.String)
            {
                return false;
            }

            string text =
                NormalizePhrase(
                    textElement.GetString());

            if (string.IsNullOrEmpty(text) ||
                !allowedPhrases.Contains(text))
            {
                return false;
            }

            if (!root.TryGetProperty(
                    "result",
                    out JsonElement resultElement) ||
                resultElement.ValueKind !=
                    JsonValueKind.Array ||
                resultElement.GetArrayLength() == 0 ||
                resultElement.GetArrayLength() >
                    MaximumPhraseWords)
            {
                return false;
            }

            var words =
                new List<string>();

            double minConfidence =
                1.0;

            foreach (
                JsonElement item
                in resultElement.EnumerateArray())
            {
                if (!item.TryGetProperty(
                        "word",
                        out JsonElement wordElement) ||
                    wordElement.ValueKind !=
                        JsonValueKind.String)
                {
                    return false;
                }

                string word =
                    NormalizePhrase(
                        wordElement.GetString());

                if (string.IsNullOrWhiteSpace(word) ||
                    string.Equals(
                        word,
                        "[unk]",
                        StringComparison.Ordinal))
                {
                    return false;
                }

                if (!item.TryGetProperty(
                        "conf",
                        out JsonElement confidenceElement) ||
                    confidenceElement.ValueKind !=
                        JsonValueKind.Number ||
                    !confidenceElement.TryGetDouble(
                        out double confidence) ||
                    !double.IsFinite(confidence) ||
                    confidence <
                        MinimumConfidence)
                {
                    return false;
                }

                words.Add(word);

                minConfidence =
                    Math.Min(
                        minConfidence,
                        confidence);
            }

            string reconstructed =
                string.Join(
                    " ",
                    words);

            if (!string.Equals(
                    reconstructed,
                    text,
                    StringComparison.Ordinal))
            {
                return false;
            }

            matchedText =
                text;

            minimumWordConfidence =
                minConfidence;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string NormalizePhrase(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            value
                .Trim()
                .ToLowerInvariant()
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries));
    }
}
