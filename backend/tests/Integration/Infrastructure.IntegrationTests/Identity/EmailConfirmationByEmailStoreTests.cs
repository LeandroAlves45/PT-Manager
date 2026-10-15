using Application.Features.Authentication.Abstractions;
using Domain.Entities.Identity;
using Infrastructure.Identity;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class EmailConfirmationByEmailStoreTests(PostgresContainerFixture database)
{
    private static readonly DateTime Now = IdentityStoreTestSupport.Now;

    private readonly IdentityStoreTestSupport _support = new(database);

    [Fact]
    public async Task IssueByEmailAsync_UnconfirmedAccount_IssuesTokenForTheStoredEmail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"by-email-{Guid.NewGuid():N}@example.test";
        var userId = await _support.SeedUserAsync(
            email, "trainer", confirmed: false, cancellationToken);

        // O pedido chega com outra capitalização: a procura é pelo email normalizado.
        var result = await IssueAsync(email.ToUpperInvariant(), cancellationToken);

        Assert.Equal(EmailConfirmationStoreStatus.Issued, result.Kind);
        Assert.Equal(email, result.Secret!.RecipientEmail);
        await using var verification = database.CreateContext(null);
        var hashes = await verification.EmailVerificationTokens.AsNoTracking()
            .Where(token => token.UserId == userId)
            .Select(token => token.TokenHash)
            .ToListAsync(cancellationToken);
        Assert.Single(hashes);
        Assert.NotEqual(result.Secret.RawToken, hashes[0]);
    }

    [Fact]
    public async Task IssueByEmailAsync_ConfirmedAccount_ReportsAlreadyConfirmedWithoutToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"by-email-confirmed-{Guid.NewGuid():N}@example.test";
        var userId = await _support.SeedUserAsync(
            email, "trainer", confirmed: true, cancellationToken);

        var result = await IssueAsync(email, cancellationToken);

        Assert.Equal(EmailConfirmationStoreStatus.AlreadyConfirmed, result.Kind);
        Assert.Null(result.Secret);
        Assert.Equal(0, await CountTokensAsync(userId, cancellationToken));
    }

    [Fact]
    public async Task IssueByEmailAsync_InactiveAccount_ReportsInactiveWithoutToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"by-email-inactive-{Guid.NewGuid():N}@example.test";
        var userId = await _support.SeedUserAsync(
            email, "trainer", confirmed: false, cancellationToken);
        await database.ExecuteSqlAsync(
            "UPDATE users SET is_active = FALSE WHERE id = @id",
            cancellationToken,
            new Npgsql.NpgsqlParameter("id", userId));

        var result = await IssueAsync(email, cancellationToken);

        Assert.Equal(EmailConfirmationStoreStatus.AccountInactive, result.Kind);
        Assert.Equal(0, await CountTokensAsync(userId, cancellationToken));
    }

    [Fact]
    public async Task IssueByEmailAsync_UnknownEmail_ReportsUserNotFound()
    {
        var result = await IssueAsync(
            $"nobody-{Guid.NewGuid():N}@example.test", TestContext.Current.CancellationToken);

        Assert.Equal(EmailConfirmationStoreStatus.UserNotFound, result.Kind);
    }

    [Fact]
    public async Task IssueByEmailAsync_ConcurrentRequests_LeaveASingleUsableToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"by-email-race-{Guid.NewGuid():N}@example.test";
        var userId = await _support.SeedUserAsync(
            email, "trainer", confirmed: false, cancellationToken);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => Task.Run(
                () => IssueAsync(email, cancellationToken), cancellationToken)));

        // A primeira emite; as restantes caem no intervalo mínimo e mantêm esse link.
        Assert.Equal(1, results.Count(result =>
            result.Kind == EmailConfirmationStoreStatus.Issued));
        Assert.Equal(3, results.Count(result =>
            result.Kind == EmailConfirmationStoreStatus.RecentlyIssued));
        Assert.Equal(1, await CountTokensAsync(userId, cancellationToken));
    }

    [Fact]
    public async Task IssueByEmailAsync_WithinCooldown_KeepsThePendingTokenAndIssuesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"by-email-cooldown-{Guid.NewGuid():N}@example.test";
        var userId = await _support.SeedUserAsync(
            email, "trainer", confirmed: false, cancellationToken);
        var first = await IssueAsync(email, cancellationToken);

        var second = await IssueAsync(email, cancellationToken, Now.AddSeconds(30));

        Assert.Equal(EmailConfirmationStoreStatus.Issued, first.Kind);
        Assert.Equal(EmailConfirmationStoreStatus.RecentlyIssued, second.Kind);
        Assert.Null(second.Secret);
        // O link emitido primeiro continua utilizável.
        await using var context = database.CreateContext(null);
        var consumed = await new EmailConfirmationStore(context, new OpaqueTokenService())
            .ConsumeAsync(first.Secret!.RawToken, Now.AddSeconds(31), cancellationToken);
        Assert.Equal(EmailConfirmationStoreStatus.Confirmed, consumed.Kind);
        Assert.Equal(0, await CountTokensAsync(userId, cancellationToken));
    }

    [Fact]
    public async Task IssueByEmailAsync_AfterCooldown_ReplacesThePendingToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"by-email-after-{Guid.NewGuid():N}@example.test";
        var userId = await _support.SeedUserAsync(
            email, "trainer", confirmed: false, cancellationToken);
        var first = await IssueAsync(email, cancellationToken);

        var second = await IssueAsync(email, cancellationToken, Now.AddSeconds(61));

        Assert.Equal(EmailConfirmationStoreStatus.Issued, second.Kind);
        Assert.NotEqual(first.Secret!.RawToken, second.Secret!.RawToken);
        Assert.Equal(1, await CountTokensAsync(userId, cancellationToken));
    }

    private async Task<EmailConfirmationStoreResult> IssueAsync(
        string email,
        CancellationToken cancellationToken,
        DateTime? now = null)
    {
        var at = now ?? Now;
        await using var context = database.CreateContext(null);
        return await new EmailConfirmationStore(context, new OpaqueTokenService())
            .IssueByEmailAsync(email, at.AddHours(24), at, cancellationToken);
    }

    private async Task<int> CountTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var verification = database.CreateContext(null);
        return await verification.EmailVerificationTokens.CountAsync(
            token => token.UserId == userId && token.ConsumedAt == null, cancellationToken);
    }
}
