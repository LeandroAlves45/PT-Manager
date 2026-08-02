using Domain.Entities.Identity;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Unit.Domain.UnitTests.Entities.Identity;

public sealed class InviteTokenTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_PreservesTargerClient()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var invite = CreateInvite(clientId);

        // Assert
        Assert.Equal(clientId, invite.ClientId);
    }

    [Fact]
    public void MarlUsed_WhenInviteIsValid_RecordsUsageTime()
    {
        // Arrange
        var invite = CreateInvite(Guid.NewGuid());
        var usedAt = Now.AddMinutes(5);

        // Act
        invite.MarkUsed(usedAt);

        // Assert
        Assert.Equal(usedAt, invite.UsedAt);
    }

    [Fact]
    public void MarkUsed_WhenInviteWasAlreadyUsed_ThrowsDomainException()
    {
        // Arrange
        var invite = CreateInvite(Guid.NewGuid());
        invite.MarkUsed(Now.AddMinutes(1));

        // Act & Assert
        var action = () => invite.MarkUsed(Now.AddMinutes(2));

        Assert.Throws<DomainException>(action);
    }

    private static InviteToken CreateInvite(Guid clientId) => new(
        Guid.NewGuid(),
        clientId,
        new EmailAddress("client@example.com"),
        "token-hash",
        Now.AddDays(2),
        Now
    );
}

