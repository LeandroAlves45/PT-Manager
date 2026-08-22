using Application.Common.Abstractions;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Features.Clients.GetClientBranding;
using Application.Features.TrainerSettings;

namespace Application.UnitTests.Features.Clients;

public sealed class GetClientBrandingHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenRoleIsNotClient_ReturnsClientOnlyWithoutQuerying()
    {
        var queries = new FakeQueries(new ClientBrandingDto("PT Manager", null, null, null));
        var handler = new GetClientBrandingHandler(
            new StubTenantContext(Guid.NewGuid(), Guid.NewGuid(), "trainer"), queries);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.Equal((TrainerSettingsErrors.ClientOnly.Code, false),
            (result.Error!.Code, queries.WasCalled));
    }

    [Fact]
    public async Task HandleAsync_WhenBrandingExists_ReturnsApprovedProjection()
    {
        var dto = new ClientBrandingDto("Studio Fit", "https://cdn/logo.png", "#112233", "#445566");
        var handler = new GetClientBrandingHandler(
            new StubTenantContext(Guid.NewGuid(), Guid.NewGuid(), "client"), new FakeQueries(dto));

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(dto, result.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomLogoIsAbsent_ReturnsSuccessfulNullLogo()
    {
        var handler = new GetClientBrandingHandler(
            new StubTenantContext(Guid.NewGuid(), Guid.NewGuid(), "client"),
            new FakeQueries(new ClientBrandingDto("PT Manager", null, null, null)));

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.Equal((true, null), (result.IsSuccess, result.Value.LogoUrl));
    }

    [Fact]
    public async Task HandleAsync_WhenBrandingIsUnavailable_ReturnsCollapsedNotFound()
    {
        var handler = new GetClientBrandingHandler(
            new StubTenantContext(Guid.NewGuid(), Guid.NewGuid(), "client"), new FakeQueries(null));

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TrainerSettingsErrors.BrandingNotAvailable.Code, result.Error!.Code);
    }

    [Fact]
    public void ClientBrandingDto_DoesNotExposeInternalIdentifiers()
    {
        var names = typeof(ClientBrandingDto).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(names,
            property => property is "Id" or "TrainerId" or "OwnerTrainerId" or "LogoPublicId");
    }

    private sealed class FakeQueries(ClientBrandingDto? dto) : IClientBrandingQueries
    {
        public bool WasCalled { get; private set; }

        public Task<ClientBrandingDto?> GetAsync(
            Guid trainerId, Guid clientUserId, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(dto);
        }
    }

    private sealed class StubTenantContext(Guid trainerId, Guid userId, string role) : ITenantContext
    {
        public Guid? TrainerId => trainerId;
        public Guid? UserId => userId;
        public string? Role => role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }
}
