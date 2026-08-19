using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.AssignSupplement;
using Application.Features.Supplements.CreateGlobalSupplement;
using Application.Features.Supplements.Dtos;
using Application.Features.Supplements.GetMySupplementAssignment;
using Application.Features.Supplements.ListGlobalSupplements;
using Application.Features.Supplements.ListSupplementAssignments;
using Application.Pagination;
using Domain.Entities.Supplements;

namespace Application.UnitTests.Features.Supplements;

public sealed class SupplementHandlerPolicyTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Assign_ForwardsAuthenticatedTrainerAndCancellationToken()
    {
        var trainerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new AssignSupplementCommand(
            Guid.NewGuid(), Guid.NewGuid(), null, null, null);
        var store = new RecordingAssignmentStore();
        var handler = new AssignSupplementHandler(
            new AssignSupplementCommandValidator(),
            Context("trainer", trainerId, userId), new TestClock(), store);
        using var source = new CancellationTokenSource();

        var result = await handler.HandleAsync(command, source.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal((trainerId, source.Token), (store.TrainerId, store.CancellationToken));
        Assert.Null(store.ServingSize);
        Assert.Null(store.Timing);
        Assert.Null(store.TrainerNotes);
    }

    [Fact]
    public async Task Assign_WhenCommandIsInvalid_DoesNotCallStore()
    {
        var store = new RecordingAssignmentStore();
        var handler = new AssignSupplementHandler(
            new AssignSupplementCommandValidator(),
            Context("trainer", Guid.NewGuid(), Guid.NewGuid()), new TestClock(), store);

        var result = await handler.HandleAsync(
            new AssignSupplementCommand(Guid.Empty, Guid.Empty, " ", " ", null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task GetMy_ForwardsTrainerAndAuthenticatedUserToJoinedQuery()
    {
        var trainerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var queries = new RecordingAssignmentQueries();
        var handler = new GetMySupplementAssignmentHandler(
            Context("client", trainerId, userId), queries);
        var assignmentId = Guid.NewGuid();

        var result = await handler.HandleAsync(
            new GetMySupplementAssignmentQuery(assignmentId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal((trainerId, userId, assignmentId),
            (queries.TrainerId, queries.UserId, queries.AssignmentId));
    }

    [Fact]
    public async Task CreateGlobal_WhenAdministrativeFlagIsFalse_DoesNotCallStore()
    {
        var store = new RecordingGlobalStore();
        var handler = new CreateGlobalSupplementHandler(
            new CreateGlobalSupplementCommandValidator(),
            Context("superuser", null, Guid.NewGuid(), false), new TestClock(), store);
        var command = new CreateGlobalSupplementCommand(
            "Creatine", null, "grams", "5 g", "daily", null);

        var result = await handler.HandleAsync(
            command, TestContext.Current.CancellationToken);

        Assert.Equal("supplement_administrator_only", result.Error!.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task CreateGlobal_ForwardsAuthenticatedActorAndCancellationToken()
    {
        var actorUserId = Guid.NewGuid();
        var store = new RecordingGlobalStore();
        var handler = new CreateGlobalSupplementHandler(
            new CreateGlobalSupplementCommandValidator(),
            Context("superuser", null, actorUserId, true), new TestClock(), store);
        using var source = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new CreateGlobalSupplementCommand(
                "Creatine", null, "grams", "5 g", "daily", "internal"),
            source.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal((actorUserId, source.Token), (store.ActorUserId, store.CancellationToken));
    }

    private static TestTenantContext Context(
        string role, Guid? trainerId, Guid? userId, bool administrative = false) =>
        new(trainerId, userId, role, administrative);

    private sealed record TestTenantContext(
        Guid? TrainerId, Guid? UserId, string? Role, bool IsAdministrative) : ITenantContext
    {
        public TenantOrigin Origin => TenantOrigin.Http;
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class RecordingAssignmentStore : IClientSupplementAssignmentStore
    {
        public int Calls { get; private set; }
        public Guid TrainerId { get; private set; }
        public string? ServingSize { get; private set; }
        public string? Timing { get; private set; }
        public string? TrainerNotes { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<ClientSupplementAssignmentStoreResult> AssignAsync(
            Guid trainerId, Guid clientId, Guid supplementId, string? servingSize,
            string? timing, string? trainerNotes, DateTime now,
            CancellationToken cancellationToken)
        {
            Calls++;
            TrainerId = trainerId;
            ServingSize = servingSize;
            Timing = timing;
            TrainerNotes = trainerNotes;
            CancellationToken = cancellationToken;
            var supplement = new Supplement(
                trainerId, Guid.NewGuid(), "Creatine", null,
                "grams", "5 g", "daily", "internal", now);
            var assignment = new ClientSupplementAssignment(
                trainerId, clientId, supplement.Id,
                servingSize ?? supplement.ServingSize,
                timing ?? supplement.Timing, trainerNotes, now);
            return Task.FromResult(ClientSupplementAssignmentStoreResult.WithEntities(
                ClientSupplementAssignmentStoreResult.Status.Assigned,
                assignment, supplement));
        }

        public Task<ClientSupplementAssignmentStoreResult> UpdateInstructionsAsync(
            Guid trainerId, Guid assignmentId, string servingSize, string timing,
            string? trainerNotes, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(ClientSupplementAssignmentStoreResult.For(
                ClientSupplementAssignmentStoreResult.Status.AssignmentNotFound));

        public Task<ClientSupplementAssignmentStoreResult> SetActiveAsync(
            Guid trainerId, Guid assignmentId, bool isActive, DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult(ClientSupplementAssignmentStoreResult.For(
                ClientSupplementAssignmentStoreResult.Status.AssignmentNotFound));
    }

    private sealed class RecordingAssignmentQueries : IClientSupplementAssignmentQueries
    {
        public Guid TrainerId { get; private set; }
        public Guid UserId { get; private set; }
        public Guid AssignmentId { get; private set; }

        public Task<MySupplementAssignmentDto?> GetMyAsync(
            Guid trainerId, Guid userId, Guid assignmentId,
            CancellationToken cancellationToken)
        {
            TrainerId = trainerId;
            UserId = userId;
            AssignmentId = assignmentId;
            return Task.FromResult<MySupplementAssignmentDto?>(new(
                assignmentId, Guid.NewGuid(), "Creatine", null,
                "grams", "5 g", "daily", "client note", false, Now));
        }

        public Task<ClientSupplementAssignmentDto?> GetAsync(
            Guid trainerId, Guid assignmentId, CancellationToken cancellationToken) =>
            Task.FromResult<ClientSupplementAssignmentDto?>(null);

        public Task<PageResult<ClientSupplementAssignmentDto>> ListAsync(
            Guid trainerId, Guid? clientId, SupplementAssignmentActivityFilter activity,
            PageRequest page, CancellationToken cancellationToken) =>
            Task.FromResult(new PageResult<ClientSupplementAssignmentDto>([], 0));

        public Task<PageResult<MySupplementAssignmentDto>> ListMyActiveAsync(
            Guid trainerId, Guid userId, PageRequest page,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PageResult<MySupplementAssignmentDto>([], 0));
    }

    private sealed class RecordingGlobalStore : IGlobalSupplementStore
    {
        public int Calls { get; private set; }
        public Guid ActorUserId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<GlobalSupplementStoreResult> CreateAsync(
            Guid actorUserId, string name, string? description, string unitOfMeasure,
            string servingSize, string timing, string? trainerNotes, DateTime now,
            CancellationToken cancellationToken)
        {
            Calls++;
            ActorUserId = actorUserId;
            CancellationToken = cancellationToken;
            var supplement = new Supplement(
                null, actorUserId, name, description, unitOfMeasure,
                servingSize, timing, trainerNotes, now);
            return Task.FromResult(GlobalSupplementStoreResult.WithSupplement(
                GlobalSupplementStoreResult.Status.Created, supplement));
        }

        public Task<GlobalSupplementStoreResult> UpdateAsync(
            Guid actorUserId, Guid supplementId, string name, string? description,
            string unitOfMeasure, string servingSize, string timing,
            string? trainerNotes, DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult(GlobalSupplementStoreResult.For(
                GlobalSupplementStoreResult.Status.NotFound));

        public Task<GlobalSupplementStoreResult> SetActiveAsync(
            Guid actorUserId, Guid supplementId, bool isActive, DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult(GlobalSupplementStoreResult.For(
                GlobalSupplementStoreResult.Status.NotFound));

        public Task<GlobalSupplementStoreResult> DeleteAsync(
            Guid actorUserId, Guid supplementId, DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult(GlobalSupplementStoreResult.For(
                GlobalSupplementStoreResult.Status.NotFound));
    }
}
