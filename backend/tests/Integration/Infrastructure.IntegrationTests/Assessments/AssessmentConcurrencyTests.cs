using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Domain.Entities.Assessments;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Assessments;
using Infrastructure.Persistence.Errors;
using Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Assessments;

[Collection(PostgresCollection.Name)]
public sealed class AssessmentConcurrencyTests
{
    private static readonly DateTime Now =
        new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public AssessmentConcurrencyTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task InitialAssessment_CreateConcurrent_CreatesExactlyOne()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync("initial-create", token);
        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        await using var secondContext = _fixture.CreateContext(seed.TrainerId);

        var results = await Task.WhenAll(
            CreateInitialAssessmentAsync(CreateInitialStore(firstContext), seed, token),
            CreateInitialAssessmentAsync(CreateInitialStore(secondContext), seed, token));

        Assert.Equal(
            [InitialAssessmentStoreResult.Status.Created,
                InitialAssessmentStoreResult.Status.AssessmentAlreadyExists],
            results.Select(result => result.Kind).Order().ToArray());
    }

    [Fact]
    public async Task CheckIn_CreateConcurrentForSameDate_ReturnsTypedConflict()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync("checkin-create", token);
        var checkInDate = DateOnly.FromDateTime(Now).AddDays(1);
        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        await using var secondContext = _fixture.CreateContext(seed.TrainerId);

        var results = await Task.WhenAll(
            CreateCheckInStore(firstContext).CreateAsync(
                seed.TrainerId, seed.ClientId, checkInDate, null, Now, token),
            CreateCheckInStore(secondContext).CreateAsync(
                seed.TrainerId, seed.ClientId, checkInDate, null, Now, token));

        Assert.Equal(
            [CheckInStoreResult.Status.Created, CheckInStoreResult.Status.DateConflict],
            results.Select(result => result.Kind).Order().ToArray());
    }

    [Fact]
    public async Task CheckIn_SubmitSameResponseConcurrent_IsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedCheckInAsync("response-concurrent", token);
        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        await using var secondContext = _fixture.CreateContext(seed.TrainerId);

        var results = await Task.WhenAll(
            SubmitResponseAsync(CreateCheckInStore(firstContext), seed, token),
            SubmitResponseAsync(CreateCheckInStore(secondContext), seed, token));

        Assert.Equal(
            [CheckInStoreResult.Status.Answered,
                CheckInStoreResult.Status.AlreadyInRequestedState],
            results.Select(result => result.Kind).Order().ToArray());
    }

    [Fact]
    public async Task CheckIn_SubmitSameResponseAgain_ReturnsAlreadyRequestedState()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedCheckInAsync("response-retry", token);
        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        var first = await SubmitResponseAsync(
            CreateCheckInStore(firstContext), seed, token);
        await using var retryContext = _fixture.CreateContext(seed.TrainerId);

        var retry = await SubmitResponseAsync(
            CreateCheckInStore(retryContext), seed, token);

        Assert.Equal(CheckInStoreResult.Status.Answered, first.Kind);
        Assert.Equal(CheckInStoreResult.Status.AlreadyInRequestedState, retry.Kind);
    }

    [Fact]
    public async Task CheckIn_SubmitResponseAndCancelConcurrent_ProducesSafeTerminalState()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedCheckInAsync("response-cancel", token);
        await using var responseContext = _fixture.CreateContext(seed.TrainerId);
        await using var cancelContext = _fixture.CreateContext(seed.TrainerId);

        var responseTask = SubmitResponseAsync(
            CreateCheckInStore(responseContext), seed, token);
        var cancelTask = CreateCheckInStore(cancelContext).CancelAsync(
            seed.TrainerId, seed.CheckInId, Now, token);
        await Task.WhenAll(responseTask, cancelTask);
        var response = await responseTask;
        var cancellation = await cancelTask;

        Assert.Equal(CheckInStoreResult.Status.Answered, response.Kind);
        Assert.Equal(CheckInStoreResult.Status.CannotCancel, cancellation.Kind);

        await using var verification = _fixture.CreateContext(seed.TrainerId);
        var persisted = await verification.CheckIns
            .IgnoreQueryFilters()
            .SingleAsync(checkIn => checkIn.Id == seed.CheckInId, token);
        Assert.NotNull(persisted.RespondedAt);
        Assert.Null(persisted.CancelledAt);
    }

    [Fact]
    public async Task CheckIn_RescheduleConcurrentToSameDate_RollsBackLosingWrite()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedTwoFutureCheckInsAsync(token);
        var targetDate = DateOnly.FromDateTime(Now).AddDays(10);
        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        await using var secondContext = _fixture.CreateContext(seed.TrainerId);

        var results = await Task.WhenAll(
            CreateCheckInStore(firstContext).RescheduleAsync(
                seed.TrainerId, seed.FirstCheckInId, targetDate, null, Now, token),
            CreateCheckInStore(secondContext).RescheduleAsync(
                seed.TrainerId, seed.SecondCheckInId, targetDate, null, Now, token));

        Assert.Equal(
            [CheckInStoreResult.Status.Rescheduled, CheckInStoreResult.Status.DateConflict],
            results.Select(result => result.Kind).Order().ToArray());

        await using var verification = _fixture.CreateContext(seed.TrainerId);
        var dates = await verification.CheckIns
            .Where(checkIn => checkIn.Id == seed.FirstCheckInId ||
                checkIn.Id == seed.SecondCheckInId)
            .Select(checkIn => checkIn.CheckInDate)
            .Order()
            .ToArrayAsync(token);
        Assert.Contains(targetDate, dates);
        Assert.Single(dates, date => date != targetDate);
    }

    [Fact]
    public async Task Operations_OtherTenant_ReturnSafeNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var owner = await SeedCheckInAsync("cross-owner", token);
        var requester = await SeedAsync("cross-requester", token);
        await using var context = _fixture.CreateContext(requester.TrainerId);

        var assessment = await CreateInitialStore(context).CreateAsync(
            requester.TrainerId,
            owner.ClientId,
            80m,
            180,
            20m,
            null,
            "beginner",
            ActivityLevel.LightlyActive,
            "Improve health",
            null,
            BodyMeasurements.Empty,
            NutritionIntake.Empty,
            Now,
            token);
        var checkIn = await CreateCheckInStore(context).CancelAsync(
            requester.TrainerId, owner.CheckInId, Now, token);

        Assert.Equal(InitialAssessmentStoreResult.Status.ClientNotFound, assessment.Kind);
        Assert.Equal(CheckInStoreResult.Status.CheckInNotFound, checkIn.Kind);
    }

    [Fact]
    public async Task Create_WhenClientIsArchived_ReturnsClientInactiveWithoutWrites()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync("archived", token);
        await using (var setup = _fixture.CreateContext(seed.TrainerId))
        {
            var client = await setup.Clients.SingleAsync(
                item => item.Id == seed.ClientId, token);
            client.Deactivate(Now);
            await setup.SaveChangesAsync(token);
        }
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var assessment = await CreateInitialAssessmentAsync(
            CreateInitialStore(context), seed, token);
        var checkIn = await CreateCheckInStore(context).CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            DateOnly.FromDateTime(Now).AddDays(1),
            null,
            Now,
            token);

        Assert.Equal(InitialAssessmentStoreResult.Status.ClientInactive, assessment.Kind);
        Assert.Equal(CheckInStoreResult.Status.ClientInactive, checkIn.Kind);
        Assert.False(await context.InitialAssessments.AnyAsync(token));
        Assert.False(await context.CheckIns.AnyAsync(token));
    }

    private async Task<AssessmentSeed> SeedAsync(
        string prefix,
        CancellationToken cancellationToken)
    {
        var seed = await _fixture.SeedTenantWithClientAsync(
            $"assessment-{prefix}-{Guid.NewGuid():N}",
            cancellationToken);
        return new AssessmentSeed(seed.TrainerId, seed.ClientId, seed.ClientUserId);
    }

    private async Task<CheckInSeed> SeedCheckInAsync(
        string prefix,
        CancellationToken cancellationToken)
    {
        var seed = await SeedAsync(prefix, cancellationToken);
        var checkIn = new CheckIn(
            seed.TrainerId,
            seed.ClientId,
            DateOnly.FromDateTime(Now),
            null,
            Now);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        context.CheckIns.Add(checkIn);
        await context.SaveChangesAsync(cancellationToken);
        return new CheckInSeed(
            seed.TrainerId,
            seed.ClientId,
            seed.ClientUserId,
            checkIn.Id);
    }

    private async Task<TwoCheckInsSeed> SeedTwoFutureCheckInsAsync(
        CancellationToken cancellationToken)
    {
        var seed = await SeedAsync("reschedule", cancellationToken);
        var first = new CheckIn(
            seed.TrainerId,
            seed.ClientId,
            DateOnly.FromDateTime(Now).AddDays(2),
            null,
            Now);
        var second = new CheckIn(
            seed.TrainerId,
            seed.ClientId,
            DateOnly.FromDateTime(Now).AddDays(3),
            null,
            Now);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        context.CheckIns.AddRange(first, second);
        await context.SaveChangesAsync(cancellationToken);
        return new TwoCheckInsSeed(seed.TrainerId, first.Id, second.Id);
    }

    private static InitialAssessmentStore CreateInitialStore(PtManagerDbContext context) =>
        new(context, new PostgresConstraintTranslator());

    private static CheckInStore CreateCheckInStore(PtManagerDbContext context) =>
        new(
            context,
            new TrainerTimeZoneProvider(context),
            new PostgresConstraintTranslator());

    private static Task<InitialAssessmentStoreResult> CreateInitialAssessmentAsync(
        InitialAssessmentStore store,
        AssessmentSeed seed,
        CancellationToken cancellationToken) => store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            80m,
            180,
            20m,
            null,
            "beginner",
            ActivityLevel.LightlyActive,
            "Improve health",
            null,
            BodyMeasurements.Empty,
            NutritionIntake.Empty,
            Now,
            cancellationToken);

    private static Task<CheckInStoreResult> SubmitResponseAsync(
        CheckInStore store,
        CheckInSeed seed,
        CancellationToken cancellationToken) => store.SubmitResponseAsync(
            seed.TrainerId,
            seed.ClientUserId,
            seed.CheckInId,
            79m,
            19m,
            "On track",
            BodyMeasurements.Empty,
            CheckInFeedback.Empty,
            90,
            85,
            Now,
            cancellationToken);

    private record AssessmentSeed(Guid TrainerId, Guid ClientId, Guid ClientUserId);

    private sealed record CheckInSeed(
        Guid TrainerId,
        Guid ClientId,
        Guid ClientUserId,
        Guid CheckInId) : AssessmentSeed(TrainerId, ClientId, ClientUserId);

    private sealed record TwoCheckInsSeed(
        Guid TrainerId,
        Guid FirstCheckInId,
        Guid SecondCheckInId);
}
