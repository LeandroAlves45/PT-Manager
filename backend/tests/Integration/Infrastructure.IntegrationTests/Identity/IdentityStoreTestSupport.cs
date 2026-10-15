using Application.Common.Abstractions;
using Domain.Entities.Clients;
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
using TrainerSettingsEntity = Domain.Entities.TrainerSettings.TrainerSettings;

namespace Infrastructure.IntegrationTests.Identity;

/// <summary>
/// Monta os stores de autenticação sobre o PostgreSQL dos testes com as mesmas opções de
/// Identity da produção, e semeia o par treinador + ficha + convite que vários testes usam.
/// </summary>
internal sealed class IdentityStoreTestSupport(PostgresContainerFixture database)
{
    internal static readonly DateTime Now = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    internal const string StrongPassword = "Invited-Password-1!";

    /// <summary>Contexto com execution strategy repetível, como a API usa.</summary>
    internal PtManagerDbContext CreateRetryingContext(
        BillingWebhookTenantContext tenant,
        params IInterceptor[] extraInterceptors)
    {
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(database.ConnectionString, npgsql =>
                npgsql.EnableRetryOnFailure(maxRetryCount: 3))
            .AddInterceptors([new TenantWriteValidationInterceptor(tenant), .. extraInterceptors])
            .EnableDetailedErrors()
            .Options;
        return new PtManagerDbContext(options, tenant);
    }

    /// <summary>UserManager com a política de password de produção (mínimo de 8 e classes).</summary>
    internal static ServiceProvider CreateIdentityServices(PtManagerDbContext context)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(context);
        services.AddIdentityCore<User>(options =>
        {
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        }).AddUserStore<UserIdentityStore>();
        return services.BuildServiceProvider();
    }

    /// <summary>Cria treinador, ficha ativa (sem conta) e um convite válido para o email dado.</summary>
    internal async Task<InvitedClientSeed> SeedInviteAsync(
        string discriminator,
        DateTime? expiresAt = null,
        string? clientEmail = null,
        CancellationToken cancellationToken = default)
    {
        var trainer = new User(
            new EmailAddress($"trainer-{discriminator}-{Guid.NewGuid():N}@example.test"),
            "trainer",
            "Invite Trainer",
            Now);
        trainer.SetPasswordHash("opaque-test-password-hash", Now);
        var email = clientEmail ?? $"invited-{discriminator}-{Guid.NewGuid():N}@example.test";
        var client = new Client(
            trainer.Id,
            "Ficha Convidada",
            email,
            "+351900000000",
            BirthDate.Create(new DateOnly(1995, 1, 1), DateOnly.FromDateTime(Now)),
            BiologicalSex.Female,
            null,
            null,
            null,
            null,
            Now);
        var tokens = new OpaqueTokenService().Generate();
        var invite = new InviteToken(
            trainer.Id,
            client.Id,
            new EmailAddress(email),
            tokens.TokenHash,
            expiresAt ?? Now.AddDays(7),
            Now.AddMinutes(-1));

        await using var context = database.CreateContext(trainer.Id);
        context.Users.Add(trainer);
        context.Clients.Add(client);
        context.TrainerSettings.Add(new TrainerSettingsEntity(trainer.Id, Now));
        context.InviteTokens.Add(invite);
        await context.SaveChangesAsync(cancellationToken);

        return new InvitedClientSeed(trainer.Id, client.Id, email, tokens.RawToken, invite.Id);
    }

    /// <summary>Cria uma conta local confirmada com este email, sem ficha.</summary>
    internal async Task<Guid> SeedUserAsync(
        string email,
        string role = "client",
        bool confirmed = true,
        CancellationToken cancellationToken = default)
    {
        var user = new User(new EmailAddress(email), role, "Existing", Now);
        user.SetPasswordHash("opaque-test-password-hash", Now);
        if (confirmed)
            user.ConfirmEmail(Now);

        await using var context = database.CreateContext(null);
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}

internal sealed record InvitedClientSeed(
    Guid TrainerId,
    Guid ClientId,
    string Email,
    string RawToken,
    Guid InviteId);
