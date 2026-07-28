using Domain.Entities.Billing;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Billing;

public sealed class ClientSessionPackTests
{
    [Fact]
    public void ConsumeSession_LastSession_LeavesZeroRemaining()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var purchaseDate = new DateOnly(2026, 7, 25);
        var sessionPack = new ClientSessionPack(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            purchaseDate,
            null,
            now
        );

        // Act
        var today = new DateOnly(2026, 7, 26);
        sessionPack.ConsumeSession(today, now);

        // Assert
        Assert.Equal(0, sessionPack.SessionsRemaining);
    }

    [Fact]
    public void ConsumeSession_ExpiredPack_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var purchaseDate = new DateOnly(2026, 7, 25);
        var sessionPack = new ClientSessionPack(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            purchaseDate,
            purchaseDate.AddDays(30),
            now
        );
        var today = new DateOnly(2026, 8, 26); // After expiration

        // Act & Assert
        Assert.Throws<DomainException>(() => sessionPack.ConsumeSession(today, now));
    }

    [Fact]
    public void ConsumeSession_DeletedPack_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var purchaseDate = new DateOnly(2026, 7, 25);
        var sessionPack = new ClientSessionPack(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            purchaseDate,
            null,
            now
        );
        sessionPack.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            sessionPack.ConsumeSession(purchaseDate, now.AddMinutes(1)));
    }
}
