using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Options;
using Academy.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Academy.Application.Services;

public sealed class AuthService
{
    private const string RefreshTokenUseClaim = "token_use";
    private const string RefreshTokenUseValue = "refresh";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        JwtOptions jwtOptions)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtOptions = jwtOptions;
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(
            request.Email,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Invalid email or password.");

        if (!user.IsActive)
        {
            throw new InvalidOperationException("User is inactive.");
        }

        if (!_passwordHasher.Verify(
                request.Password,
                user.PasswordHash))
        {
            throw new InvalidOperationException(
                "Invalid email or password.");
        }

        return CreateLoginResponse(user);
    }

    public async Task<LoginResponse> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new SecurityTokenException(
                "Refresh token is required.");
        }

        ClaimsPrincipal principal =
            ValidateRefreshToken(refreshToken);

        string? userIdValue =
            principal
                .FindFirst(ClaimTypes.NameIdentifier)
                ?.Value;

        if (
            userIdValue is null ||
            !Guid.TryParse(
                userIdValue,
                out Guid userId)
        )
        {
            throw new SecurityTokenException(
                "Refresh token user is invalid.");
        }

        var user =
            await _userRepository.GetByIdAsync(
                userId,
                cancellationToken)
            ?? throw new SecurityTokenException(
                "Refresh token user was not found.");

        if (!user.IsActive)
        {
            throw new SecurityTokenException(
                "User is inactive.");
        }

        return CreateLoginResponse(user);
    }

    public async Task<UserDto> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(
            userId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "User not found.");

        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString(),
            IsActive = user.IsActive
        };
    }

    private LoginResponse CreateLoginResponse(
        User user)
    {
        return new LoginResponse
        {
            Token = GenerateAccessToken(user),
            RefreshToken = GenerateRefreshToken(user),
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString()
        };
    }

    private string GenerateAccessToken(
        User user)
    {
        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),
            new(
                ClaimTypes.Email,
                user.Email),
            new(
                ClaimTypes.Role,
                user.Role.ToString()),
            new(
                "full_name",
                user.FullName),
            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString("N"))
        };

        return GenerateToken(
            claims,
            _jwtOptions.Audience,
            DateTime.UtcNow.AddMinutes(
                _jwtOptions.ExpiryMinutes));
    }

    private string GenerateRefreshToken(
        User user)
    {
        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),
            new(
                RefreshTokenUseClaim,
                RefreshTokenUseValue),
            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString("N"))
        };

        return GenerateToken(
            claims,
            _jwtOptions.RefreshAudience,
            DateTime.UtcNow.AddDays(
                _jwtOptions.RefreshExpiryDays));
    }

    private string GenerateToken(
        IReadOnlyCollection<Claim> claims,
        string audience,
        DateTime expiresUtc)
    {
        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _jwtOptions.SigningKey));

        var creds =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: audience,
                claims: claims,
                expires: expiresUtc,
                signingCredentials: creds);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    private ClaimsPrincipal ValidateRefreshToken(
        string refreshToken)
    {
        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _jwtOptions.SigningKey));

        var validation =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience =
                    _jwtOptions.RefreshAudience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

        ClaimsPrincipal principal =
            new JwtSecurityTokenHandler()
                .ValidateToken(
                    refreshToken,
                    validation,
                    out SecurityToken validatedToken);

        if (
            validatedToken is not JwtSecurityToken jwt ||
            !string.Equals(
                jwt.Header.Alg,
                SecurityAlgorithms.HmacSha256,
                StringComparison.Ordinal)
        )
        {
            throw new SecurityTokenException(
                "Refresh token algorithm is invalid.");
        }

        if (
            !string.Equals(
                principal
                    .FindFirst(
                        RefreshTokenUseClaim)
                    ?.Value,
                RefreshTokenUseValue,
                StringComparison.Ordinal)
        )
        {
            throw new SecurityTokenException(
                "Token is not a refresh token.");
        }

        return principal;
    }
}