using System.Text.Json;

namespace Academy.Agent.Service;

public readonly record struct QaLocalVoskVerification(
    bool Accepted,
    string Transcript,
    double Confidence);

public static class QaLocalVoskVerifier
{
    public const double MinimumConfidence = 0.90;

    // Candidate detection already has up to ten seconds
    // of pre-trigger audio. Verify only the most recent
    // eight seconds against the unrestricted English graph.
    public const int VerificationWindowFrames = 400;

    public static QaLocalVoskVerification Verify(
        Vosk.Model model,
        IEnumerable<short[]> frames,
        string targetPhrase)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(frames);

        string normalizedTarget =
            QaLocalVoskDecisionGate.NormalizePhrase(
                targetPhrase);

        if (string.IsNullOrWhiteSpace(
                normalizedTarget))
        {
            return new QaLocalVoskVerification(
                false,
                string.Empty,
                0.0);
        }

        using var recognizer =
            new Vosk.VoskRecognizer(
                model,
                16_000.0f);

        recognizer.SetWords(true);

        var jsonResults =
            new List<string>();

        byte[] pcmBytes =
            new byte[
                QaTeacherPcm16kConverter
                    .OutputSamplesPerFrame *
                sizeof(short)];

        foreach (short[] frame in frames)
        {
            if (frame.Length !=
                QaTeacherPcm16kConverter
                    .OutputSamplesPerFrame)
            {
                throw new ArgumentException(
                    "Verifier PCM frame must contain exactly 320 samples.",
                    nameof(frames));
            }

            Buffer.BlockCopy(
                frame,
                0,
                pcmBytes,
                0,
                pcmBytes.Length);

            if (recognizer.AcceptWaveform(
                    pcmBytes,
                    pcmBytes.Length))
            {
                jsonResults.Add(
                    recognizer.Result());
            }
        }

        jsonResults.Add(
            recognizer.FinalResult());

        return EvaluateResults(
            jsonResults,
            normalizedTarget);
    }

    public static QaLocalVoskVerification EvaluateResults(
        IEnumerable<string> resultJson,
        string targetPhrase)
    {
        ArgumentNullException.ThrowIfNull(
            resultJson);

        string normalizedTarget =
            QaLocalVoskDecisionGate.NormalizePhrase(
                targetPhrase);

        if (string.IsNullOrWhiteSpace(
                normalizedTarget))
        {
            return new QaLocalVoskVerification(
                false,
                string.Empty,
                0.0);
        }

        string[] targetWords =
            normalizedTarget.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        var transcriptParts =
            new List<string>();

        var recognizedWords =
            new List<RecognizedWord>();

        foreach (string json in resultJson)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(json);

                JsonElement root =
                    document.RootElement;

                if (root.TryGetProperty(
                        "text",
                        out JsonElement textElement) &&
                    textElement.ValueKind ==
                        JsonValueKind.String)
                {
                    string text =
                        QaLocalVoskDecisionGate
                            .NormalizePhrase(
                                textElement.GetString());

                    if (!string.IsNullOrWhiteSpace(
                            text))
                    {
                        transcriptParts.Add(text);
                    }
                }

                if (!root.TryGetProperty(
                        "result",
                        out JsonElement resultElement) ||
                    resultElement.ValueKind !=
                        JsonValueKind.Array)
                {
                    continue;
                }

                foreach (
                    JsonElement item
                    in resultElement.EnumerateArray())
                {
                    if (!item.TryGetProperty(
                            "word",
                            out JsonElement wordElement) ||
                        wordElement.ValueKind !=
                            JsonValueKind.String ||
                        !item.TryGetProperty(
                            "conf",
                            out JsonElement confidenceElement) ||
                        confidenceElement.ValueKind !=
                            JsonValueKind.Number ||
                        !confidenceElement.TryGetDouble(
                            out double confidence) ||
                        !double.IsFinite(confidence))
                    {
                        continue;
                    }

                    string word =
                        QaLocalVoskDecisionGate
                            .NormalizePhrase(
                                wordElement.GetString());

                    if (string.IsNullOrWhiteSpace(
                            word))
                    {
                        continue;
                    }

                    recognizedWords.Add(
                        new RecognizedWord(
                            word,
                            confidence));
                }
            }
            catch (JsonException)
            {
                // Invalid recognizer output is not
                // sufficient to confirm a QA alert.
            }
        }

        string transcript =
            QaLocalVoskDecisionGate.NormalizePhrase(
                string.Join(
                    " ",
                    transcriptParts));

        double bestConfidence = 0.0;

        for (
            int start = 0;
            start <=
                recognizedWords.Count -
                targetWords.Length;
            start++)
        {
            bool matches = true;

            double minimumConfidence =
                1.0;

            for (
                int offset = 0;
                offset < targetWords.Length;
                offset++)
            {
                RecognizedWord recognized =
                    recognizedWords[
                        start + offset];

                if (!string.Equals(
                        recognized.Word,
                        targetWords[offset],
                        StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }

                minimumConfidence =
                    Math.Min(
                        minimumConfidence,
                        recognized.Confidence);
            }

            if (matches)
            {
                bestConfidence =
                    Math.Max(
                        bestConfidence,
                        minimumConfidence);
            }
        }

        return new QaLocalVoskVerification(
            bestConfidence >=
                MinimumConfidence,
            transcript,
            bestConfidence);
    }

    private readonly record struct RecognizedWord(
        string Word,
        double Confidence);
}