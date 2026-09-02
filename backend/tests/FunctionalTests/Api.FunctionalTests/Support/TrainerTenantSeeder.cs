using Application.Common.Abstractions;
using Domain.Entities.Billing;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Support;

/// <summary>
/// Semeia tenants reais em PostgreSQL para os testes funcionais do sub-lote 4A.
/// </summary>
/// <remarks>
/// A escrita passa pelo <see cref="PtManagerDbContext"/> real, com o
/// <c>TenantWriteValidationInterceptor</c> ativo e o tenant estabelecido através de
/// <see cref="ITenantContextInitializer"/>. É deliberado: semear por SQL directo
/// contornaria as mesmas invariantes de tenant que os testes existem para provar,
/// e um seed que o interceptor rejeitasse indicaria um teste construído sobre um
/// estado que a aplicação nunca produziria.
/// </remarks>
internal static class TrainerTenantSeeder
{
    /// <summary>Instante fixo dos dados semeados, para asserções determinísticas.</summary>
    internal static readonly DateTime SeedInstant =
        new(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Cria um personal trainer completo: conta, subscrição activa e definições.
    /// </summary>
    /// <param name="factory">Host de teste que fornece o container de serviços.</param>
    /// <param name="discriminator">Prefixo do email, para isolar tenants entre testes.</param>
    /// <param name="cancellationToken">Sinal de cancelamento.</param>
    /// <returns>Identificadores do tenant semeado.</returns>
    internal static async Task<SeededTrainer> SeedTrainerAsync(
        ApiWebApplicationFactory factory,
        string discriminator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var trainer = new User(
            new EmailAddress($"{discriminator}-{Guid.NewGuid():N}@trainer.test"),
            "trainer",
            "Functional Test Trainer",
            SeedInstant);
        trainer.SetPasswordHash("opaque-functional-test-password-hash", SeedInstant);

        // O trial tem de terminar no futuro face ao instante de criação, exigência do
        // construtor de TrainerSubscription.
        var subscription = new TrainerSubscription(
            trainer.Id,
            SeedInstant.AddDays(15),
            SeedInstant);

        var settings = new Domain.Entities.TrainerSettings.TrainerSettings(
            trainer.Id,
            SeedInstant);

        await using var scope = CreateTenantScope(factory, trainer.Id);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        context.Users.Add(trainer);
        context.TrainerSubscriptions.Add(subscription);
        context.TrainerSettings.Add(settings);
        await context.SaveChangesAsync(cancellationToken);

        return new SeededTrainer(trainer.Id, trainer.Email);
    }

    /// <summary>Cria uma ficha de cliente pertencente ao tenant indicado.</summary>
    /// <remarks>
    /// A subscrição é incrementada na mesma transação, tal como
    /// <c>CreateClientHandler</c> faz em produção, para que o estado semeado seja
    /// indistinguível do estado produzido pelo caso de uso real.
    /// </remarks>
    internal static async Task<Guid> SeedClientAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        string name,
        CancellationToken cancellationToken,
        bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using var scope = CreateTenantScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var client = new Client(
            trainerId,
            name,
            null,
            $"+3519{Random.Shared.Next(10_000_000, 99_999_999)}",
            BirthDate.Create(
                new DateOnly(1990, 1, 1),
                DateOnly.FromDateTime(SeedInstant)),
            BiologicalSex.Male,
            objective: null,
            notes: null,
            emergencyContactName: null,
            emergencyContactPhone: null,
            SeedInstant);

        if (!isActive)
            client.Deactivate(SeedInstant);

        context.Clients.Add(client);

        var subscription = await context.TrainerSubscriptions
            .SingleAsync(item => item.TrainerId == trainerId, cancellationToken);
        if (isActive)
            subscription.RegisterClientAdded(SeedInstant);

        await context.SaveChangesAsync(cancellationToken);

        return client.Id;
    }

    private static AsyncServiceScope CreateTenantScope(
        ApiWebApplicationFactory factory,
        Guid trainerId)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(trainerId, trainerId, "trainer", TenantOrigin.System, false);

        return scope;
    }
}

/// <summary>Identificadores de um tenant semeado.</summary>
/// <param name="TrainerId">Identificador do personal trainer, que é também o tenant.</param>
/// <param name="Email">Email da conta criada.</param>
internal sealed record SeededTrainer(Guid TrainerId, string Email);
