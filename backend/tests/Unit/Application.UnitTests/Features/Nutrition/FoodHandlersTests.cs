using Application.Common.Abstractions;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.ArchiveFood;
using Application.Features.Nutrition.Foods.CreateFood;
using Application.Features.Nutrition.Foods.ReactivateFood;
using Application.Features.Nutrition.Foods.UpdateFood;
using Domain.Entities.Nutrition;

namespace Application.UnitTests.Features.Nutrition;

public sealed class FoodHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_ValidCommand_UsesEffectiveTenantAndReloadsPersistedFood()
    {
        var store = new FakeFoodStore();
        var handler = CreateHandler(store, TrainerId);

        var result = await handler.HandleAsync(
            ValidCreate(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(TrainerId, store.AddedFood!.OwnerTrainerId);
        Assert.Equal(1, store.ReadCalls);
        Assert.Equal("private", result.Value.Scope);
    }

    [Fact]
    public async Task Create_InvalidMacros_DoesNotCallStore()
    {
        var store = new FakeFoodStore();

        var result = await CreateHandler(store, TrainerId).HandleAsync(
            ValidCreate() with { Protein = 60m, Carbs = 60m },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Contains(
            result.Error!.ValidationErrors,
            error => error.Code == "food_macros_total_invalid");
        Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public async Task Update_GlobalFood_ReturnsForbidden()
    {
        var store = new FakeFoodStore
        {
            UpdateResult = FoodStoreResult.ForGlobalReadOnly()
        };
        var handler = new UpdateFoodHandler(
            new UpdateFoodCommandValidator(),
            new TenantContextStub(TrainerId),
            new ClockStub(Now),
            store);

        var result = await handler.HandleAsync(
            ValidUpdate(),
            TestContext.Current.CancellationToken);

        Assert.Equal("global_food_read_only", result.Error!.Code);
        Assert.Equal(Application.Errors.ErrorCategory.Forbidden, result.Error.Category);
    }

    [Fact]
    public async Task Update_OtherTenant_ReturnsNotFound()
    {
        var store = new FakeFoodStore
        {
            UpdateResult = FoodStoreResult.ForNotFound()
        };
        var handler = new UpdateFoodHandler(
            new UpdateFoodCommandValidator(),
            new TenantContextStub(TrainerId),
            new ClockStub(Now),
            store);

        var result = await handler.HandleAsync(
            ValidUpdate(),
            TestContext.Current.CancellationToken);

        Assert.Equal("food_not_found", result.Error!.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchiveAndReactivate_IdempotentOutcomesReturnSuccess(bool changed)
    {
        var store = new FakeFoodStore
        {
            ActiveResult = changed
                ? FoodStoreResult.ForChanged()
                : FoodStoreResult.ForAlreadyRequested()
        };
        var tenant = new TenantContextStub(TrainerId);
        var clock = new ClockStub(Now);

        var archive = await new ArchiveFoodHandler(tenant, clock, store).HandleAsync(
            new ArchiveFoodCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);
        var reactivate = await new ReactivateFoodHandler(tenant, clock, store).HandleAsync(
            new ReactivateFoodCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(archive.IsSuccess);
        Assert.True(reactivate.IsSuccess);
    }

    [Fact]
    public async Task Create_MissingTenant_FailsClosedBeforeStore()
    {
        var store = new FakeFoodStore();

        var result = await CreateHandler(store, null).HandleAsync(
            ValidCreate(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(0, store.AddCalls);
    }

    private static CreateFoodHandler CreateHandler(FakeFoodStore store, Guid? trainerId) => new(
        new CreateFoodCommandValidator(),
        new TenantContextStub(trainerId),
        new ClockStub(Now),
        store);

    private static CreateFoodCommand ValidCreate() =>
        new("Rice", null, 2.7m, 28m, 0.3m, 0.4m);

    private static UpdateFoodCommand ValidUpdate() =>
        new(Guid.NewGuid(), "Rice", null, 2.7m, 28m, 0.3m, 0.4m);

    private sealed class FakeFoodStore : IFoodStore
    {
        public FoodStoreResult UpdateResult { get; init; } = FoodStoreResult.ForNotFound();
        public FoodStoreResult ActiveResult { get; init; } = FoodStoreResult.ForChanged();
        public Food? AddedFood { get; private set; }
        public int AddCalls { get; private set; }
        public int ReadCalls { get; private set; }

        public Task AddAsync(Food food, CancellationToken cancellationToken)
        {
            AddCalls++;
            AddedFood = food;
            return Task.CompletedTask;
        }

        public Task<Food?> GetOwnedForReadAsync(
            Guid foodId,
            CancellationToken cancellationToken)
        {
            ReadCalls++;
            return Task.FromResult(AddedFood);
        }

        public Task<FoodStoreResult> UpdateAsync(
            Guid foodId,
            Guid trainerId,
            string name,
            string? description,
            decimal protein,
            decimal carbs,
            decimal fats,
            decimal? fiber,
            DateTime now,
            CancellationToken cancellationToken) => Task.FromResult(UpdateResult);

        public Task<FoodStoreResult> SetActiveAsync(
            Guid foodId,
            Guid trainerId,
            bool isActive,
            DateTime now,
            CancellationToken cancellationToken) => Task.FromResult(ActiveResult);
    }

    private sealed class ClockStub(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class TenantContextStub(Guid? trainerId) : ITenantContext
    {
        public Guid? TrainerId { get; } = trainerId;
        public Guid? UserId => null;
        public string? Role => "trainer";
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }
}
