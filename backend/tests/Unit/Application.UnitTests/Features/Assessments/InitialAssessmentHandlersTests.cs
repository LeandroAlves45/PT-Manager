using Application.Common.Abstractions;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.Assessments.InitialAssessments.CreateInitialAssessment;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Application.Features.Assessments.InitialAssessments.GetInitialAssessment;
using Application.Features.Assessments.InitialAssessments.UpdateInitialAssessment;
using Domain.Entities.Assessments;
using Domain.ValueObjects;

namespace Application.UnitTests.Features.Assessments;

public sealed class InitialAssessmentHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_Trainer_UsesEffectiveTenantAndCancellationToken()
    {
        var store = new StoreStub();
        using var cancellation = new CancellationTokenSource();
        var handler = new CreateInitialAssessmentHandler(
            new CreateInitialAssessmentCommandValidator(),
            new TenantStub(TrainerId, "trainer"),
            new ClockStub(),
            store);

        var result = await handler.HandleAsync(
            ValidCreate(),
            cancellation.Token);

        Assert.Equal(
            (true, TrainerId, cancellation.Token),
            (result.IsSuccess, store.TrainerId, store.CancellationToken));
    }

    [Theory]
    [InlineData("client")]
    [InlineData("superuser")]
    public async Task Create_NonTrainer_ReturnsForbiddenWithoutWrite(string role)
    {
        var store = new StoreStub();
        var handler = new CreateInitialAssessmentHandler(
            new CreateInitialAssessmentCommandValidator(),
            new TenantStub(TrainerId, role),
            new ClockStub(),
            store);

        var result = await handler.HandleAsync(
            ValidCreate(),
            TestContext.Current.CancellationToken);

        Assert.Equal(("assessment_trainer_only", 0),
            (result.Error!.Code, store.CreateCalls));
    }

    [Fact]
    public async Task Create_InvalidCommand_ReturnsValidationWithoutWrite()
    {
        var store = new StoreStub();
        var handler = new CreateInitialAssessmentHandler(
            new CreateInitialAssessmentCommandValidator(),
            new TenantStub(TrainerId, "trainer"),
            new ClockStub(),
            store);

        var result = await handler.HandleAsync(
            ValidCreate() with { WeightKg = 0m },
            TestContext.Current.CancellationToken);

        Assert.Equal(("validation_failed", 0),
            (result.Error!.Code, store.CreateCalls));
    }

    [Fact]
    public async Task Create_ExistingAssessment_MapsConflict()
    {
        var store = new StoreStub
        {
            Outcome = InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.AssessmentAlreadyExists)
        };
        var handler = new CreateInitialAssessmentHandler(
            new CreateInitialAssessmentCommandValidator(),
            new TenantStub(TrainerId, "trainer"),
            new ClockStub(),
            store);

        var result = await handler.HandleAsync(
            ValidCreate(),
            TestContext.Current.CancellationToken);

        Assert.Equal("initial_assessment_already_exists", result.Error!.Code);
    }

    [Fact]
    public async Task Get_MissingAssessment_ReturnsNotFound()
    {
        var handler = new GetInitialAssessmentHandler(
            new TenantStub(TrainerId, "trainer"),
            new QueryStub());

        var result = await handler.HandleAsync(
            new GetInitialAssessmentQuery(ClientId),
            TestContext.Current.CancellationToken);

        Assert.Equal("initial_assessment_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Update_IdempotentOutcome_ReturnsPersistedAssessment()
    {
        var assessment = CreateAssessment();
        var store = new StoreStub
        {
            Outcome = InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.AlreadyInRequestedState,
                assessment)
        };
        var handler = new UpdateInitialAssessmentHandler(
            new UpdateInitialAssessmentCommandValidator(),
            new TenantStub(TrainerId, "trainer"),
            new ClockStub(),
            store);

        var result = await handler.HandleAsync(
            ValidUpdate(assessment.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(assessment.Id, result.Value.Id);
    }

    private static CreateInitialAssessmentCommand ValidCreate() => new(
        ClientId,
        80m,
        180,
        null,
        null,
        "intermediate",
        "moderately_active",
        "strength",
        null,
        null,
        null);

    private static UpdateInitialAssessmentCommand ValidUpdate(Guid assessmentId) => new(
        assessmentId,
        80m,
        180,
        null,
        null,
        "intermediate",
        "moderately_active",
        "strength",
        null,
        null,
        null);

    private static InitialAssessment CreateAssessment() => new(
        TrainerId,
        ClientId,
        80m,
        180,
        null,
        null,
        "intermediate",
        ActivityLevel.ModeratelyActive,
        "strength",
        null,
        BodyMeasurements.Empty,
        NutritionIntake.Empty,
        Now);

    private sealed class ClockStub : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class TenantStub(Guid? trainerId, string? role) : ITenantContext
    {
        public Guid? TrainerId { get; } = trainerId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Role { get; } = role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class StoreStub : IInitialAssessmentStore
    {
        public InitialAssessmentStoreResult? Outcome { get; init; }
        public int CreateCalls { get; private set; }
        public Guid TrainerId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<InitialAssessmentStoreResult> CreateAsync(
            Guid trainerId,
            Guid clientId,
            decimal weightKg,
            int heightCm,
            decimal? bodyFatPercentage,
            string? medicalConditions,
            string fitnessLevel,
            ActivityLevel activityLevel,
            string goals,
            string? profession,
            BodyMeasurements bodyMeasurements,
            NutritionIntake nutritionIntake,
            DateTime now,
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            TrainerId = trainerId;
            CancellationToken = cancellationToken;
            return Task.FromResult(Outcome ?? InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.Created,
                CreateAssessment()));
        }

        public Task<InitialAssessmentStoreResult> UpdateAsync(
            Guid trainerId,
            Guid assessmentId,
            decimal weightKg,
            int heightCm,
            decimal? bodyFatPercentage,
            string? medicalConditions,
            string fitnessLevel,
            ActivityLevel activityLevel,
            string goals,
            string? profession,
            BodyMeasurements bodyMeasurements,
            NutritionIntake nutritionIntake,
            DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult(Outcome ?? InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.Updated,
                CreateAssessment()));
    }

    private sealed class QueryStub : IInitialAssessmentQueries
    {
        public Task<InitialAssessmentDto?> GetByClientAsync(
            Guid trainerId,
            Guid clientId,
            CancellationToken cancellationToken) =>
            Task.FromResult<InitialAssessmentDto?>(null);
    }
}
