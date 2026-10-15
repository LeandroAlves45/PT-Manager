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
public sealed class AuthenticationRegistrationStoreTests(PostgresContainerFixture database)
{
    private static readonly DateTime Now = IdentityStoreTestSupport.Now;

    private readonly IdentityStoreTestSupport _support = new(database);

    [Fact]
    public async Task RegisterTrainerAsync_NewEmailStrongPassword_Creates()
    {
        var result = await RegisterAsync(
            NewEmail("new"), IdentityStoreTestSupport.StrongPassword);

        Assert.Equal(RegisterTrainerStoreStatus.Created, result.Kind);
    }

    [Fact]
    public async Task RegisterTrainerAsync_ExistingEmailStrongPassword_IsDuplicate()
    {
        var email = NewEmail("dup");
        await _support.SeedUserAsync(
            email, "trainer", cancellationToken: TestContext.Current.CancellationToken);

        var result = await RegisterAsync(
            email.ToUpperInvariant(), IdentityStoreTestSupport.StrongPassword);

        Assert.Equal(RegisterTrainerStoreStatus.DuplicateEmail, result.Kind);
    }

    [Fact]
    public async Task RegisterTrainerAsync_WeakPassword_IsRejectedIdenticallyForNewAndExistingEmails()
    {
        var existing = NewEmail("weak-existing");
        await _support.SeedUserAsync(
            existing, "trainer", cancellationToken: TestContext.Current.CancellationToken);

        var forNewEmail = await RegisterAsync(NewEmail("weak-new"), "alllowercase");
        var forExistingEmail = await RegisterAsync(existing, "alllowercase");

        Assert.Equal(
            (RegisterTrainerStoreStatus.InvalidIdentityData, RegisterTrainerStoreStatus.InvalidIdentityData),
            (forNewEmail.Kind, forExistingEmail.Kind));
    }

    [Fact]
    public async Task RegisterTrainerAsync_ExistingEmailWeakPassword_DoesNotLeakDuplicate()
    {
        var existing = NewEmail("no-leak");
        await _support.SeedUserAsync(
            existing, "trainer", cancellationToken: TestContext.Current.CancellationToken);

        var result = await RegisterAsync(existing, "alllowercase");

        Assert.NotEqual(RegisterTrainerStoreStatus.DuplicateEmail, result.Kind);
    }

    private static string NewEmail(string discriminator) =>
        $"register-{discriminator}-{Guid.NewGuid():N}@example.test";

    private async Task<RegisterTrainerStoreResult> RegisterAsync(string email, string password)
    {
        var tenant = new BillingWebhookTenantContext();
        await using var context = _support.CreateRetryingContext(tenant);
        await using var services = IdentityStoreTestSupport.CreateIdentityServices(context);
        return await new AuthenticationRegistrationStore(
            context,
            services.GetRequiredService<UserManager<User>>(),
            tenant,
            new OpaqueTokenService(),
            new TestClock(Now))
            .RegisterTrainerAsync(
                new RegisterTrainerStoreRequest(
                    email, password, "Trainer", Now.AddDays(15), Now.AddHours(24)),
                TestContext.Current.CancellationToken);
    }
}
