using Domain.Entities.Identity;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Unit.Domain.UnitTests.Entities.Identity;

public sealed class InviteTokenTests
{
    [Fact]
    public void MarkUsed_ValidInvite_SetIsUsed()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var email = new EmailAddress("test@example.com");
        var inviteToken = new InviteToken(
            Guid.NewGuid(),
            email,
            "validhash",
            now.AddDays(1),
            now
        );

        // Act
        inviteToken.MarkUsed(now);

        // Assert
        Assert.True(inviteToken.IsUsed);
    }

    [Fact]
    public void MarkUsed_ExpiredInvite_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var expiredAt = now.AddDays(2); // Expira no futuro.
        var email = new EmailAddress("test@example.com");
        var inviteToken = new InviteToken(
            Guid.NewGuid(),
            email,
            "validhash",
            expiredAt, // Expired
            now
        );

        // Act & Assert
        var currentTime = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
        var exception = Assert.Throws<DomainException>(() => inviteToken.MarkUsed(currentTime));
        Assert.Equal("Invite invalid, used or expired", exception.Message);
    }
}

