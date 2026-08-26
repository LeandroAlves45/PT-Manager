using Application.Features.Authentication.Abstractions;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Identity;
using Infrastructure.IntegrationTests.Billing;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class AuthPersistenceTests(PostgresContainerFixture database)
{
    [Fact]
    public async Task EmailConfirmation_RawTokenIsNeverPersisted_AndConsumptionIsSingleUse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User(
            new EmailAddress($"auth-{Guid.NewGuid():N}@example.test"),
            "trainer",
            "Auth Test",
            now);
        user.SetPasswordHash("opaque-test-password-hash", now);

        await using (var seed = database.CreateContext(null))
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync(cancellationToken);
        }

        IssuedAuthenticationSecret secret;
        await using (var issueContext = database.CreateContext(null))
        {
            var store = new EmailConfirmationStore(issueContext, new OpaqueTokenService());
            var issued = await store.IssueAsync(
                user.Id,
                now.AddHours(1),
                now,
                cancellationToken);
            secret = Assert.IsType<IssuedAuthenticationSecret>(issued.Secret);
            var persistedHashes = await issueContext.EmailVerificationTokens
                .AsNoTracking()
                .Select(token => token.TokenHash)
                .ToListAsync(cancellationToken);

            Assert.DoesNotContain(persistedHashes, value => value == secret.RawToken);
        }

        await using var firstContext = database.CreateContext(null);
        var first = await new EmailConfirmationStore(firstContext, new OpaqueTokenService())
            .ConsumeAsync(secret.RawToken, now.AddMinutes(1), cancellationToken);
        await using var secondContext = database.CreateContext(null);
        var second = await new EmailConfirmationStore(secondContext, new OpaqueTokenService())
            .ConsumeAsync(secret.RawToken, now.AddMinutes(2), cancellationToken);

        Assert.Equal(EmailConfirmationStoreStatus.Confirmed, first.Kind);
        Assert.Equal(EmailConfirmationStoreStatus.TokenAlreadyConsumed, second.Kind);
    }

    [Fact]
    public async Task EmailConfirmation_WithRetryingExecutionStrategy_IssuesAndConsumesInsideStrategyTransactions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User(
            new EmailAddress($"auth-retry-{Guid.NewGuid():N}@example.test"),
            "trainer",
            "Auth Retry Test",
            now);
        user.SetPasswordHash("opaque-test-password-hash", now);

        await using (var seed = database.CreateContext(null))
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var support = new BillingTestSupport(database);
        IssuedAuthenticationSecret secret;
        await using (var issueContext = support.CreateRetryingTrainerContext(user.Id))
        {
            var issued = await new EmailConfirmationStore(issueContext, new OpaqueTokenService())
                .IssueAsync(user.Id, now.AddHours(1), now, cancellationToken);
            Assert.Equal(EmailConfirmationStoreStatus.Issued, issued.Kind);
            secret = Assert.IsType<IssuedAuthenticationSecret>(issued.Secret);
        }

        await using var consumeContext = support.CreateRetryingTrainerContext(user.Id);
        var consumed = await new EmailConfirmationStore(consumeContext, new OpaqueTokenService())
            .ConsumeAsync(secret.RawToken, now.AddMinutes(1), cancellationToken);

        Assert.Equal(EmailConfirmationStoreStatus.Confirmed, consumed.Kind);
    }
}
