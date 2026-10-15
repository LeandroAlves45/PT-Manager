using Application.Features.Authentication.Abstractions;
using Domain.Entities.Identity;
using Infrastructure.Identity;
using Infrastructure.IntegrationTests.Billing;
using Infrastructure.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.IntegrationTests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class ClientInvitationRegistrationStoreTests(PostgresContainerFixture database)
{
    private static readonly DateTime Now = IdentityStoreTestSupport.Now;
    private const string StrongPassword = IdentityStoreTestSupport.StrongPassword;

    private readonly IdentityStoreTestSupport _support = new(database);

    [Fact]
    public async Task RegisterClientAsync_ValidInvite_CreatesConfirmedClientAccountAndSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync("happy", cancellationToken: cancellationToken);
        var tenant = new BillingWebhookTenantContext();

        var result = await RegisterAsync(seed, StrongPassword, tenant, cancellationToken);

        Assert.Equal(RegisterInvitedClientStoreStatus.Registered, result.Kind);
        var principal = Assert.IsType<AuthenticatedPrincipal>(result.Principal);
        Assert.Equal(("client", seed.TrainerId), (principal.Role, principal.TrainerId));
        Assert.Equal(1, tenant.EstablishCalls);

        await using var verification = database.CreateAdministrativeContext();
        var user = await verification.Users.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == principal.UserId, cancellationToken);
        Assert.Equal(
            (seed.Email, "client", true, true, "Ficha Convidada"),
            (user.Email, user.Role, user.EmailConfirmed, user.PasswordHash is not null, user.FullName));

        var client = await verification.Clients.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(candidate => candidate.Id == seed.ClientId, cancellationToken);
        Assert.Equal(user.Id, client.UserId);

        var invite = await verification.InviteTokens.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == seed.InviteId, cancellationToken);
        Assert.NotNull(invite.UsedAt);

        var refreshHashes = await verification.RefreshTokens.AsNoTracking()
            .Where(token => token.UserId == user.Id)
            .Select(token => token.TokenHash)
            .ToListAsync(cancellationToken);
        Assert.Single(refreshHashes);
        Assert.DoesNotContain(result.RefreshSession!.RawToken, refreshHashes);
    }

    [Fact]
    public async Task RegisterClientAsync_EmailAlreadyHasAccount_ReportsItAndConsumesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync("exists", cancellationToken: cancellationToken);
        await _support.SeedUserAsync(
            seed.Email.ToLowerInvariant(), cancellationToken: cancellationToken);

        var result = await RegisterAsync(
            seed, StrongPassword, new BillingWebhookTenantContext(), cancellationToken);

        Assert.Equal(RegisterInvitedClientStoreStatus.AccountAlreadyExists, result.Kind);
        await AssertNothingConsumedAsync(seed, expectedUsers: 1, cancellationToken);
    }

    [Fact]
    public async Task RegisterClientAsync_WeakPassword_IsRejectedAndRollsEverythingBack()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync("weak", cancellationToken: cancellationToken);

        var result = await RegisterAsync(
            seed, "alllowercase", new BillingWebhookTenantContext(), cancellationToken);

        Assert.Equal(RegisterInvitedClientStoreStatus.PasswordRejected, result.Kind);
        await AssertNothingConsumedAsync(seed, expectedUsers: 0, cancellationToken);
    }

    [Fact]
    public async Task RegisterClientAsync_UnknownToken_IsNotFoundWithoutEstablishingTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = new BillingWebhookTenantContext();
        var invite = await _support.SeedInviteAsync("unknown", cancellationToken: cancellationToken);
        var seed = invite with { RawToken = new OpaqueTokenService().Generate().RawToken };

        var result = await RegisterAsync(seed, StrongPassword, tenant, cancellationToken);

        Assert.Equal(
            (RegisterInvitedClientStoreStatus.TokenNotFound, 0),
            (result.Kind, tenant.EstablishCalls));
    }

    [Fact]
    public async Task RegisterClientAsync_ExpiredInvite_IsRejectedBeforeAnythingIsWritten()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync(
            "expired", expiresAt: Now.AddMinutes(1), cancellationToken: cancellationToken);

        var result = await RegisterAsync(
            seed,
            StrongPassword,
            new BillingWebhookTenantContext(),
            cancellationToken,
            now: Now.AddMinutes(2));

        Assert.Equal(RegisterInvitedClientStoreStatus.TokenExpired, result.Kind);
        await AssertNothingConsumedAsync(seed, expectedUsers: 0, cancellationToken);
    }

    [Fact]
    public async Task RegisterClientAsync_SecondUseOfTheSameInvite_IsAlreadyConsumed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync("twice", cancellationToken: cancellationToken);

        var first = await RegisterAsync(
            seed, StrongPassword, new BillingWebhookTenantContext(), cancellationToken);
        var second = await RegisterAsync(
            seed, StrongPassword, new BillingWebhookTenantContext(), cancellationToken);

        Assert.Equal(
            (RegisterInvitedClientStoreStatus.Registered,
                RegisterInvitedClientStoreStatus.TokenAlreadyConsumed),
            (first.Kind, second.Kind));
    }

    [Fact]
    public async Task RegisterClientAsync_UsedAndExpiredInvite_ReportsExpiredFirst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync(
            "precedence", expiresAt: Now.AddMinutes(10), cancellationToken: cancellationToken);
        await RegisterAsync(
            seed, StrongPassword, new BillingWebhookTenantContext(), cancellationToken);

        var result = await RegisterAsync(
            seed,
            StrongPassword,
            new BillingWebhookTenantContext(),
            cancellationToken,
            now: Now.AddMinutes(11));

        Assert.Equal(RegisterInvitedClientStoreStatus.TokenExpired, result.Kind);
    }

    [Fact]
    public async Task RegisterClientAsync_ArchivedClient_IsARelationshipConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync("archived", cancellationToken: cancellationToken);
        await database.ExecuteSqlAsync(
            "UPDATE clients SET is_active = FALSE WHERE id = @id",
            cancellationToken,
            new Npgsql.NpgsqlParameter("id", seed.ClientId));

        var result = await RegisterAsync(
            seed, StrongPassword, new BillingWebhookTenantContext(), cancellationToken);

        Assert.Equal(RegisterInvitedClientStoreStatus.RelationshipConflict, result.Kind);
        await AssertNothingConsumedAsync(seed, expectedUsers: 0, cancellationToken);
    }

    [Fact]
    public async Task RegisterClientAsync_ClientContactEmailChangedAfterInvite_IsARelationshipConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync("changed", cancellationToken: cancellationToken);
        await database.ExecuteSqlAsync(
            "UPDATE clients SET normalized_contact_email = @email WHERE id = @id",
            cancellationToken,
            new Npgsql.NpgsqlParameter("email", $"OTHER-{Guid.NewGuid():N}@EXAMPLE.TEST"),
            new Npgsql.NpgsqlParameter("id", seed.ClientId));

        var result = await RegisterAsync(
            seed, StrongPassword, new BillingWebhookTenantContext(), cancellationToken);

        Assert.Equal(RegisterInvitedClientStoreStatus.RelationshipConflict, result.Kind);
        await AssertNothingConsumedAsync(seed, expectedUsers: 0, cancellationToken);
    }

    [Fact]
    public async Task RegisterClientAsync_EmailAlreadyHasAccountAndClientIsArchived_ReportsTheAccountFirst()
    {
        // A verificação de conta existente corre antes da do cliente: sem ela, o
        // UserManager.CreateAsync ainda detetaria o duplicado, mas só depois do conflito de relação.
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync("existsarch", cancellationToken: cancellationToken);
        await _support.SeedUserAsync(
            seed.Email.ToLowerInvariant(), cancellationToken: cancellationToken);
        await database.ExecuteSqlAsync(
            "UPDATE clients SET is_active = FALSE WHERE id = @id",
            cancellationToken,
            new Npgsql.NpgsqlParameter("id", seed.ClientId));

        var result = await RegisterAsync(
            seed, StrongPassword, new BillingWebhookTenantContext(), cancellationToken);

        Assert.Equal(RegisterInvitedClientStoreStatus.AccountAlreadyExists, result.Kind);
        await AssertNothingConsumedAsync(seed, expectedUsers: 1, cancellationToken);
    }

    [Fact]
    public async Task RegisterClientAsync_TwoConcurrentRequests_OnlyOneCreatesTheAccount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync("race", cancellationToken: cancellationToken);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(_ => Task.Run(
                () => RegisterAsync(
                    seed, StrongPassword, new BillingWebhookTenantContext(), cancellationToken),
                cancellationToken)));

        Assert.Single(results, result => result.Kind == RegisterInvitedClientStoreStatus.Registered);
        Assert.Single(results, result => result.Kind != RegisterInvitedClientStoreStatus.Registered);
        await using var verification = database.CreateAdministrativeContext();
        var users = await verification.Users.CountAsync(
            user => user.NormalizedEmail == seed.Email.ToUpperInvariant(), cancellationToken);
        Assert.Equal(1, users);
    }

    [Fact]
    public async Task RegisterClientAsync_WhenCommitConfirmationIsLost_IsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _support.SeedInviteAsync("retry", cancellationToken: cancellationToken);
        var commitFailure = new FailOnceAfterCommitInterceptor();
        var tenant = new BillingWebhookTenantContext();
        await using var context = _support.CreateRetryingContext(tenant, commitFailure);
        await using var services = IdentityStoreTestSupport.CreateIdentityServices(context);
        commitFailure.Arm();

        var result = await new ClientInvitationStore(
            context,
            new OpaqueTokenService(),
            tenant,
            tenant,
            services.GetRequiredService<UserManager<User>>())
            .RegisterClientAsync(
                seed.RawToken,
                StrongPassword,
                Now.AddDays(30),
                Now,
                cancellationToken);

        await using var verification = database.CreateAdministrativeContext();
        var users = await verification.Users.CountAsync(
            user => user.NormalizedEmail == seed.Email.ToUpperInvariant(), cancellationToken);
        var sessions = await verification.RefreshTokens.CountAsync(
            token => token.UserId == result.Principal!.UserId, cancellationToken);
        Assert.Equal(
            (RegisterInvitedClientStoreStatus.Registered, 1, 1, 1, 1),
            (result.Kind, commitFailure.Failures, users, sessions, tenant.EstablishCalls));
    }

    private async Task<RegisterInvitedClientStoreResult> RegisterAsync(
        InvitedClientSeed seed,
        string password,
        BillingWebhookTenantContext tenant,
        CancellationToken cancellationToken,
        DateTime? now = null)
    {
        await using var context = _support.CreateRetryingContext(tenant);
        await using var services = IdentityStoreTestSupport.CreateIdentityServices(context);
        return await new ClientInvitationStore(
            context,
            new OpaqueTokenService(),
            tenant,
            tenant,
            services.GetRequiredService<UserManager<User>>())
            .RegisterClientAsync(
                seed.RawToken,
                password,
                (now ?? Now).AddDays(30),
                now ?? Now,
                cancellationToken);
    }

    private async Task AssertNothingConsumedAsync(
        InvitedClientSeed seed,
        int expectedUsers,
        CancellationToken cancellationToken)
    {
        await using var verification = database.CreateAdministrativeContext();
        var invite = await verification.InviteTokens.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == seed.InviteId, cancellationToken);
        var client = await verification.Clients.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(candidate => candidate.Id == seed.ClientId, cancellationToken);
        var users = await verification.Users.CountAsync(
            user => user.NormalizedEmail == seed.Email.ToUpperInvariant(), cancellationToken);
        var sessions = await verification.RefreshTokens.CountAsync(
            token => verification.Users.Any(user =>
                user.Id == token.UserId && user.NormalizedEmail == seed.Email.ToUpperInvariant()),
            cancellationToken);

        Assert.Null(invite.UsedAt);
        Assert.Null(client.UserId);
        Assert.Equal((expectedUsers, 0), (users, sessions));
    }
}
