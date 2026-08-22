using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Academy.Application.Options;
using Microsoft.IdentityModel.Tokens;

namespace Academy.Application.Services;

public sealed class LiveKitTokenService
{
    private readonly LiveKitOptions _options;

    public LiveKitTokenService(LiveKitOptions options)
    {
        _options = options;
    }

    public string Host => _options.Host;

    public string GenerateToken(
        string roomName,
        string identity,
        bool canPublish,
        bool canSubscribe)
    {
        var now = DateTime.UtcNow;

        var videoGrant = new Dictionary<string, object>
        {
            ["room"] = roomName,
            ["roomJoin"] = true,
            ["canPublish"] = canPublish,
            ["canSubscribe"] = canSubscribe
        };

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, identity)
        };

        var payload = new JwtPayload(
            issuer: _options.ApiKey,
            audience: null,
            claims: claims,
            notBefore: now,
            expires: now.AddHours(1));

        payload["video"] = videoGrant;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            new JwtHeader(credentials),
            payload);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateServerApiToken()
    {
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "api-admin")
        };

        var grants = new Dictionary<string, object>
        {
            ["admin"] = true,
            ["ingressAdmin"] = true,
            ["ingress"] = true,
            ["roomAdmin"] = true,
            ["roomCreate"] = true,
            ["roomList"] = true,
            ["roomRecord"] = true
        };

        var payload = new JwtPayload(
            issuer: _options.ApiKey,
            audience: null,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(5));

        payload["grants"] = grants;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            new JwtHeader(credentials),
            payload);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}