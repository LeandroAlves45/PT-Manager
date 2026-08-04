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
        var supplement = new Supplement(
            TrainerId, AuthorId, "Creatine", null, "grams", "5 g", "Daily", null, Now);

        // Assert
        Assert.Equal(TrainerId, supplement.OwnerTrainerId);
    }

    [Fact]
    public void Constructor_GlobalSupplement_KeepsOwnerTrainerIdNull()
    {
        // Arrange & Act
        var supplement = new Supplement(
            null, AuthorId, "Creatine", null, "grams", "5 g", "Daily", null, Now);

        // Assert
        Assert.Null(supplement.OwnerTrainerId);
    }

    [Fact]
    public void Constructor_OwnerAndAuthorDiffer_PreservesBothRoles()
    {
        // Arrange & Act
        var supplement = new Supplement(
            TrainerId,
            AuthorId,
            "Creatine",
            null,
            "grams",
            "5 g",
            "Daily",
            null,
            Now);

        // Assert
        Assert.Equal(TrainerId, supplement.OwnerTrainerId);
        Assert.Equal(AuthorId, supplement.CreatedByUserId);
    }

    [Fact]
    public void Update_DeletedSupplement_ThrowsDomainException()
    {
        // Arrange
        var supplement = new Supplement(
            null, AuthorId, "Creatine", null, "grams", "5 g", "Daily", null, Now);
        supplement.SoftDelete(Now);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            supplement.Update(
                "Creatine monohydrate", null, "grams", "5 g", "Daily", null,
                Now.AddMinutes(1)));
    }

    [Fact]
    public void Constructor_ValidSupplement_StartsActive()
    {
        // Arrange & Act
        var supplement = new Supplement(
            TrainerId, AuthorId, "Creatine", null, "grams", "5 g", "Daily", null, Now);

        // Assert
        Assert.True(supplement.IsActive);
    }

    [Theory]
    [InlineData("", "5 g", "Daily")]
    [InlineData("grams", "", "Daily")]
    [InlineData("grams", "5 g", "")]
    public void Constructor_RequiredServingFieldsAreBlank_ThrowsDomainException(
        string unitOfMeasure,
        string servingSize,
        string timing)
    {
        // Act
        var action = () => new Supplement(
            TrainerId,
            AuthorId,
            "Creatine",
            null,
            unitOfMeasure,
            servingSize,
            timing,
            null,
            Now);

        // Assert
        Assert.Throws<DomainException>(action);
    }
}
