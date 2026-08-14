using Application.Common.Abstractions;
using Application.Features.Training.TrainingPlans;
using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Features.Training.TrainingPlans.CreateTrainingPlan;
using Application.Features.Training.TrainingPlans.Dtos;
using Application.Features.Training.TrainingPlans.UpdateTrainingPlanStructure;
using Application.Pagination;

namespace Application.UnitTests.Features.Training;

public sealed class TrainingHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TrainingPlanStoreResult.Status.ClientNotFound,
        "training_client_not_found")]
    [InlineData(TrainingPlanStoreResult.Status.ExerciseReferenceNotFound,
        "training_exercise_reference_not_found")]
    [InlineData(TrainingPlanStoreResult.Status.ExerciseReferenceInactive,
        "training_exercise_reference_inactive")]
    [InlineData(TrainingPlanStoreResult.Status.ActivePlanConflict,
        "active_training_plan_conflict")]
    public async Task Create_ExpectedFailure_MapsStableError(
        TrainingPlanStoreResult.Status status,
        string expectedCode)
    {
        var store = new FakeTrainingPlanStore
        {
            CreateResult = Failure(status)
        };
        var queries = new FakeTrainingPlanQueries();
        var handler = new CreateTrainingPlanHandler(
            new CreateTrainingPlanCommandValidator(),
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store,
            queries);

        var result = await handler.HandleAsync(
            new CreateTrainingPlanCommand(
                Guid.NewGuid(),
                "Plan",
                null,
                null,
                null,
                new DateOnly(2026, 8, 1),
                null,
                EmptyNewStructure()),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(0, queries.DetailsCalls);
    }

    [Theory]
    [InlineData(TrainingPlanStoreResult.Status.StructureHasHistory,
        "training_structure_has_history")]
    [InlineData(TrainingPlanStoreResult.Status.StructureReferenceNotFound,
        "training_structure_reference_not_found")]
    [InlineData(TrainingPlanStoreResult.Status.StructureReorderRequiresFreeSlot,
        "training_structure_reorder_requires_free_slot")]
    public async Task UpdateStructure_ExpectedFailure_MapsStableError(
        TrainingPlanStoreResult.Status status,
        string expectedCode)
    {
        var store = new FakeTrainingPlanStore
        {
            StructureResult = Failure(status)
        };
        var queries = new FakeTrainingPlanQueries();
        var handler = new UpdateTrainingPlanStructureHandler(
            new UpdateTrainingPlanStructureCommandValidator(),
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store,
            queries);

        var result = await handler.HandleAsync(
            new UpdateTrainingPlanStructureCommand(Guid.NewGuid(), new([])),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(0, queries.DetailsCalls);
    }

    private static TrainingPlanStructureInput EmptyNewStructure() => new([]);

    private static TrainingPlanStoreResult Failure(
        TrainingPlanStoreResult.Status status) => status switch
        {
            TrainingPlanStoreResult.Status.ClientNotFound =>
                TrainingPlanStoreResult.ForClientNotFound(),
            TrainingPlanStoreResult.Status.ExerciseReferenceNotFound =>
                TrainingPlanStoreResult.ForExerciseReferenceNotFound(),
            TrainingPlanStoreResult.Status.ExerciseReferenceInactive =>
                TrainingPlanStoreResult.ForExerciseReferenceInactive(),
            TrainingPlanStoreResult.Status.ActivePlanConflict =>
                TrainingPlanStoreResult.ForActivePlanConflict(),
            TrainingPlanStoreResult.Status.StructureHasHistory =>
                TrainingPlanStoreResult.ForStructureHasHistory(),
            TrainingPlanStoreResult.Status.StructureReferenceNotFound =>
                TrainingPlanStoreResult.ForStructureReferenceNotFound(),
            TrainingPlanStoreResult.Status.StructureReorderRequiresFreeSlot =>
                TrainingPlanStoreResult.ForStructureReorderRequiresFreeSlot(),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

    private sealed class FakeTrainingPlanStore : ITrainingPlanStore
    {
        public TrainingPlanStoreResult CreateResult { get; init; } =
            TrainingPlanStoreResult.ForNotFound();
        public TrainingPlanStoreResult StructureResult { get; init; } =
            TrainingPlanStoreResult.ForNotFound();

        public Task<TrainingPlanStoreResult> CreateAsync(
            Guid trainerId,
            CreateTrainingPlanWriteModel model,
            DateTime now,
            CancellationToken cancellationToken) => Task.FromResult(CreateResult);

        public Task<TrainingPlanStoreResult> UpdateMetadataAsync(
            Guid trainerId,
            UpdateTrainingPlanMetadataWriteModel model,
            DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult(TrainingPlanStoreResult.ForNotFound());

        public Task<TrainingPlanStoreResult> UpdateStructureAsync(
            Guid trainerId,
            UpdateTrainingPlanStructureWriteModel model,
            DateTime now,
            CancellationToken cancellationToken) => Task.FromResult(StructureResult);

        public Task<TrainingPlanStoreResult> ArchiveAsync(
            Guid trainingPlanId,
            Guid trainerId,
            DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult(TrainingPlanStoreResult.ForNotFound());

        public Task<TrainingPlanStoreResult> ReplaceAsync(
            Guid trainerId,
            ReplaceTrainingPlanWriteModel model,
            DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult(TrainingPlanStoreResult.ForNotFound());
    }

    private sealed class FakeTrainingPlanQueries : ITrainingPlanQueries
    {
        public int DetailsCalls { get; private set; }

        public Task<TrainingPlanDetailsDto?> GetDetailsAsync(
            Guid trainingPlanId,
            CancellationToken cancellationToken)
        {
            DetailsCalls++;
            return Task.FromResult<TrainingPlanDetailsDto?>(null);
        }

        public Task<PageResult<TrainingPlanSummaryDto>> ListAsync(
            Guid? clientId,
            string? search,
            Application.Features.Training.TrainingPlans.ListTrainingPlans
                .TrainingPlanActivityFilter activity,
            PageRequest page,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PageResult<TrainingPlanSummaryDto>([], 0));
    }

    private sealed class ClockStub(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class TenantStub(Guid trainerId) : ITenantContext
    {
        public Guid? TrainerId { get; } = trainerId;
        public Guid? UserId => null;
        public string? Role => "trainer";
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }
}
