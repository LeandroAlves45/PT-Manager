using Application.Common.Abstractions;
using Application.Features.TrainerSettings;
using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.ChangeTimezone;
using Application.Features.TrainerSettings.Dtos;
using Application.Features.TrainerSettings.UpdateBranding;
using TrainerSettingsEntity = Domain.Entities.TrainerSettings.TrainerSettings;

namespace Application.UnitTests.Features.TrainerSettings;

public sealed class ChangeTimezoneHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_WhenTimezoneIsInvalid_ReturnsValidationWithoutCallingStore()
    {
        var store = new FakeStore();
        var handler = CreateHandler(store);

        var result = await handler.HandleAsync(
            new ChangeTimezoneCommand("Not/AZone"), TestContext.Current.CancellationToken);

        Assert.Equal(("validation_failed", false), (result.Error!.Code, store.WasCalled));
    }

    [Fact]
    public async Task HandleAsync_WhenTimezoneUsesWindowsIdentifier_ReturnsValidationFailure()
    {
        var store = new FakeStore();
        var handler = CreateHandler(store);

        var result = await handler.HandleAsync(
            new ChangeTimezoneCommand("GMT Standard Time"),
            TestContext.Current.CancellationToken);

        Assert.Equal(("validation_failed", false), (result.Error!.Code, store.WasCalled));
    }

    [Fact]
    public async Task HandleAsync_WhenTimezoneHasOuterWhitespace_AcceptsNormalizedValidation()
    {
        var store = new FakeStore();
        var handler = CreateHandler(store);

        var result = await handler.HandleAsync(
            new ChangeTimezoneCommand("  Europe/Lisbon  "), TestContext.Current.CancellationToken);

        Assert.Equal(
            (true, true, "Europe/Lisbon"),
            (result.IsSuccess, store.WasCalled, store.ReceivedTimezone));
    }

    [Fact]
    public async Task HandleAsync_WhenStoreDetectsConflict_ReturnsScheduleConflict()
    {
        var store = new FakeStore { Result = TrainerSettingsStoreResult.Conflict() };
        var handler = CreateHandler(store);

        var result = await handler.HandleAsync(
            new ChangeTimezoneCommand("America/Sao_Paulo"), TestContext.Current.CancellationToken);

        Assert.Equal(TrainerSettingsErrors.ScheduleConflict.Code, result.Error!.Code);
    }

    [Fact]
    public async Task UpdateBrandingValidator_WithFiftyNormalizedCharacters_IsValid()
    {
        var validator = new UpdateBrandingCommandValidator();
        var command = new UpdateBrandingCommand($"  {new string('A', 50)}  ", null, null);

        var result = await validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task UpdateBrandingValidator_WithFiftyOneNormalizedCharacters_IsInvalid()
    {
        var validator = new UpdateBrandingCommandValidator();
        var command = new UpdateBrandingCommand($"  {new string('A', 51)}  ", null, null);

        var result = await validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors,
            failure => failure.ErrorCode == "trainer_settings_app_name_length");
    }

    [Fact]
    public void TrainerSettingsDto_DoesNotExposeInternalIdentifiers()
    {
        var propertyNames = typeof(TrainerSettingsDto).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(propertyNames,
            property => property is "Id" or "TrainerId" or "LogoPublicId");
    }

    private static ChangeTimezoneHandler CreateHandler(FakeStore store) =>
        new(new ChangeTimezoneCommandValidator(), new StubTenantContext(), new StubClock(), store);

    private sealed class FakeStore : ITrainerSettingsStore
    {
        public bool WasCalled { get; private set; }
        public string? ReceivedTimezone { get; private set; }
        public TrainerSettingsStoreResult Result { get; set; } = TrainerSettingsStoreResult.Updated(
            new TrainerSettingsEntity(Guid.NewGuid(), Now));

        public Task<TrainerSettingsStoreResult> ChangeTimezoneAsync(
            Guid trainerId, string timezone, DateTime now, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedTimezone = timezone;
            return Task.FromResult(Result);
        }

        public Task<TrainerSettingsStoreResult> UpdateBrandingAsync(Guid trainerId, string appName,
            string? primaryColor, string? bodyColor, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
        public Task<TrainerSettingsStoreResult> ResetBrandingColorsAsync(Guid trainerId,
            DateTime now, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task<TrainerSettingsStoreResult> UpdateContactsAsync(Guid trainerId, string? phone,
            string? address, string? city, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
        public Task<TrainerSettingsStoreResult> ReplaceLogoAsync(Guid trainerId, string logoUrl,
            string logoPublicId, Guid correlationId, DateTime now,
            CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task<TrainerSettingsStoreResult> RemoveLogoAsync(Guid trainerId, Guid correlationId,
            DateTime now, CancellationToken cancellationToken) => Task.FromResult(Result);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid? TrainerId => Guid.NewGuid();
        public Guid? UserId => Guid.NewGuid();
        public string? Role => "trainer";
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class StubClock : IClock
    {
        public DateTime UtcNow => Now;
    }
}
