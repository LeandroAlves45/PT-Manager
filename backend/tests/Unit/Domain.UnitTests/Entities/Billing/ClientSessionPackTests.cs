using Domain.Entities.Billing;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Billing;

public sealed class ClientSessionPackTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_CopiesCommercialSnapshots()
    {
        // Arrange
        var trainerId = Guid.NewGuid();
        var packType = CreatePackType(trainerId);

        // Act
        var pack = new ClientSessionPack(
            trainerId,
            Guid.NewGuid(),
            packType,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 10, 30),
            Now
        );

        // Assert
        Assert.Equal(("Pack 10", 10, 30000, "EUR"),
            (pack.PackName, pack.SessionsTotal, pack.PriceCents, pack.Currency));
    }

    [Fact]
    public void Constructor_WhenPackBelongsToAnotherTrainer_ThrowsDomainException()
    {
        // Arrange
        var packType = CreatePackType(Guid.NewGuid());

        // Act & Assert
        var action = () => new ClientSessionPack(
            Guid.NewGuid(),
            Guid.NewGuid(),
            packType,
            new DateOnly(2026, 8, 1),
            null,
            Now
        );

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void ConsumeSession_WhenPackIsUsable_DecrementsBalance()
    {
        // Arrange
        var trainerId = Guid.NewGuid();
        var pack = new ClientSessionPack(
            trainerId,
            Guid.NewGuid(),
            CreatePackType(trainerId),
            new DateOnly(2026, 8, 1),
            null,
            Now
        );

        pack.ConsumeSession(new DateOnly(2026, 8, 1), Now.AddMinutes(1));

        // Assert
        Assert.Equal(9, pack.SessionsRemaining);
    }

    private static PackType CreatePackType(Guid trainerId) => new(
        trainerId,
        "Pack 10",
        10,
        30000,
        "eur",
        90,
        Now
    );
}
