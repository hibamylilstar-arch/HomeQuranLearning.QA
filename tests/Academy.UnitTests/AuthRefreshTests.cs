using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Options;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class AuthRefreshTests
{
    [Fact]
    public async Task Refresh_ValidRefreshToken_RotatesSessionTokens()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Owner",
            Email = "owner@academy.local",
            PasswordHash = "hash",
            Role = UserRole.Owner,
            IsActive = true
        };

        var userRepo =
            new Mock<IUserRepository>();

        userRepo
            .Setup(
                x => x.GetByEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        userRepo
            .Setup(
                x => x.GetByIdAsync(
                    user.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var passwordHasher =
            new Mock<IPasswordHasher>();

        passwordHasher
            .Setup(
                x => x.Verify(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .Returns(true);

        var service =
            new AuthService(
                userRepo.Object,
                passwordHasher.Object,
                new JwtOptions
                {
                    Issuer = "Test",
                    Audience = "Test.Access",
                    RefreshAudience =
                        "Test.Refresh",
                    SigningKey =
                        "this-is-a-test-signing-key-for-unit-tests",
                    ExpiryMinutes = 120,
                    RefreshExpiryDays =
                        400
                });

        var login =
            await service.LoginAsync(
                new LoginRequest
                {
                    Email =
                        user.Email,
                    Password =
                        "pass"
                });

        var refreshed =
            await service.RefreshAsync(
                login.RefreshToken);

        Assert.False(
            string.IsNullOrWhiteSpace(
                login.Token));

        Assert.False(
            string.IsNullOrWhiteSpace(
                login.RefreshToken));

        Assert.False(
            string.IsNullOrWhiteSpace(
                refreshed.Token));

        Assert.False(
            string.IsNullOrWhiteSpace(
                refreshed.RefreshToken));

        Assert.NotEqual(
            login.RefreshToken,
            refreshed.RefreshToken);

        Assert.Equal(
            "Owner",
            refreshed.Role);
    }
}