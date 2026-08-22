using Domain.Exceptions;
using TrainerSettingsEntity = Domain.Entities.TrainerSettings.TrainerSettings;

namespace Domain.UnitTests.Entities.TrainerSettings;

public sealed class TrainerSettingsTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_WithValidTrainer_UsesApprovedDefaults()
    {
        var settings = new TrainerSettingsEntity(Guid.NewGuid(), Now);

        Assert.Equal(("PT Manager", "Europe/Lisbon", null, null, null, null),
            (settings.AppName, settings.Timezone, settings.PrimaryColor,
                settings.BodyColor, settings.LogoUrl, settings.LogoPublicId));
    }

    [Fact]
    public void Constructor_WhenTrainerIdIsEmpty_ThrowsDomainException()
    {
        var action = () => new TrainerSettingsEntity(Guid.Empty, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void UpdateBranding_WithFiftyCharacterNormalizedName_Succeeds()
    {
        var settings = CreateSettings();
        var appName = new string('A', 50);

        settings.UpdateBranding($"  {appName}  ", null, null, Now.AddMinutes(1));

        Assert.Equal(appName, settings.AppName);
    }

    [Fact]
    public void UpdateBranding_WithFiftyOneCharacterNormalizedName_ThrowsDomainException()
    {
        var settings = CreateSettings();
        var action = () => settings.UpdateBranding(
            $"  {new string('A', 51)}  ", null, null, Now.AddMinutes(1));

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("   ")]
    public void UpdateBranding_WhenNormalizedNameIsTooShort_ThrowsDomainException(string appName)
    {
        var settings = CreateSettings();
        var action = () => settings.UpdateBranding(appName, null, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("#00000")]
    [InlineData("#GGGGGG")]
    public void UpdateBranding_WhenColorIsInvalid_ThrowsDomainException(string color)
    {
        var settings = CreateSettings();
        var action = () => settings.UpdateBranding("Studio Fit", color, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void ResetColors_WhenColorsAreSet_ClearsBoth()
    {
        var settings = CreateSettings();
        settings.UpdateBranding("Studio Fit", "#112233", "#445566", Now);

        settings.ResetColors(Now.AddMinutes(1));

        Assert.Equal((null, null), (settings.PrimaryColor, settings.BodyColor));
    }

    [Fact]
    public void ResetColors_WhenAlreadyNull_DoesNotChangeTimestamp()
    {
        var settings = CreateSettings();

        settings.ResetColors(Now.AddMinutes(1));

        Assert.Equal(Now, settings.UpdatedAt);
    }

    [Fact]
    public void ReplaceLogo_WhenLogoExists_ReturnsPreviousPublicId()
    {
        var settings = CreateSettings();
        settings.ReplaceLogo("https://cdn/logo-1.png", "logo-1", Now);

        var previous = settings.ReplaceLogo("https://cdn/logo-2.png", "logo-2", Now.AddMinutes(1));

        Assert.Equal(("logo-1", "logo-2"), (previous, settings.LogoPublicId));
    }

    [Fact]
    public void RemoveLogo_WhenLogoExists_ClearsReferences()
    {
        var settings = CreateSettings();
        settings.ReplaceLogo("https://cdn/logo.png", "logo", Now);

        var previous = settings.RemoveLogo(Now.AddMinutes(1));

        Assert.Equal(("logo", null, null), (previous, settings.LogoUrl, settings.LogoPublicId));
    }

    [Fact]
    public void RemoveLogo_WhenNoLogo_DoesNotChangeTimestamp()
    {
        var settings = CreateSettings();

        var previous = settings.RemoveLogo(Now.AddMinutes(1));

        Assert.Equal((null, Now), (previous, settings.UpdatedAt));
    }

    [Theory]
    [InlineData("Lisbon")]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeTimezone_WhenNormalizedShapeIsInvalid_ThrowsDomainException(string timezone)
    {
        var settings = CreateSettings();
        var action = () => settings.ChangeTimezone(timezone, Now.AddMinutes(1));

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void ChangeTimezone_ToSameNormalizedValue_DoesNotChangeTimestamp()
    {
        var settings = CreateSettings();

        settings.ChangeTimezone("  Europe/Lisbon  ", Now.AddMinutes(1));

        Assert.Equal(Now, settings.UpdatedAt);
    }

    [Fact]
    public void ChangeTimezone_ToDifferentValue_UpdatesNormalizedValueAndTimestamp()
    {
        var settings = CreateSettings();

        settings.ChangeTimezone("  America/Sao_Paulo  ", Now.AddMinutes(1));

        Assert.Equal(("America/Sao_Paulo", Now.AddMinutes(1)),
            (settings.Timezone, settings.UpdatedAt));
    }

    [Theory]
    [InlineData(21, 0, 0)]
    [InlineData(0, 501, 0)]
    [InlineData(0, 0, 256)]
    public void UpdateContacts_WhenAFieldExceedsLimit_ThrowsDomainException(
        int phoneLength, int addressLength, int cityLength)
    {
        var settings = CreateSettings();
        var action = () => settings.UpdateContacts(
            phoneLength == 0 ? null : new string('1', phoneLength),
            addressLength == 0 ? null : new string('A', addressLength),
            cityLength == 0 ? null : new string('C', cityLength), Now);

        Assert.Throws<DomainException>(action);
    }

    private static TrainerSettingsEntity CreateSettings() => new(Guid.NewGuid(), Now);
}
