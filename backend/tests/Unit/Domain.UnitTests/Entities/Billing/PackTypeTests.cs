using Domain.Entities.Billing;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Billing;

public sealed class PackTypeTests
{
    [Fact]
    public void Constructor_NameWithOuterWhitespace_StoresNormalizedName()
    {
        // Arrange & Act
        var packType = new PackType(
            Guid.NewGuid(),
            "  Ten sessions  ",
            10,
            30000,
            90,
            new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc)
        );

        // Assert
        Assert.Equal("Ten sessions", packType.Name);
    }

    [Fact]
    public void Update_NameWithOuterWhitespace_StoresNormalizedName()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var packType = new PackType(Guid.NewGuid(), "Ten sessions", 10, 30000, 90, now);

        // Act
        packType.Update("  Twelve sessions  ", 12, 35000, 90, now);

        // Assert
        Assert.Equal("Twelve sessions", packType.Name);
    }

    [Fact]
    public void Update_DeletedPackType_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var packType = new PackType(Guid.NewGuid(), "Ten sessions", 10, 30000, 90, now);
        packType.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            packType.Update("Twelve sessions", 12, 35000, 90, now));
    }
}
