using System.Text.Json;
using Academy.Agent.Service;

namespace Academy.Agent.Tests;

public sealed class QaLocalVoskDecisionGateTests
{
    [Theory]
    [InlineData("whatsapp", "whatsapp")]
    [InlineData("[unk] whatsapp", "[unk] whatsapp")]
    [InlineData("whatsapp up", "whatsapp up")]
    public void Accepts_ConfidentWhatsappToken(string text, string words)
    {
        string json = CreateResult(text, words, 0.90);

        bool accepted =
            QaLocalVoskDecisionGate.TryAccept(
                json,
                out string matched,
                out double confidence);

        Assert.True(accepted);
        Assert.Equal(text, matched);
        Assert.True(confidence >= 0.80);
    }

    [Fact]
    public void Rejects_BelowConfidenceFloor()
    {
        string json = CreateResult("whatsapp", "whatsapp", 0.7999);

        Assert.False(
            QaLocalVoskDecisionGate.TryAccept(
                json,
                out _,
                out _));
    }

    [Theory]
    [InlineData("[unk]")]
    [InlineData("alhamdulillah")]
    [InlineData("quran")]
    [InlineData("please read")]
    [InlineData("what's up")]
    [InlineData("what app")]
    [InlineData("what is up")]
    [InlineData("what up")]
    public void Rejects_UnrelatedOrUnknownText(string text)
    {
        string json =
            JsonSerializer.Serialize(
                new
                {
                    result = new[]
                    {
                        new
                        {
                            conf = 0.99,
                            word = text
                        }
                    },
                    text
                });

        Assert.False(
            QaLocalVoskDecisionGate.TryAccept(
                json,
                out _,
                out _));
    }

    [Fact]
    public void Accepts_TargetMixedWithUnknownContext()
    {
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

        Assert.True(
            QaLocalVoskDecisionGate.TryAccept(
                json,
                out string matched,
                out double confidence));

        Assert.Equal("[unk] whatsapp", matched);
        Assert.True(confidence >= 0.99);
    }

    [Fact]
    public void Rejects_TargetMixedWithUnknown_WhenTargetBelowConfidenceFloor()
    {
        string json =
            """
            {
              "result": [
                { "conf": 0.99, "word": "[unk]" },
                { "conf": 0.7999, "word": "whatsapp" }
              ],
              "text": "[unk] whatsapp"
            }
            """;

        Assert.False(
            QaLocalVoskDecisionGate.TryAccept(
                json,
                out _,
                out _));
    }

    [Fact]
    public void Grammar_IsExactlyLocked()
    {
        Assert.Equal(
            "[\"whatsapp\",\"what's up\",\"what app\",\"what is up\",\"[unk]\"]",
            QaLocalVoskDecisionGate.GrammarJson);
    }

    private static string CreateResult(
        string text,
        string words,
        double confidence)
    {
        return JsonSerializer.Serialize(
            new
            {
                result =
                    words.Split(
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
