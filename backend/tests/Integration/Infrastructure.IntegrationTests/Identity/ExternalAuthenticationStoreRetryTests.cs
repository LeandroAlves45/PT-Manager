using System.Data.Common;
using Application.Common.Abstractions;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Google.Abstractions;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.Identity;
using Infrastructure.IntegrationTests.Billing;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure.IntegrationTests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class ExternalAuthenticationStoreRetryTests(PostgresContainerFixture database)
{
    private const string Password = "Retry-Password-1!";
    private static readonly DateTime Now =
        new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SignInAsync_WhenCommitConfirmationIsTransient_IsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var identity = CreateIdentity("onboarding");
        var tokens = new OpaqueTokenService();
        var nonce = tokens.Generate();
        var commitFailure = new FailOnceAfterCommitInterceptor();
        var tenant = new BillingWebhookTenantContext();
        await using var context = CreateRetryingContext(tenant, commitFailure);
        context.Set<ExternalAuthenticationChallenge>().Add(new ExternalAuthenticationChallenge(
            nonce.TokenHash,
            ExternalAuthenticationChallenge.SignInPurpose,
            null,
            Now.AddMinutes(5),
            Now));
        await context.SaveChangesAsync(cancellationToken);
        commitFailure.Arm();
        await using var services = CreateIdentityServices(context);
        var store = new ExternalAuthenticationStore(
            context,
            tokens,
            services.GetRequiredService<UserManager<User>>(),
            tenant);

        var result = await store.SignInAsync(
            identity,
            nonce.RawToken,
            null,
            Now.AddDays(15),
            Now.AddHours(24),
            Now.AddDays(30),
            Now,
            cancellationToken);

        await using var verification = database.CreateContext(null);
        var persisted = (
            Users: await verification.Users.CountAsync(
                user => user.NormalizedEmail == new EmailAddress(identity.Email).Normalized,
                cancellationToken),
            Identities: await verification.Set<ExternalIdentity>().CountAsync(
                external => external.Provider == identity.Provider &&
                    external.Subject == identity.Subject,
                cancellationToken),
            RefreshSessions: await verification.RefreshTokens.CountAsync(
                token => token.UserId == result.Principal!.UserId,
                cancellationToken));

        Assert.Equal(
            (GoogleSignInStoreStatus.Authenticated, 1, 1, 1, 1, 1),
            (result.Kind, commitFailure.Failures, persisted.Users, persisted.Identities,
                persisted.RefreshSessions, tenant.EstablishCalls));
    }

    [Fact]
    public async Task LinkAsync_WhenCommitConfirmationIsTransient_ReturnsLinkedWithoutDuplicate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var identity = CreateIdentity("link");
        var user = new User(new EmailAddress(identity.Email), "trainer", "Retry Link", Now);
        user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, Password), Now);
        await using (var seed = database.CreateContext(user.Id))
        {
            seed.Users.Add(user);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var tokens = new OpaqueTokenService();
        var nonce = tokens.Generate();
        var commitFailure = new FailOnceAfterCommitInterceptor();
        var tenant = BillingWebhookTenantContext.ForTrainer(user.Id);
        await using var context = CreateRetryingContext(tenant, commitFailure);
        context.Set<ExternalAuthenticationChallenge>().Add(new ExternalAuthenticationChallenge(
            nonce.TokenHash,
            ExternalAuthenticationChallenge.LinkPurpose,
            user.Id,
            Now.AddMinutes(5),
            Now));
        await context.SaveChangesAsync(cancellationToken);
        commitFailure.Arm();
        await using var services = CreateIdentityServices(context);
        var store = new ExternalAuthenticationStore(
            context,
            tokens,
            services.GetRequiredService<UserManager<User>>(),
            tenant);

        var result = await store.LinkAsync(
            user.Id,
            identity,
            nonce.RawToken,
            Password,
            Now,
            cancellationToken);

        await using var verification = database.CreateContext(null);
        var identityCount = await verification.Set<ExternalIdentity>().CountAsync(
            external => external.Provider == identity.Provider &&
                external.Subject == identity.Subject && external.UserId == user.Id,
            cancellationToken);
        Assert.Equal(
            (GoogleLinkStoreStatus.Linked, 1, 1),
            (result, commitFailure.Failures, identityCount));
    }

    private PtManagerDbContext CreateRetryingContext(
        BillingWebhookTenantContext tenant,
        FailOnceAfterCommitInterceptor commitFailure)
    {
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(database.ConnectionString, npgsql =>
                npgsql.EnableRetryOnFailure(maxRetryCount: 3))
            .AddInterceptors(
                new TenantWriteValidationInterceptor(tenant),
                commitFailure)
            .EnableDetailedErrors()
            .Options;
        return new PtManagerDbContext(options, tenant);
    }

    private static ServiceProvider CreateIdentityServices(PtManagerDbContext context)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(context);
        services.AddIdentityCore<User>(options => options.User.RequireUniqueEmail = true)
            .AddUserStore<UserIdentityStore>();
        return services.BuildServiceProvider();
    }

    private static VerifiedExternalIdentity CreateIdentity(string discriminator) => new(
        ExternalIdentity.GoogleProvider,
        $"subject-{discriminator}-{Guid.NewGuid():N}",
        $"google-{discriminator}-{Guid.NewGuid():N}@example.test",
        "Google Retry Test",
        true);

    private sealed class FailOnceAfterCommitInterceptor : DbTransactionInterceptor
    {
        private bool _isArmed;
        internal int Failures { get; private set; }

        internal void Arm() => _isArmed = true;

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (_isArmed && Failures == 0)
            {
                Failures++;
                throw new NpgsqlException(
                    "Simulated loss of the PostgreSQL commit acknowledgement.",
                    new TimeoutException());
            }

            return Task.CompletedTask;
        }
    }
}
