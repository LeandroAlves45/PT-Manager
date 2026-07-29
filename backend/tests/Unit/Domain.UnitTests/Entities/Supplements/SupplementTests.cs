using Domain.Entities.Supplements;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Supplements;

public sealed class SupplementTests
{

    /// <summary>Campos estáticos para os testes.</summary>
    private static readonly DateTime Now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TrainerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AuthorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Constructor_PrivateSupplement_PreservesOwnerTrainerId()
    {
        // Arrange & Act
        var supplement = new Supplement(TrainerId, AuthorId, "Creatine", null, "grams", Now);

        // Assert
        Assert.Equal(TrainerId, supplement.OwnerTrainerId);
    }

    [Fact]
    public void Constructor_GlobalSupplement_KeepsOwnerTrainerIdNull()
    {
        // Arrange & Act
        var supplement = new Supplement(null, AuthorId, "Creatine", null, "grams", Now);

        // Assert
        Assert.Null(supplement.OwnerTrainerId);
    }

    [Fact]
    public void Constructor_OwnerAndAuthorDiffer_PreservesBothRoles()
    {
        // Arrange & Act
        var supplement = new Supplement(TrainerId, AuthorId, "Creatine", null, "grams", Now);

        // Assert
        Assert.Equal(TrainerId, supplement.OwnerTrainerId);
        Assert.Equal(AuthorId, supplement.CreatedByUserId);
    }

    [Fact]
    public void Update_DeletedSupplement_ThrowsDomainException()
    {
        // Arrange
        var supplement = new Supplement(null, AuthorId, "Creatine", null, "grams", Now);
        supplement.SoftDelete(Now);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            supplement.Update("Creatine monohydrate", null, "grams", Now.AddMinutes(1)));
    }
}
