namespace Academy.Application.Options;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "HomeQuranLearning";
    public string Audience { get; init; } = "HomeQuranLearning.Dashboard";
    public string SigningKey { get; init; } = "development-only-signing-key-change-me";
    public int ExpiryMinutes { get; init; } = 120;
}