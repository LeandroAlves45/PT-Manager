using Application.Features.Administration.Overview;

namespace Application.UnitTests.Features;

public sealed class GetAdminOverviewHandlerTests : ReadHandlerTestContext
{
    [Fact]
    public async Task AdminOverview_WithTrainerActor_IsForbidden()
    {
        var handler = new GetAdminOverviewHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"), new AdminOverviewQueriesFake());

        var result = await handler.HandleAsync(new GetAdminOverviewQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminOverviewErrors.AdministratorOnly.Code, result.Error!.Code);
    }

    [Fact]
    public async Task AdminOverview_WithAdministrator_ReturnsCounts()
    {
        var handler = new GetAdminOverviewHandler(
            new AdministrativeTenantStub(), new AdminOverviewQueriesFake());

        var result = await handler.HandleAsync(new GetAdminOverviewQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.GlobalFoods.TotalCount);
        Assert.Equal(1, result.Value.PrivateExercises.BlockedCount);
    }

}
