using Application.Pagination;
using Application.Common.Abstractions;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.Assessments.InitialAssessments.CreateInitialAssessment;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Application.Features.Assessments.InitialAssessments.GetInitialAssessment;
using Application.Features.Assessments.InitialAssessments.MarkInitialAssessmentReviewed;
using Application.Features.Assessments.InitialAssessments.UpdateInitialAssessment;
using Application.Features.ClientPortal.GetMyInitialAssessment;
using Application.Features.ClientPortal.SubmitMyInitialAssessment;
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

    [Fact]
    public async Task Submit_Client_WritesForTheAuthenticatedAccount()
    {
        var store = new StoreStub();
        var tenant = new TenantStub(TrainerId, "client");
        var handler = SubmitHandler(tenant, store);

        var result = await handler.HandleAsync(ValidSubmit(), TestContext.Current.CancellationToken);

        Assert.Equal(
            (true, 1, TrainerId, tenant.UserId!.Value),
            (result.IsSuccess, store.SubmitCalls, store.TrainerId, store.ClientUserId));
    }

    [Theory]
    [InlineData("trainer")]
    [InlineData("superuser")]
    public async Task Submit_NotClient_ReturnsForbiddenWithoutWrite(string role)
    {
        var store = new StoreStub();
        var handler = SubmitHandler(new TenantStub(TrainerId, role), store);

        var result = await handler.HandleAsync(ValidSubmit(), TestContext.Current.CancellationToken);

        Assert.Equal(("portal_client_only", 0), (result.Error!.Code, store.SubmitCalls));
    }

    [Fact]
    public async Task Submit_NullCharacterInText_ReturnsValidationWithoutWrite()
    {
        var store = new StoreStub();
        var handler = SubmitHandler(new TenantStub(TrainerId, "client"), store);

        var result = await handler.HandleAsync(
            ValidSubmit() with { Goals = "for" + '\0' + "ça" },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ("validation_failed", "text_invalid_character", 0),
            (result.Error!.Code, result.Error.ValidationErrors.Single().Code, store.SubmitCalls));
    }

    [Theory]
    [InlineData(InitialAssessmentStoreResult.Status.ClientNotFound, "portal_profile_not_available")]
    [InlineData(InitialAssessmentStoreResult.Status.AssessmentAlreadyExists, "initial_assessment_already_exists")]
    public async Task Submit_StoreRefusal_MapsToPortalError(
        InitialAssessmentStoreResult.Status status,
        string code)
    {
        var store = new StoreStub { Outcome = InitialAssessmentStoreResult.For(status) };
        var handler = SubmitHandler(new TenantStub(TrainerId, "client"), store);

        var result = await handler.HandleAsync(ValidSubmit(), TestContext.Current.CancellationToken);

        Assert.Equal(code, result.Error!.Code);
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1000.01, false)]
    [InlineData(80.005, false)]
    public async Task Submit_MeasurementLimit_IsEnforcedOnTheServer(double waistCm, bool accepted)
    {
        var store = new StoreStub();
        var handler = SubmitHandler(new TenantStub(TrainerId, "client"), store);

        var result = await handler.HandleAsync(
            ValidSubmit() with
            {
                BodyMeasurements = new(
                    (decimal)waistCm, null, null, null, null, null, null, null, null)
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(accepted, result.IsSuccess);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(50.01, false)]
    public async Task Submit_WaterLimit_IsEnforcedOnTheServer(double liters, bool accepted)
    {
        var store = new StoreStub();
        var handler = SubmitHandler(new TenantStub(TrainerId, "client"), store);

        var result = await handler.HandleAsync(
            ValidSubmit() with
            {
                NutritionIntake = new(
                    null, null, null, null, null, null, null, null, null,
                    (decimal)liters, null, null, null, null)
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(accepted, result.IsSuccess);
    }

    [Theory]
    [InlineData(null, false, "portal_profile_not_available")]
    [InlineData(true, true, null)]
    [InlineData(false, true, null)]
    public async Task Status_ReadsPendingForTheAccount(bool? pending, bool success, string? code)
    {
        var handler = new GetMyInitialAssessmentStatusHandler(
            new TenantStub(TrainerId, "client"),
            new QueryStub { Pending = pending });

        var result = await handler.HandleAsync(
            new GetMyInitialAssessmentStatusQuery(),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            (success, code, success ? pending : null),
            (result.IsSuccess, result.Error?.Code, success ? result.Value.Pending : (bool?)null));
    }

    [Fact]
    public async Task Review_NotSubmittedByClient_MapsConflict()
    {
        var store = new StoreStub
        {
            Outcome = InitialAssessmentStoreResult.For(InitialAssessmentStoreResult.Status.NotReviewable)
        };
        var handler = new MarkInitialAssessmentReviewedHandler(
            new TenantStub(TrainerId, "trainer"), new ClockStub(), store);

        var result = await handler.HandleAsync(
            new MarkInitialAssessmentReviewedCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal("initial_assessment_not_reviewable", result.Error!.Code);
    }

    [Fact]
    public async Task Review_Client_ReturnsForbiddenWithoutWrite()
    {
        var store = new StoreStub();
        var handler = new MarkInitialAssessmentReviewedHandler(
            new TenantStub(TrainerId, "client"), new ClockStub(), store);

        var result = await handler.HandleAsync(
            new MarkInitialAssessmentReviewedCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal(("assessment_trainer_only", 0), (result.Error!.Code, store.ReviewCalls));
    }

    private static SubmitMyInitialAssessmentHandler SubmitHandler(TenantStub tenant, StoreStub store) =>
        new(new SubmitMyInitialAssessmentCommandValidator(), tenant, new ClockStub(), store);

    private static SubmitMyInitialAssessmentCommand ValidSubmit() => new(
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

        public int SubmitCalls { get; private set; }
        public Guid ClientUserId { get; private set; }

        public Task<InitialAssessmentStoreResult> SubmitByClientAsync(
            Guid trainerId,
            Guid clientUserId,
            InitialAssessmentWriteValues values,
            DateTime now,
            CancellationToken cancellationToken)
        {
            SubmitCalls++;
            TrainerId = trainerId;
            ClientUserId = clientUserId;
            return Task.FromResult(Outcome ?? InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.Created,
                CreateAssessment()));
        }

        public int ReviewCalls { get; private set; }

        public Task<InitialAssessmentStoreResult> MarkReviewedAsync(
            Guid trainerId,
            Guid assessmentId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            ReviewCalls++;
            TrainerId = trainerId;
            return Task.FromResult(Outcome ?? InitialAssessmentStoreResult.For(
                InitialAssessmentStoreResult.Status.Updated,
                CreateAssessment()));
        }
    }

    private sealed class QueryStub : IInitialAssessmentQueries
    {
        public Task<InitialAssessmentDto?> GetByClientAsync(
            Guid trainerId,
            Guid clientId,
            CancellationToken cancellationToken) =>
            Task.FromResult<InitialAssessmentDto?>(null);

        public bool? Pending { get; init; }

        public Task<bool?> IsPendingForClientUserAsync(
            Guid trainerId,
            Guid clientUserId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Pending);

        public Task<PageResult<InitialAssessmentPendingReviewDto>> ListPendingReviewAsync(
            Guid trainerId,
            PageRequest page,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PageResult<InitialAssessmentPendingReviewDto>([], 0));
    }
}
