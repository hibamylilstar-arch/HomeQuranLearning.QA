using System.Text.Json;
using Academy.Agent.Service;

namespace Academy.Agent.Tests;

public sealed class QaLocalVoskVerifierTests
{
    [Fact]
    public void Accepts_ExactWholeWord_InUnrestrictedTranscript()
    {
        string json =
            CreateResult(
                new[]
                {
                    ("please", 0.96),
                    ("contact", 0.95),
                    ("me", 0.97)
                });

        QaLocalVoskVerification result =
            QaLocalVoskVerifier.EvaluateResults(
                new[] { json },
                "contact");

        Assert.True(result.Accepted);
        Assert.Equal(
            "please contact me",
            result.Transcript);
        Assert.Equal(
            0.95,
            result.Confidence,
            3);
    }

    [Fact]
    public void Rejects_WhenTargetIsAbsent()
    {
        string json =
            CreateResult(
                new[]
                {
                    ("recitation", 0.98),
                    ("continues", 0.96)
                });

        QaLocalVoskVerification result =
            QaLocalVoskVerifier.EvaluateResults(
                new[] { json },
                "sister");

        Assert.False(result.Accepted);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void Rejects_BelowVerifierConfidence()
    {
        string json =
            CreateResult(
                new[]
                {
                    ("phone", 0.89)
                });

        QaLocalVoskVerification result =
            QaLocalVoskVerifier.EvaluateResults(
                new[] { json },
                "phone");

        Assert.False(result.Accepted);
        Assert.Equal(
            0.89,
            result.Confidence,
            3);
    }

    [Fact]
    public void Rejects_SubstringThatIsNotWholeWord()
    {
        string json =
            CreateResult(
                new[]
                {
                    ("headphone", 0.99)
                });

        QaLocalVoskVerification result =
            QaLocalVoskVerifier.EvaluateResults(
                new[] { json },
                "phone");

        Assert.False(result.Accepted);
    }

    [Fact]
    public void Accepts_ContiguousMultiWordPhrase()
    {
        string json =
            CreateResult(
                new[]
                {
                    ("my", 0.98),
                    ("personal", 0.94),
                    ("number", 0.93),
                    ("is", 0.96)
                });

        QaLocalVoskVerification result =
            QaLocalVoskVerifier.EvaluateResults(
                new[] { json },
                "personal number");

        Assert.True(result.Accepted);
        Assert.Equal(
            0.93,
            result.Confidence,
            3);
    }

    private static string CreateResult(
        IEnumerable<(string Word, double Confidence)> words)
    {
        var items =
            words
                .Select(
                    x => new
                    {
                        word = x.Word,
                        conf = x.Confidence
                    })
                .ToArray();

        return JsonSerializer.Serialize(
            new
            {
                result = items,
                text =
                    string.Join(
                        " ",
                        items.Select(
                            x => x.word))
            });
    }
}