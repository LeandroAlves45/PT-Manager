using Domain.Entities.Supplements;
using Domain.Exceptions;

namespace Domain.UnitTests.Entities.Supplements;

public sealed class SupplementTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_NormalizesFieldsAndStartsActive()
    {
        var creator = Guid.NewGuid();

        var supplement = new Supplement(
            Guid.NewGuid(), creator, "  Creatine  ", "  Pure  ", " grams ",
            " 5 g ", " daily ", " internal ", Now);

        Assert.Equal(("Creatine", "Pure", "grams", "5 g", "daily", "internal"),
            (supplement.Name, supplement.Description, supplement.UnitOfMeasure,
                supplement.ServingSize, supplement.Timing, supplement.TrainerNotes));
        Assert.Equal(creator, supplement.CreatedByUserId);
        Assert.True(supplement.IsActive);
    }

    [Fact]
    public void Constructor_WhenCreatorIsEmpty_ThrowsDomainException()
    {
        var action = () => new Supplement(
            null, Guid.Empty, "Creatine", null, "grams", "5 g", "daily", null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData(256, "grams", "5 g", "daily")]
    [InlineData(1, "", "5 g", "daily")]
    [InlineData(1, "grams", "", "daily")]
    [InlineData(1, "grams", "5 g", "")]
    public void Constructor_WhenARequiredFieldExceedsItsBoundary_ThrowsDomainException(
        int nameLength, string unit, string servingSize, string timing)
    {
        var action = () => new Supplement(
            null, Guid.NewGuid(), new string('x', nameLength), null,
            unit, servingSize, timing, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void ArchiveAndReactivate_PreserveIdentityAndChangeAvailability()
    {
        var supplement = CreateSupplement();

        supplement.Archive(Now.AddMinutes(1));
        supplement.Reactivate(Now.AddMinutes(2));

        Assert.True(supplement.IsActive);
        Assert.Equal(Now.AddMinutes(2), supplement.UpdatedAt);
    }

    [Fact]
    public void PublicSurface_DoesNotExposeSoftDelete()
    {
        var properties = typeof(Supplement).GetProperties().Select(property => property.Name);
        var methods = typeof(Supplement).GetMethods().Select(method => method.Name);

        Assert.DoesNotContain("IsDeleted", properties);
        Assert.DoesNotContain("SoftDelete", methods);
    }

    private static Supplement CreateSupplement() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Creatine", null,
        "grams", "5 g", "daily", null, Now);
}
