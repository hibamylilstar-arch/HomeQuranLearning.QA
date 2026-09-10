using System.Text.Json;
using Academy.Agent.Service;

namespace Academy.Agent.Tests;

public sealed class QaLocalVoskDecisionGateTests
{
    [Theory]
    [InlineData("whatsapp")]
    [InlineData("mother")]
    [InlineData("father")]
    [InlineData("contact")]
    public void Accepts_ExactConfiguredPhrase(
        string phrase)
    {
        var allowed =
            QaLocalVoskDecisionGate
                .NormalizePhrases(
                    new[] { phrase });

        string json =
            CreateResult(
                phrase,
                0.90);

        bool accepted =
            QaLocalVoskDecisionGate.TryAccept(
                json,
                allowed,
                out string matched,
                out double confidence);

        Assert.True(accepted);
        Assert.Equal(phrase, matched);
        Assert.True(confidence >= 0.80);
    }

    [Fact]
    public void Accepts_ExactConfiguredMultiWordPhrase()
    {
        var allowed =
            QaLocalVoskDecisionGate
                .NormalizePhrases(
                    new[] { "personal number" });

        string json =
            CreateResult(
                "personal number",
                0.91);

        Assert.True(
            QaLocalVoskDecisionGate.TryAccept(
                json,
                allowed,
                out string matched,
                out _));

        Assert.Equal(
            "personal number",
            matched);
    }

    [Fact]
    public void Rejects_AliasWhenOnlyWhatsappConfigured()
    {
        var allowed =
            QaLocalVoskDecisionGate
                .NormalizePhrases(
                    new[] { "whatsapp" });

        string json =
            CreateResult(
                "what's up",
                0.99);

        Assert.False(
            QaLocalVoskDecisionGate.TryAccept(
                json,
                allowed,
                out _,
                out _));
    }

    [Fact]
    public void Rejects_UnconfiguredPhrase()
    {
        var allowed =
            QaLocalVoskDecisionGate
                .NormalizePhrases(
                    new[] { "whatsapp", "mother" });

        string json =
            CreateResult(
                "sister",
                0.99);

        Assert.False(
            QaLocalVoskDecisionGate.TryAccept(
                json,
                allowed,
                out _,
                out _));
    }

    [Fact]
    public void Rejects_BelowConfidenceFloor()
    {
        var allowed =
            QaLocalVoskDecisionGate
                .NormalizePhrases(
                    new[] { "whatsapp" });

        string json =
            CreateResult(
                "whatsapp",
                0.7999);

        Assert.False(
            QaLocalVoskDecisionGate.TryAccept(
                json,
                allowed,
                out _,
                out _));
    }

    [Fact]
    public void Rejects_UnknownMixedWithTarget()
    {
        var allowed =
            QaLocalVoskDecisionGate
                .NormalizePhrases(
                    new[] { "whatsapp" });

        string json =
            """
            {
              "result": [
                { "conf": 0.99, "word": "[unk]" },
                { "conf": 0.99, "word": "whatsapp" }
              ],
              "text": "[unk] whatsapp"
            }
            """;

        Assert.False(
            QaLocalVoskDecisionGate.TryAccept(
                json,
                allowed,
                out _,
                out _));
    }

    [Fact]
    public void Grammar_IsExactAndContainsOnlyConfiguredPhrasesPlusUnknown()
    {
        string grammar =
            QaLocalVoskDecisionGate
                .BuildGrammarJson(
                    new[]
                    {
                        "WhatsApp",
                        "mother",
                        "whatsapp"
                    });

        Assert.Equal(
            "[\"mother\",\"whatsapp\",\"[unk]\"]",
            grammar);
    }

    private static string CreateResult(
        string text,
        double confidence)
    {
        return JsonSerializer.Serialize(
            new
            {
                result =
                    text.Split(
                            ' ',
                            StringSplitOptions.RemoveEmptyEntries)
                        .Select(
                            word =>
                                new
                                {
                                    conf = confidence,
                                    word
                                })
                        .ToArray(),
                text
            });
    }
}
