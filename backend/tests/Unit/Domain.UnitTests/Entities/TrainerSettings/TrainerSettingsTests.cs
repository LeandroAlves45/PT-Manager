using Domain.Entities;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.TrainerSettings;

public sealed class TrainerSettingsTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_UsesLisbonTimezone()
    {
        // Arrange
        var settings = new Domain.Entities.TrainerSettings.TrainerSettings(
            Guid.NewGuid(),
            Now
        );

        // Assert
        Assert.Equal("Europe/Lisbon", settings.Timezone);
    }

    [Fact]
    public void ChangeTimezone_WhenIdentifierHasInvalidShape_ThrowsDomainException()
    {
        // Arrange
        var settings = new Domain.Entities.TrainerSettings.TrainerSettings(
            Guid.NewGuid(),
            Now
        );

        // Act & Assert
        var action = () => settings.ChangeTimezone("Lisbon", Now.AddMinutes(1));
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void UpdateBranding_WhenAppNameIsNull_ThrowsDomainException()
    {
        var settings = new Domain.Entities.TrainerSettings.TrainerSettings(
            Guid.NewGuid(), Now);

        var action = () => settings.UpdateBranding(
            null!, "#000000", "#FFFFFF", null, Now.AddMinutes(1));

        Assert.Throws<DomainException>(action);
    }
}
