$ErrorActionPreference = "Stop"
$RepoRoot = (Get-Location).Path

# 1. LiveKitOptions.cs
$liveKitOptions = @'
namespace Academy.Application.Options;

public sealed class LiveKitOptions
{
    public string Host { get; init; } = "http://localhost:7880";
    public string ApiKey { get; init; } = "devkey";
    public string ApiSecret { get; init; } = "devsecret";
}
'@

New-Item -ItemType Directory -Force -Path "$RepoRoot\src\Backend\Academy.Application\Options" | Out-Null
Set-Content -Path "$RepoRoot\src\Backend\Academy.Application\Options\LiveKitOptions.cs" -Value $liveKitOptions -Encoding UTF8

# 2. LiveKitTokenRequest.cs
$liveKitTokenRequest = @'
namespace Academy.Application.Contracts;

public sealed class LiveKitTokenRequest
{
    public string RoomName { get; init; } = string.Empty;
    public string Identity { get; init; } = string.Empty;
    public bool CanPublish { get; init; } = true;
    public bool CanSubscribe { get; init; } = true;
}
'@

Set-Content -Path "$RepoRoot\src\Backend\Academy.Application\Contracts\LiveKitTokenRequest.cs" -Value $liveKitTokenRequest -Encoding UTF8

# 3. LiveKitTokenService.cs
$liveKitTokenService = @'
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

    public string GenerateToken(
        string roomName,
        string identity,
        bool canPublish,
        bool canSubscribe)
    {
        var now = DateTime.UtcNow;

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

        payload["video"] = new
        {
            room = roomName,
            roomJoin = true,
            canPublish = canPublish,
            canSubscribe = canSubscribe
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            new JwtHeader(credentials),
            payload);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
'@

Set-Content -Path "$RepoRoot\src\Backend\Academy.Application\Services\LiveKitTokenService.cs" -Value $liveKitTokenService -Encoding UTF8

# 4. Update appsettings.Development.json
$appDev = @'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=homequranlearning_qa;Username=academy;Password=AcademyLocalDev2026"
  },
  "AgentApiKey": "local-dev-agent-key",
  "AdminApiKey": "local-dev-admin-key",
  "WorkerApiKey": "local-dev-worker-key",
  "Storage": {
    "Endpoint": "localhost:9000",
    "AccessKey": "academy_minio",
    "SecretKey": "AcademyMinio2026",
    "Bucket": "academy-recordings"
  },
  "Jwt": {
    "Issuer": "HomeQuranLearning",
    "Audience": "HomeQuranLearning.Dashboard",
    "SigningKey": "development-only-signing-key-change-me",
    "ExpiryMinutes": 120
  },
  "SeedOwner": {
    "FullName": "Owner",
    "Email": "owner@academy.local",
    "Password": "OwnerPass123!"
  },
  "LiveKit": {
    "Host": "http://localhost:7880",
    "ApiKey": "devkey",
    "ApiSecret": "devsecret"
  }
}
'@

Set-Content -Path "$RepoRoot\src\Backend\Academy.Api\appsettings.Development.json" -Value $appDev -Encoding UTF8

# 5. Update appsettings.json
$appProd = @'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=homequranlearning_qa;Username=academy;Password=CHANGE_ME"
  },
  "AgentApiKey": "CHANGE_ME_AGENT_API_KEY",
  "WorkerApiKey": "CHANGE_ME_WORKER_API_KEY",
  "Storage": {
    "Endpoint": "localhost:9000",
    "AccessKey": "CHANGE_ME_MINIO_ACCESS_KEY",
    "SecretKey": "CHANGE_ME_MINIO_SECRET_KEY",
    "Bucket": "academy-recordings"
  },
  "Jwt": {
    "Issuer": "HomeQuranLearning",
    "Audience": "HomeQuranLearning.Dashboard",
    "SigningKey": "CHANGE_ME_JWT_SIGNING_KEY",
    "ExpiryMinutes": 120
  },
  "SeedOwner": {
    "FullName": "Owner",
    "Email": "owner@academy.local",
    "Password": "CHANGE_ME_SEED_OWNER_PASSWORD"
  },
  "LiveKit": {
    "Host": "http://localhost:7880",
    "ApiKey": "CHANGE_ME_LIVEKIT_API_KEY",
    "ApiSecret": "CHANGE_ME_LIVEKIT_API_SECRET"
  }
}
'@

Set-Content -Path "$RepoRoot\src\Backend\Academy.Api\appsettings.json" -Value $appProd -Encoding UTF8

Write-Host "LiveKit backend token files created."
Write-Host "Now run the build command manually."