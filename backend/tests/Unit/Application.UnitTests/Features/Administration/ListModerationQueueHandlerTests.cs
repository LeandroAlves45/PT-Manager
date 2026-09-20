using Application.Features.Administration.ContentModeration;
using Application.Features.Administration.ContentModeration.ListModerationQueue;

namespace Application.UnitTests.Features;

public sealed class ListModerationQueueHandlerTests : ReadHandlerTestContext
{
    [Fact]
    public async Task ModerationQueue_WithTrainerActor_IsForbidden()
    {
        var handler = new ListModerationQueueHandler(
            new ListModerationQueueQueryValidator(),
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new ModerationQueueQueriesFake());

        var result = await handler.HandleAsync(
            new ListModerationQueueQuery(ModerationContentKind.Food), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ContentModerationErrors.AdministratorOnly.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ModerationQueue_WithInvalidPageSize_FailsValidationBeforeReading()
    {
        var queries = new ModerationQueueQueriesFake();
        var handler = new ListModerationQueueHandler(
            new ListModerationQueueQueryValidator(),
            new AdministrativeTenantStub(),
            queries);

        var result = await handler.HandleAsync(
            new ListModerationQueueQuery(ModerationContentKind.Food, PageSize: 500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task ModerationQueue_WithAdministrator_NormalizesSearch()
    {
        var queries = new ModerationQueueQueriesFake();
        var handler = new ListModerationQueueHandler(
            new ListModerationQueueQueryValidator(),
            new AdministrativeTenantStub(),
            queries);

        var result = await handler.HandleAsync(
            new ListModerationQueueQuery(
                ModerationContentKind.Exercise, ModerationStatusFilter.Blocked, "   "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(queries.LastSearch);
        Assert.Equal(ModerationStatusFilter.Blocked, queries.LastStatus);
        Assert.Equal(ModerationContentKind.Exercise, queries.LastKind);
    }

}
