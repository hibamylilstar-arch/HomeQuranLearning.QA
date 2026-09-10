namespace Academy.Agent.Service;

public sealed class QaLocalVoskRecognizer :
    IDisposable
{
    private readonly Vosk.VoskRecognizer
        _recognizer;

    private readonly HashSet<string>
        _allowedPhrases;

    private readonly byte[] _pcmBytes =
        new byte[
            QaTeacherPcm16kConverter
                .OutputSamplesPerFrame *
            sizeof(short)];

    public QaLocalVoskRecognizer(
        Vosk.Model model,
        IEnumerable<string> phrases)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(phrases);

        _allowedPhrases =
            QaLocalVoskDecisionGate
                .NormalizePhrases(phrases);

        if (_allowedPhrases.Count == 0)
        {
            throw new ArgumentException(
                "At least one supported QA phrase is required.",
                nameof(phrases));
        }

        string grammarJson =
            QaLocalVoskDecisionGate
                .BuildGrammarJson(
                    _allowedPhrases);

        _recognizer =
            new Vosk.VoskRecognizer(
                model,
                16_000.0f,
                grammarJson);

        _recognizer.SetWords(true);
    }

    public bool AcceptPcm16Frame(
        short[] pcm16,
        out string matchedText,
        out double confidence)
    {
        ArgumentNullException.ThrowIfNull(pcm16);

        if (pcm16.Length !=
            QaTeacherPcm16kConverter
                .OutputSamplesPerFrame)
        {
            throw new ArgumentException(
                "PCM16 frame must contain exactly 320 samples.",
                nameof(pcm16));
        }

        Buffer.BlockCopy(
            pcm16,
            0,
            _pcmBytes,
            0,
            _pcmBytes.Length);

        if (!_recognizer.AcceptWaveform(
                _pcmBytes,
                _pcmBytes.Length))
        {
            matchedText =
                string.Empty;

            confidence =
                0.0;

            return false;
        }

        return
            QaLocalVoskDecisionGate.TryAccept(
                _recognizer.Result(),
                _allowedPhrases,
                out matchedText,
                out confidence);
    }

    public void Reset()
    {
        _recognizer.Reset();
    }

    public void Dispose()
    {
        _recognizer.Dispose();
    }
}
