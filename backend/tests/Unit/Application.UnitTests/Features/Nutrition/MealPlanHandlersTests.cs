using Application.Common.Abstractions;
using Application.Features.Nutrition.MealPlans;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Features.Nutrition.MealPlans.ArchiveMealPlan;
using Application.Features.Nutrition.MealPlans.CreateMealPlan;
using Application.Features.Nutrition.MealPlans.Dtos;
using Application.Features.Nutrition.MealPlans.ListMealPlans;
using Application.Features.Nutrition.MealPlans.ReactivateMealPlan;
using Application.Features.Nutrition.MealPlans.UpdateMealPlan;
using Application.Pagination;

namespace Application.UnitTests.Features.Nutrition;

public sealed class MealPlanHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_ValidCommand_SendsServerSnapshotAndFinalTreeToStore()
    {
        // Arrange
        var store = new FakeMealPlanStore { CreateResult = MealPlanStoreResult.ForCreated(Guid.NewGuid()) };
        var queries = new FakeMealPlanQueries { Details = CreateDetails(store.CreateResult.MealPlanId!.Value) };
        var handler = new CreateMealPlanHandler(
            new CreateMealPlanCommandValidator(),
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store,
            queries);

        var command = CreateCommand();

        // Act
        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Now, store.CreatedModel!.Calculation.CalculatedAt);
        Assert.Equal(command.Structure, store.CreatedModel.Structure);
    }

    [Theory]
    [InlineData(MealPlanStoreResult.Status.ClientNotFound, "nutrition_client_not_found")]
    [InlineData(MealPlanStoreResult.Status.CatalogReferenceNotFound, "nutrition_catalog_reference_not_found")]
    [InlineData(MealPlanStoreResult.Status.CatalogReferenceInactive, "nutrition_catalog_reference_inactive")]
    public async Task Create_ExpectedStoreFailure_MapsStableError(
        MealPlanStoreResult.Status status,
        string expectedCode
    )
    {
        // Arrange
        var store = new FakeMealPlanStore { CreateResult = CreateFailure(status) };
        var queries = new FakeMealPlanQueries();
        var handler = new CreateMealPlanHandler(
            new CreateMealPlanCommandValidator(),
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store,
            queries);

        // Act
        var result = await handler.HandleAsync(CreateCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(0, queries.DetailsCalls);
    }

    [Fact]
    public async Task Update_WithoutCalculation_SendsNullReplacement()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var store = new FakeMealPlanStore { UpdateResult = MealPlanStoreResult.ForUpdated(planId) };
        var queries = new FakeMealPlanQueries { Details = CreateDetails(planId) };

        // Act
        var result = await CreateUpdateHandler(store, queries).HandleAsync(
            UpdateCommand(planId, null),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(store.UpdatedModel!.ReplacementCalculation);
    }

    [Fact]
    public async Task Update_WithCalculation_SendsNewServerSnapshot()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var store = new FakeMealPlanStore { UpdateResult = MealPlanStoreResult.ForUpdated(planId) };
        var queries = new FakeMealPlanQueries { Details = CreateDetails(planId) };

        // Act
        await CreateUpdateHandler(store, queries).HandleAsync(
            UpdateCommand(planId, CalculationInput()),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Now, store.UpdatedModel!.ReplacementCalculation!.CalculatedAt);
        Assert.Equal(1, store.UpdatedModel.ReplacementCalculation.SchemaVersion);
    }

    [Fact]
    public async Task Update_StructureForeignId_ReturnsSafeNotFound()
    {
        var store = new FakeMealPlanStore
        {
            UpdateResult = MealPlanStoreResult.ForStructureReferenceNotFound()
        };

        // Act
        var result = await CreateUpdateHandler(store, new FakeMealPlanQueries()).HandleAsync(
            UpdateCommand(Guid.NewGuid(), null),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("meal_plan_structure_reference_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task List_CrossTenantClientFilter_ReturnsEmptyPageFromQuery()
    {
        var queries = new FakeMealPlanQueries();
        var handler = new ListMealPlansHandler(
            new ListMealPlansQueryValidator(), new TenantStub(TrainerId), queries
        );

        var result = await handler.HandleAsync(
            new ListMealPlansQuery(Guid.NewGuid(), null), TestContext.Current.CancellationToken
        );

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchiveReactivate_IdempotentOutcomesSucceed(bool changed)
    {
        var store = new FakeMealPlanStore
        {
            TransitionResult = changed
                ? MealPlanStoreResult.ForChanged()
                : MealPlanStoreResult.ForAlreadyRequested()
        };
        var tenant = new TenantStub(TrainerId);
        var clock = new ClockStub(Now);

        var archive = await new ArchiveMealPlanHandler(tenant, clock, store).HandleAsync(
            new ArchiveMealPlanCommand(Guid.NewGuid()), TestContext.Current.CancellationToken
        );
        var reactivate = await new ReactivateMealPlanHandler(tenant, clock, store).HandleAsync(
            new ReactivateMealPlanCommand(Guid.NewGuid()), TestContext.Current.CancellationToken
        );

        Assert.True(archive.IsSuccess);
        Assert.True(reactivate.IsSuccess);
    }

    private static UpdateMealPlanHandler CreateUpdateHandler(
        FakeMealPlanStore store,
        FakeMealPlanQueries queries
    ) => new(
        new UpdateMealPlanCommandValidator(), new TenantStub(TrainerId),
        new ClockStub(Now), store, queries
    );

    private static CreateMealPlanCommand CreateCommand() => new(
        Guid.NewGuid(), "Plan", null, new DateOnly(2026, 8, 10), null,
        CalculationInput(), new MealPlanStructureInput([])
    );

    private static UpdateMealPlanCommand UpdateCommand(
        Guid id,
        Application.Features.Nutrition.Calculations.NutritionCalculationInput? calculation
    ) => new(id, "Plan", null, new DateOnly(2026, 8, 10), null, calculation,
        new MealPlanStructureInput([]));

    private static Application.Features.Nutrition.Calculations.NutritionCalculationInput CalculationInput() =>
        new("manual_energy", null, 80m, null, null, null, null, null, null, null,
            2000m, "percentage", 30m, 40m, 30m, null, null, null, null, null);

    private static MealPlanDetailsDto CreateDetails(Guid id) => new(
        id, Guid.NewGuid(), "Plan", null, new DateOnly(2026, 8, 10), null,
        new(1, "manual_energy", Now, null, 80m, null, null, null, null, null, null,
            null, null, null, null, 2000m, "percentage", 150m, 200m, 66.67m,
            30m, 40m, 30m, 2000m, 0m),
        NutritionTotalsDto.Zero, true, false, [], Now, Now
    );

    private static MealPlanStoreResult CreateFailure(MealPlanStoreResult.Status status) => status switch
    {
        MealPlanStoreResult.Status.ClientNotFound => MealPlanStoreResult.ForClientNotFound(),
        MealPlanStoreResult.Status.CatalogReferenceNotFound => MealPlanStoreResult.ForCatalogReferenceNotFound(),
        MealPlanStoreResult.Status.CatalogReferenceInactive => MealPlanStoreResult.ForCatalogReferenceInactive(),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private sealed class FakeMealPlanStore : IMealPlanStore
    {
        public MealPlanStoreResult CreateResult { get; init; } = MealPlanStoreResult.ForClientNotFound();
        public MealPlanStoreResult UpdateResult { get; init; } = MealPlanStoreResult.ForNotFound();
        public MealPlanStoreResult TransitionResult { get; init; } = MealPlanStoreResult.ForNotFound();
        public CreateMealPlanWriteModel? CreatedModel { get; private set; }
        public UpdateMealPlanWriteModel? UpdatedModel { get; private set; }

        public Task<MealPlanStoreResult> CreateAsync(Guid trainerId, CreateMealPlanWriteModel model,
            DateTime now, CancellationToken cancellationToken)
        { CreatedModel = model; return Task.FromResult(CreateResult); }
        public Task<MealPlanStoreResult> UpdateAsync(Guid trainerId, UpdateMealPlanWriteModel model,
            DateTime now, CancellationToken cancellationToken)
        { UpdatedModel = model; return Task.FromResult(UpdateResult); }
        public Task<MealPlanStoreResult> SetArchivedAsync(Guid mealPlanId, Guid trainerId,
            bool isArchived, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(TransitionResult);
    }

    private sealed class FakeMealPlanQueries : IMealPlanQueries
    {
        public MealPlanDetailsDto? Details { get; init; }
        public int DetailsCalls { get; private set; }
        public Task<MealPlanDetailsDto?> GetDetailsAsync(Guid id, CancellationToken token)
        { DetailsCalls++; return Task.FromResult(Details); }
        public Task<PageResult<MealPlanSummaryDto>> ListAsync(Guid? clientId, string? search,
            MealPlanActivityFilter activity, PageRequest page, CancellationToken token) =>
            Task.FromResult(new PageResult<MealPlanSummaryDto>([], 0));
    }

    private sealed class ClockStub(DateTime utcNow) : IClock
    { public DateTime UtcNow { get; } = utcNow; }

    private sealed class TenantStub(
        Guid? trainerId,
        Guid? userId = null,
        string? role = "trainer"
    ) : ITenantContext
    {
        public Guid? TrainerId { get; } = trainerId;
        public Guid? UserId { get; } = userId ?? Guid.NewGuid();
        public string? Role { get; } = role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }
}


