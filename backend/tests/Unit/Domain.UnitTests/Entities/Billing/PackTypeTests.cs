using Domain.Entities.Billing;
using Domain.Exceptions;

namespace Domain.UnitTests.Entities.Billing;

public sealed class PackTypeTests
{
    private static readonly DateTime Now =
        new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_ValidInput_NormalizesCommercialFields()
    {
        var pack = new PackType(
            Guid.NewGuid(),
            "  Ten sessions  ",
            10,
            30000,
            " eur ",
            30,
            Now
        );

        Assert.Equal("Ten sessions", pack.Name);
        Assert.Equal("EUR", pack.Currency);
        Assert.Equal(30, pack.ExpectedDurationDays);
        Assert.True(pack.IsActive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidExpectedDuration_Throws(int duration)
    {
        Assert.Throws<DomainException>(() => new PackType(
            Guid.NewGuid(), "Pack", 10, 1000, "EUR", duration, Now
        ));
    }

    [Fact]
    public void Update_ValidInput_PreservesIdentityAndCreation()
    {
        var pack = CreatePack();
        var id = pack.Id;

        pack.Update("New", 12, 12000, "usd", null, Now.AddMinutes(1));

        Assert.Equal(id, pack.Id);
        Assert.Equal(Now, pack.CreatedAt);
        Assert.Equal("USD", pack.Currency);
        Assert.Null(pack.ExpectedDurationDays);
    }

    [Fact]
    public void Archive_Repeated_PreservesFirstTransitionTimestamp()
    {
        var pack = CreatePack();
        var changedAt = Now.AddMinutes(1);

        pack.Archive(changedAt);
        pack.Archive(Now.AddMinutes(2));

        Assert.False(pack.IsActive);
        Assert.Equal(changedAt, pack.UpdatedAt);
    }

    [Fact]
    public void Reactivate_Repeated_PreservesFirstTransitionTimestamp()
    {
        var pack = CreatePack();
        pack.Archive(Now.AddMinutes(1));
        var changedAt = Now.AddMinutes(2);

        pack.Reactivate(changedAt);
        pack.Reactivate(Now.AddMinutes(3));

        Assert.True(pack.IsActive);
        Assert.Equal(changedAt, pack.UpdatedAt);
    }

    [Fact]
    public void Update_SoftDeletedPack_Throws()
    {
        var pack = CreatePack();
        pack.SoftDelete(Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => pack.Update(
            "New", 12, 1000, "EUR", 20, Now.AddMinutes(2)
        ));
    }

    private static PackType CreatePack() => new(
        Guid.NewGuid(), "Pack", 10, 10000, "EUR", 30, Now
    );
}
