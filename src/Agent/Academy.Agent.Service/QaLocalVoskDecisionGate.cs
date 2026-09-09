using System.Text.Json;

namespace Academy.Agent.Service;

public static class QaLocalVoskDecisionGate
{
    public const double MinimumConfidence = 0.80;

    public const string GrammarJson =
        "[\"whatsapp\",\"what's up\",\"what app\",\"what is up\",\"[unk]\"]";

    private static readonly HashSet<string> AcceptedTexts =
        new(
            new[]
            {
                "whatsapp",
                "what's up",
                "what app",
                "what is up",
                    "what up"
            },
            StringComparer.Ordinal);

    public static bool TryAccept(
        string? resultJson,
        out string matchedFamilyText,
        out double minimumWordConfidence)
    {
        matchedFamilyText = string.Empty;
        minimumWordConfidence = 0.0;

        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(resultJson);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("text", out JsonElement textElement) ||
                textElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            string text = Normalize(textElement.GetString());

            bool textContainsWhatsapp =
                text.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Contains(
                        "whatsapp",
                        StringComparer.Ordinal);

            if (!textContainsWhatsapp)
            {
                return false;
            }

            if (!root.TryGetProperty("result", out JsonElement resultElement) ||
                resultElement.ValueKind != JsonValueKind.Array ||
                resultElement.GetArrayLength() == 0 ||
                resultElement.GetArrayLength() > 3)
            {
                return false;
            }

            var words = new List<string>();
            double minConfidence = 1.0;

            foreach (JsonElement item in resultElement.EnumerateArray())
            {
                if (!item.TryGetProperty("word", out JsonElement wordElement) ||
                    wordElement.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                string word = Normalize(wordElement.GetString());

                if (word == "[unk]")
                {
                    continue;
                }

                if (!item.TryGetProperty("conf", out JsonElement confidenceElement) ||
                    confidenceElement.ValueKind != JsonValueKind.Number ||
                    !confidenceElement.TryGetDouble(out double confidence) ||
                    !double.IsFinite(confidence) ||
                    confidence < MinimumConfidence)
                {
                    return false;
                }

                words.Add(word);
                minConfidence = Math.Min(minConfidence, confidence);
            }

            string reconstructed = string.Join(" ", words);

            if (!string.Equals(reconstructed, text, StringComparison.Ordinal) &&
                !(text == "what's up" && reconstructed == "what is up") &&
                !(text == "what is up" && reconstructed == "what's up") &&
                !(textContainsWhatsapp &&
                  words.Contains(
                      "whatsapp",
                      StringComparer.Ordinal)))
            {
                return false;
            }

            matchedFamilyText = text;
            minimumWordConfidence = minConfidence;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            value.Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
