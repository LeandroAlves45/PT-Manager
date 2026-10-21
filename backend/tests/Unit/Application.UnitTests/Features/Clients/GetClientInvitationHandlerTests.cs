using Application.Features.Clients;
using Application.Features.Clients.Dtos;
using Application.Features.Clients.GetClientInvitation;

namespace Application.UnitTests.Features.Clients;

/// <summary>Estado do convite na ficha (U63, §4.5): pendente, expirado, aceite e sem convite.</summary>
public sealed class GetClientInvitationHandlerTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private static GetClientInvitationHandler Create(FakeClientQueries queries, string role = "trainer") =>
        new(
            new StubTenantContext { TrainerId = TrainerId, Role = role },
            new StubClock { UtcNow = Now },
            queries);

    [Fact]
    public async Task ValidInvite_IsPendingWithInviteEmailAndExpiry()
    {
        var expiresAt = Now.AddDays(3);
        var queries = new FakeClientQueries
        {
            InvitationResult = new ClientInvitationSnapshot(false, "old@example.test", expiresAt)
        };

        var result = await Create(queries).HandleAsync(
            new GetClientInvitationQuery(ClientId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ClientInvitationStates.Pending, result.Value.State);
        Assert.Equal("old@example.test", result.Value.Email);
        Assert.Equal(expiresAt, result.Value.ExpiresAt);
        Assert.Equal(TrainerId, queries.LastInvitationTrainerId);
    }

    [Fact]
    public async Task InviteAtExactExpiry_IsExpired()
    {
        // Igual a InviteToken.IsValid (ExpiresAt > now): no instante da validade já não serve.
        var queries = new FakeClientQueries
        {
            InvitationResult = new ClientInvitationSnapshot(false, "client@example.test", Now)
        };

        var result = await Create(queries).HandleAsync(
            new GetClientInvitationQuery(ClientId), TestContext.Current.CancellationToken);

        Assert.Equal(ClientInvitationStates.Expired, result.Value.State);
        Assert.Equal(Now, result.Value.ExpiresAt);
    }

    [Fact]
    public async Task ClientWithAccount_IsAcceptedWithoutDetails()
    {
        var queries = new FakeClientQueries
        {
            InvitationResult = new ClientInvitationSnapshot(true, "client@example.test", Now.AddDays(1))
        };

        var result = await Create(queries).HandleAsync(
            new GetClientInvitationQuery(ClientId), TestContext.Current.CancellationToken);

        Assert.Equal(ClientInvitationStates.Accepted, result.Value.State);
        Assert.Null(result.Value.Email);
        Assert.Null(result.Value.ExpiresAt);
    }

    [Fact]
    public async Task NoUnusedInvite_IsNone()
    {
        var queries = new FakeClientQueries
        {
            InvitationResult = new ClientInvitationSnapshot(false, null, null)
        };

        var result = await Create(queries).HandleAsync(
            new GetClientInvitationQuery(ClientId), TestContext.Current.CancellationToken);

        Assert.Equal(ClientInvitationStates.None, result.Value.State);
    }

    [Fact]
    public async Task MissingOrForeignClient_IsNotFound()
    {
        var result = await Create(new FakeClientQueries()).HandleAsync(
            new GetClientInvitationQuery(ClientId), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.ClientNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ClientRole_IsRejectedBeforeReading()
    {
        var queries = new FakeClientQueries
        {
            InvitationResult = new ClientInvitationSnapshot(false, "client@example.test", Now.AddDays(1))
        };

        var result = await Create(queries, role: "client").HandleAsync(
            new GetClientInvitationQuery(ClientId), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.TrainerOnly.Code, result.Error!.Code);
        Assert.Equal(Guid.Empty, queries.LastInvitationTrainerId);
    }
}
