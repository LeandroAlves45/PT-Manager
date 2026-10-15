using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Google.Abstractions;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Identity;
using Infrastructure.IntegrationTests.Billing;
using Infrastructure.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.IntegrationTests.Identity;

/// <summary>Entrar com Google só cria conta com consentimento explícito (create_account).</summary>
[Collection(PostgresCollection.Name)]
public sealed class ExternalAuthenticationStoreAccountCreationTests(PostgresContainerFixture database)
{
    private static readonly DateTime Now = IdentityStoreTestSupport.Now;

    private readonly IdentityStoreTestSupport _support = new(database);

    [Fact]
    public async Task SignInAsync_UnknownIdentityWithoutCreateAccount_ReturnsAccountNotFoundAndCreatesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var identity = CreateIdentity("unknown");
        var nonce = await SeedChallengeAsync(cancellationToken);

        var result = await SignInAsync(identity, nonce.RawToken, createAccount: false);

        Assert.Equal(GoogleSignInStoreStatus.AccountNotFound, result.Kind);
        await using var verification = database.CreateContext(null);
        Assert.Equal(0, await verification.Users.CountAsync(
            user => user.NormalizedEmail == new EmailAddress(identity.Email).Normalized,
            cancellationToken));
        Assert.Equal(0, await verification.Set<ExternalIdentity>().CountAsync(
            external => external.Subject == identity.Subject, cancellationToken));
        Assert.False(await verification.Set<ExternalAuthenticationChallenge>().AnyAsync(
            challenge => challenge.NonceHash == nonce.TokenHash, cancellationToken));
    }

    [Fact]
    public async Task SignInAsync_UnknownIdentityWithCreateAccount_CreatesTheTrainer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var identity = CreateIdentity("create");
        var nonce = await SeedChallengeAsync(cancellationToken);

        var result = await SignInAsync(identity, nonce.RawToken, createAccount: true);

        Assert.Equal(GoogleSignInStoreStatus.Authenticated, result.Kind);
        Assert.Equal("trainer", result.Principal!.Role);
    }

    [Fact]
    public async Task SignInAsync_EmailOfExistingAccountWithoutCreateAccount_StillRequiresLinking()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var identity = CreateIdentity("link-required");
        await _support.SeedUserAsync(identity.Email, "trainer", cancellationToken: cancellationToken);
        var nonce = await SeedChallengeAsync(cancellationToken);

        var result = await SignInAsync(identity, nonce.RawToken, createAccount: false);

        Assert.Equal(GoogleSignInStoreStatus.AccountLinkRequired, result.Kind);
    }

    [Fact]
    public async Task SignInAsync_LinkedIdentityWithoutCreateAccount_SignsInNormally()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var identity = CreateIdentity("returning");
        var userId = await _support.SeedUserAsync(
            identity.Email, "trainer", cancellationToken: cancellationToken);
        await using (var seed = database.CreateContext(null))
        {
            seed.Set<ExternalIdentity>().Add(new ExternalIdentity(
                userId, identity.Provider, identity.Subject, Now));
            await seed.SaveChangesAsync(cancellationToken);
        }

        var nonce = await SeedChallengeAsync(cancellationToken);

        var result = await SignInAsync(identity, nonce.RawToken, createAccount: false);

        Assert.Equal(GoogleSignInStoreStatus.Authenticated, result.Kind);
    }

    private async Task<GeneratedOpaqueToken> SeedChallengeAsync(CancellationToken cancellationToken)
    {
        var nonce = new OpaqueTokenService().Generate();
        await using var context = database.CreateContext(null);
        context.Set<ExternalAuthenticationChallenge>().Add(new ExternalAuthenticationChallenge(
            nonce.TokenHash,
            ExternalAuthenticationChallenge.SignInPurpose,
            null,
            Now.AddMinutes(5),
            Now));
        await context.SaveChangesAsync(cancellationToken);
        return nonce;
    }

    private async Task<GoogleSignInStoreResult> SignInAsync(
        VerifiedExternalIdentity identity,
        string rawNonce,
        bool createAccount)
    {
        var tenant = new BillingWebhookTenantContext();
        await using var context = _support.CreateRetryingContext(tenant);
        await using var services = IdentityStoreTestSupport.CreateIdentityServices(context);
        return await new ExternalAuthenticationStore(
            context,
            new OpaqueTokenService(),
            services.GetRequiredService<UserManager<User>>(),
            tenant)
            .SignInAsync(
                identity,
                rawNonce,
                null,
                createAccount,
                Now.AddDays(15),
                Now.AddHours(24),
                Now.AddDays(30),
                Now,
                TestContext.Current.CancellationToken);
    }

    private static VerifiedExternalIdentity CreateIdentity(string discriminator) => new(
        ExternalIdentity.GoogleProvider,
        $"subject-{discriminator}-{Guid.NewGuid():N}",
        $"google-{discriminator}-{Guid.NewGuid():N}@example.test",
        "Google Account Test",
        true);
}
