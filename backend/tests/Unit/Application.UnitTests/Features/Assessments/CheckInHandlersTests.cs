using Application.Common.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.CancelCheckIn;
using Application.Features.Assessments.CheckIns.CorrectCheckIn;
using Application.Features.Assessments.CheckIns.CreateCheckIn;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Features.Assessments.CheckIns.GetCheckIn;
using Application.Features.Assessments.CheckIns.GetMyDueCheckIn;
using Application.Features.Assessments.CheckIns.ListCheckIns;
using Application.Features.Assessments.CheckIns.RescheduleCheckIn;
using Application.Features.Assessments.CheckIns.SubmitCheckInResponse;
using Application.Pagination;
using Domain.Entities.Assessments;
using Domain.ValueObjects;

namespace Application.UnitTests.Features.Assessments;

public sealed class CheckInHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CheckInId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 8, 17);

    [Fact]
    public async Task Create_Trainer_UsesEffectiveTenant()
    {
        var store = new StoreStub();
        var handler = new CreateCheckInHandler(
            new CreateCheckInCommandValidator(),
            TrainerTenant(),
            new ClockStub(),
            new TimeZoneStub(),
            store);

        var result = await handler.HandleAsync(
            new CreateCheckInCommand(ClientId, Today, null),
            TestContext.Current.CancellationToken);

        Assert.Equal((true, TrainerId), (result.IsSuccess, store.TrainerId));
    }

    [Fact]
    public async Task Get_MissingCheckIn_ReturnsNotFound()
    {
        var handler = new GetCheckInHandler(
            TrainerTenant(),
            new ClockStub(),
            new TimeZoneStub(),
            new QueryStub());

        var result = await handler.HandleAsync(
            new GetCheckInQuery(CheckInId),
            TestContext.Current.CancellationToken);

        Assert.Equal("check_in_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task List_PropagatesFiltersAndPagination()
    {
        var queries = new QueryStub();
        var handler = new ListCheckInsHandler(
            new ListCheckInsQueryValidator(),
            TrainerTenant(),
            new ClockStub(),
            new TimeZoneStub(),
            queries);
        var query = new ListCheckInsQuery(
            ClientId,
            CheckInStatusFilter.Missed,
            Today.AddDays(-7),
            Today,
            2,
            25);

        var result = await handler.HandleAsync(
            query,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            (true, ClientId, CheckInStatusFilter.Missed, new PageRequest(2, 25)),
            (result.IsSuccess, queries.ClientId, queries.Status, queries.Page));
    }

    [Fact]
    public async Task Reschedule_ClosedCheckIn_MapsConflict()
    {
        var store = new StoreStub
        {
            Outcome = CheckInStoreResult.For(
                CheckInStoreResult.Status.CannotReschedule)
        };
        var handler = new RescheduleCheckInHandler(
            new RescheduleCheckInCommandValidator(),
            TrainerTenant(),
            new ClockStub(),
            new TimeZoneStub(),
            store);

        var result = await handler.HandleAsync(
            new RescheduleCheckInCommand(
                CheckInId,
                Today.AddDays(1),
                null),
            TestContext.Current.CancellationToken);

        Assert.Equal("check_in_cannot_be_rescheduled", result.Error!.Code);
    }

    [Fact]
    public async Task Cancel_NonTrainer_ReturnsForbiddenWithoutWrite()
    {
        var store = new StoreStub();
        var handler = new CancelCheckInHandler(
            new TenantStub(TrainerId, UserId, "client"),
            new ClockStub(),
            new TimeZoneStub(),
            store);

        var result = await handler.HandleAsync(
            new CancelCheckInCommand(CheckInId),
            TestContext.Current.CancellationToken);

        Assert.Equal(("assessment_trainer_only", 0),
            (result.Error!.Code, store.Calls));
    }

    [Fact]
    public async Task Correct_UnansweredCheckIn_MapsConflict()
    {
        var store = new StoreStub
        {
            Outcome = CheckInStoreResult.For(CheckInStoreResult.Status.NotAnswered)
        };
        var handler = new CorrectCheckInHandler(
            new CorrectCheckInCommandValidator(),
            TrainerTenant(),
            new ClockStub(),
            new TimeZoneStub(),
            store);

        var result = await handler.HandleAsync(
            new CorrectCheckInCommand(
                CheckInId,
                null,
                80m,
                null,
                null,
                null,
                null,
                null,
                null),
            TestContext.Current.CancellationToken);

        Assert.Equal("check_in_not_answered", result.Error!.Code);
    }

    [Fact]
    public async Task GetMyDue_ClientWithoutDueCheckIn_ReturnsSuccessfulNull()
    {
        var queries = new QueryStub();
        var handler = new GetMyDueCheckInHandler(
            ClientTenant(),
            new ClockStub(),
            new TimeZoneStub(),
            queries);

        var result = await handler.HandleAsync(
            new GetMyDueCheckInQuery(),
            TestContext.Current.CancellationToken);

        Assert.Equal((true, null, UserId),
            (result.IsSuccess, result.Value, queries.UserId));
    }

    [Fact]
    public async Task GetMyDue_Trainer_ReturnsForbiddenWithoutQuery()
    {
        var queries = new QueryStub();
        var handler = new GetMyDueCheckInHandler(
            TrainerTenant(),
            new ClockStub(),
            new TimeZoneStub(),
            queries);

        var result = await handler.HandleAsync(
            new GetMyDueCheckInQuery(),
            TestContext.Current.CancellationToken);

        Assert.Equal(("check_in_client_only", 0),
            (result.Error!.Code, queries.DueCalls));
    }

    [Fact]
    public async Task SubmitResponse_Client_UsesAuthenticatedIdentity()
    {
        var checkIn = CreateCheckIn();
        var store = new StoreStub
        {
            Outcome = CheckInStoreResult.For(
                CheckInStoreResult.Status.AlreadyInRequestedState,
                checkIn)
        };
        var handler = new SubmitCheckInResponseHandler(
            new SubmitCheckInResponseCommandValidator(),
            ClientTenant(),
            new ClockStub(),
            new TimeZoneStub(),
            store);

        var result = await handler.HandleAsync(
            ValidSubmit(checkIn.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            (true, TrainerId, UserId),
            (result.IsSuccess, store.TrainerId, store.UserId));
    }

    [Fact]
    public async Task SubmitResponse_Trainer_ReturnsForbiddenWithoutWrite()
    {
        var store = new StoreStub();
        var handler = new SubmitCheckInResponseHandler(
            new SubmitCheckInResponseCommandValidator(),
            TrainerTenant(),
            new ClockStub(),
            new TimeZoneStub(),
            store);

        var result = await handler.HandleAsync(
            ValidSubmit(CheckInId),
            TestContext.Current.CancellationToken);

        Assert.Equal(("check_in_client_only", 0),
            (result.Error!.Code, store.Calls));
    }

    private static ITenantContext TrainerTenant() =>
        new TenantStub(TrainerId, UserId, "trainer");

    private static ITenantContext ClientTenant() =>
        new TenantStub(TrainerId, UserId, "client");

    private static SubmitCheckInResponseCommand ValidSubmit(Guid checkInId) => new(
        checkInId,
        80m,
        null,
        null,
        null,
        null,
        null,
        null);

    private static CheckIn CreateCheckIn() =>
        new(TrainerId, ClientId, Today, null, Now);

    private sealed class ClockStub : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class TimeZoneStub : ITrainerTimeZoneProvider
    {
        public Task<TimeZoneInfo> GetRequiredAsync(
            Guid trainerId,
            CancellationToken cancellationToken) =>
            Task.FromResult(TimeZoneInfo.Utc);
    }

    private sealed class TenantStub(
        Guid? trainerId,
        Guid? userId,
        string? role) : ITenantContext
    {
        public Guid? TrainerId { get; } = trainerId;
        public Guid? UserId { get; } = userId;
        public string? Role { get; } = role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class StoreStub : ICheckInStore
    {
        public CheckInStoreResult? Outcome { get; init; }
        public int Calls { get; private set; }
        public Guid TrainerId { get; private set; }
        public Guid UserId { get; private set; }

        public Task<CheckInStoreResult> CreateAsync(
            Guid trainerId,
            Guid clientId,
            DateOnly checkInDate,
            DateOnly? targetDate,
            DateTime now,
            CancellationToken cancellationToken)
        {
            RecordCall(trainerId);
            return ResultOrCreated();
        }

        public Task<CheckInStoreResult> RescheduleAsync(
            Guid trainerId,
            Guid checkInId,
            DateOnly checkInDate,
            DateOnly? targetDate,
            DateTime now,
            CancellationToken cancellationToken)
        {
            RecordCall(trainerId);
            return ResultOrCreated();
        }

        public Task<CheckInStoreResult> CancelAsync(
            Guid trainerId,
            Guid checkInId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            RecordCall(trainerId);
            return ResultOrCreated();
        }

        public Task<CheckInStoreResult> SubmitResponseAsync(
            Guid trainerId,
            Guid userId,
            Guid checkInId,
            decimal weightKg,
            decimal? bodyFatPercentage,
            string? notes,
            BodyMeasurements bodyMeasurements,
            CheckInFeedback feedback,
            int? trainingAdherenceScore,
            int? nutritionAdherenceScore,
            DateTime now,
            CancellationToken cancellationToken)
        {
            RecordCall(trainerId);
            UserId = userId;
            return ResultOrCreated();
        }

        public Task<CheckInStoreResult> CorrectAsync(
            Guid trainerId,
            Guid checkInId,
            DateOnly? targetDate,
            decimal weightKg,
            decimal? bodyFatPercentage,
            string? notes,
            BodyMeasurements bodyMeasurements,
            CheckInFeedback feedback,
            int? trainingAdherenceScore,
            int? nutritionAdherenceScore,
            DateTime now,
            CancellationToken cancellationToken)
        {
            RecordCall(trainerId);
            return ResultOrCreated();
        }

        private void RecordCall(Guid trainerId)
        {
            Calls++;
            TrainerId = trainerId;
        }

        private Task<CheckInStoreResult> ResultOrCreated() =>
            Task.FromResult(Outcome ?? CheckInStoreResult.For(
                CheckInStoreResult.Status.Created,
                CreateCheckIn()));
    }

    private sealed class QueryStub : ICheckInQueries
    {
        public int DueCalls { get; private set; }
        public Guid UserId { get; private set; }
        public Guid? ClientId { get; private set; }
        public CheckInStatusFilter? Status { get; private set; }
        public PageRequest? Page { get; private set; }

        public Task<CheckInDto?> GetAsync(
            Guid trainerId,
            Guid checkInId,
            DateOnly localToday,
            CancellationToken cancellationToken) =>
            Task.FromResult<CheckInDto?>(null);

        public Task<PageResult<CheckInDto>> ListAsync(
            Guid trainerId,
            Guid? clientId,
            CheckInStatusFilter? status,
            DateOnly? fromDate,
            DateOnly? toDate,
            DateOnly localToday,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            ClientId = clientId;
            Status = status;
            Page = page;
            return Task.FromResult(new PageResult<CheckInDto>([], 0));
        }

        public Task<CheckInDto?> GetMyDueAsync(
            Guid trainerId,
            Guid userId,
            DateOnly localToday,
            CancellationToken cancellationToken)
        {
            DueCalls++;
            UserId = userId;
            return Task.FromResult<CheckInDto?>(null);
        }
    }
}
