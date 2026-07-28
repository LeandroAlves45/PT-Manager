using Domain.Entities.Identity;
using Xunit;

namespace Unit.Domain.UnitTests.Entities.Identity;

public sealed class RefreshTokenTests
{
    [Fact]
    public void IsActive_BeforeExpiry_ReturnsTrue()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var refreshToken = new RefreshToken(
            userId: Guid.NewGuid(),
            familyId: Guid.NewGuid(),
            tokenHash: "validhash",
            rotatedFromId: null,
            expiresAt: now.AddDays(7),
            now: now
        );

        // Act
        var isActive = refreshToken.IsActive(now);

        // Assert
        Assert.True(isActive);
    }

    [Fact]
    public void IsReused_AfterRevoke_ReturnsTrue()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var refreshToken = new RefreshToken(
            userId: Guid.NewGuid(),
            familyId: Guid.NewGuid(),
            tokenHash: "validhash",
            rotatedFromId: null,
            expiresAt: now.AddDays(7),
            now: now
        );

        // Act
        refreshToken.Revoke(now);

        // Assert
        Assert.True(refreshToken.IsReused());
    }

    [Fact]
    public void Revoke_Twice_KeepsFirstRevocationInstant()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var refreshToken = new RefreshToken(
            userId: Guid.NewGuid(),
            familyId: Guid.NewGuid(),
            tokenHash: "validhash",
            rotatedFromId: null,
            expiresAt: now.AddDays(7),
            now: now
        );
        refreshToken.Revoke(now);

        // Act
        refreshToken.Revoke(now.AddMinutes(5));

        // Assert
        Assert.Equal(now, refreshToken.RevokedAt);
    }
}
