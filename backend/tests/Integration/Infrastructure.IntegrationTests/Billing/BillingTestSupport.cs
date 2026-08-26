using Application.Common.Abstractions;
using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Billing;

internal sealed class BillingTestSupport(PostgresContainerFixture database)
{
    internal static readonly DateTime Now =
        new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    internal async Task<BillingTrainerSeed> SeedTrainerAsync(
        string discriminator,
        string? customerId = null,
        string? subscriptionId = null,
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        var user = new User(
            new EmailAddress(
                $"billing-{discriminator}-{Guid.NewGuid():N}@example.test"),
            "trainer",
            "Billing Integration Test",
            Now);
        user.SetPasswordHash("opaque-integration-test-password-hash", Now);

        var subscription = new TrainerSubscription(user.Id, Now.AddDays(15), Now);
        if (customerId is not null && subscriptionId is not null)
            subscription.LinkStripeSubscription(customerId, subscriptionId, Now);
        else if (customerId is not null)
            subscription.LinkStripeCustomer(customerId, Now);

        await using (var context = database.CreateContext(user.Id))
        {
            context.Users.Add(user);
            context.TrainerSubscriptions.Add(subscription);
            await context.SaveChangesAsync(cancellationToken);
        }

        if (!isActive)
        {
            await database.ExecuteSqlAsync(
                "UPDATE users SET is_active = FALSE WHERE id = @trainer_id",
                cancellationToken,
                new Npgsql.NpgsqlParameter("trainer_id", user.Id));
        }

        return new BillingTrainerSeed(user.Id, subscription.Id, user.Email);
    }

    internal PtManagerDbContext CreateRetryingTrainerContext(Guid trainerId)
    {
        var tenant = BillingWebhookTenantContext.ForTrainer(trainerId);
        return CreateRetryingContext(tenant);
    }

    internal PtManagerDbContext CreateRetryingWebhookContext(
        out BillingWebhookTenantContext tenant)
    {
        tenant = new BillingWebhookTenantContext();
        return CreateRetryingContext(tenant);
    }

    private PtManagerDbContext CreateRetryingContext(BillingWebhookTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(database.ConnectionString, npgsql =>
                npgsql.EnableRetryOnFailure(maxRetryCount: 3))
            .AddInterceptors(new TenantWriteValidationInterceptor(tenant))
            .EnableDetailedErrors()
            .Options;

        return new PtManagerDbContext(options, tenant);
    }
}

internal sealed record BillingTrainerSeed(
    Guid TrainerId,
    Guid SubscriptionId,
    string Email);

internal sealed class BillingWebhookTenantContext :
    ITenantContext,
    ITenantContextInitializer
{
    public Guid? TrainerId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Role { get; private set; }
    public TenantOrigin Origin { get; private set; } = TenantOrigin.Webhook;
    public bool IsAdministrative { get; private set; }
    public int EstablishCalls { get; private set; }

    internal static BillingWebhookTenantContext ForTrainer(Guid trainerId) => new()
    {
        TrainerId = trainerId,
        UserId = trainerId,
        Role = "trainer",
        Origin = TenantOrigin.Http,
    };

    public void Establish(
        Guid? trainerId,
        Guid? userId,
        string? role,
        TenantOrigin origin,
        bool isAdministrative)
    {
        if (EstablishCalls != 0)
            throw new InvalidOperationException("The tenant context is already established.");

        TrainerId = trainerId;
        UserId = userId;
        Role = role;
        Origin = origin;
        IsAdministrative = isAdministrative;
        EstablishCalls++;
    }
}
