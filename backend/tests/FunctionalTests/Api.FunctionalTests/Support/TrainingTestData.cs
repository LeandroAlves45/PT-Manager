using Application.Common.Abstractions;
using Application.Features.Training.Exercises.Abstractions;
using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Domain.Entities.Sessions;
using Domain.Entities.Training;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Support;

/// <summary>
/// Semeia o mínimo necessário para os testes de Training, Sessions e Packs,
/// sem depender dos endpoints que os próprios testes provam.
/// </summary>
internal static class TrainingTestData
{
    /// <summary>Tenant com cliente, exercício privado e tipo de pack prontos a usar.</summary>
    internal sealed record TrainingTenant(
        Guid TrainerId,
        Guid ClientId,
        Guid ExerciseId,
        Guid PackTypeId);

    /// <summary>Semeia um tenant completo para os testes.</summary>
    internal static async Task<TrainingTenant> SeedTenantAsync(
        ApiWebApplicationFactory factory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            factory,
            $"training-{Guid.NewGuid():N}",
            cancellationToken);
        var clientId = await TrainerTenantSeeder.SeedClientAsync(
            factory,
            trainer.TrainerId,
            "Training client",
            cancellationToken);
        var exerciseId = await SeedPrivateExerciseAsync(
            factory,
            trainer.TrainerId,
            "Squat",
            cancellationToken);
        var packTypeId = await SeedPackTypeAsync(
            factory,
            trainer.TrainerId,
            "Pack 10 sessions",
            sessionCount: 10,
            cancellationToken);

        return new TrainingTenant(trainer.TrainerId, clientId, exerciseId, packTypeId);
    }

    /// <summary>Cria um exercício privado pertencente ao tenant indicado.</summary>
    internal static async Task<Guid> SeedPrivateExerciseAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        string name,
        CancellationToken cancellationToken,
        bool isActive = true)
    {
        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var exercise = new Exercise(
            trainerId,
            name,
            null,
            "quadriceps",
            "barbell",
            "intermediate",
            null,
            TrainerTenantSeeder.SeedInstant);

        if (!isActive)
            exercise.SetActive(false, TrainerTenantSeeder.SeedInstant);

        context.Exercises.Add(exercise);
        await context.SaveChangesAsync(cancellationToken);

        return exercise.Id;
    }

    /// <summary>Cria um exercício global do catálogo partilhado.</summary>
    internal static async Task<Guid> SeedGlobalExerciseAsync(
        ApiWebApplicationFactory factory,
        string name,
        CancellationToken cancellationToken,
        bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var superuserId = await SeedSuperuserAsync(factory, cancellationToken);

        await using var scope = CreateAdministrativeScope(factory, superuserId);
        var store = scope.ServiceProvider.GetRequiredService<IGlobalExerciseStore>();

        var created = await store.CreateAsync(
            superuserId,
            name,
            null,
            null,
            null,
            null,
            null,
            TrainerTenantSeeder.SeedInstant,
            cancellationToken);

        var exerciseId = created.Exercise?.Id
            ?? throw new InvalidOperationException("Global exercise seed failed.");

        if (!isActive)
        {
            await store.SetActiveAsync(
                superuserId,
                exerciseId,
                false,
                TrainerTenantSeeder.SeedInstant,
                cancellationToken);
        }

        return exerciseId;
    }

    /// <summary>Cria um superuser sem tenant, para os endpoints administrativos.</summary>
    internal static async Task<Guid> SeedSuperuserAsync(
        ApiWebApplicationFactory factory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var user = new User(
            new EmailAddress($"superuser-{Guid.NewGuid():N}@example.test"),
            "superuser",
            "Superuser 4C",
            TrainerTenantSeeder.SeedInstant);
        user.SetPasswordHash(
            "opaque-functional-test-password-hash",
            TrainerTenantSeeder.SeedInstant);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }

    /// <summary>Cria um tipo de pack activo do tenant.</summary>
    internal static async Task<Guid> SeedPackTypeAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        string name,
        int sessionCount,
        CancellationToken cancellationToken)
    {
        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var packType = new PackType(
            trainerId,
            name,
            sessionCount,
            priceCents: 25_000,
            currency: "EUR",
            expectedDurationDays: 90,
            TrainerTenantSeeder.SeedInstant);

        context.PackTypes.Add(packType);
        await context.SaveChangesAsync(cancellationToken);

        return packType.Id;
    }

    /// <summary>Atribui um pack a um cliente, com saldo completo.</summary>
    internal static async Task<Guid> SeedClientSessionPackAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid clientId,
        Guid packTypeId,
        CancellationToken cancellationToken)
    {
        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var packType = await context.PackTypes.FindAsync([packTypeId], cancellationToken)
            ?? throw new InvalidOperationException("Pack type seed missing.");

        var pack = new ClientSessionPack(
            trainerId,
            clientId,
            packType,
            DateOnly.FromDateTime(TrainerTenantSeeder.SeedInstant),
            null,
            TrainerTenantSeeder.SeedInstant);

        context.ClientSessionPacks.Add(pack);
        await context.SaveChangesAsync(cancellationToken);

        return pack.Id;
    }

    /// <summary>
    /// Cria uma sessão directamente na base, permitindo instantes passados que o
    /// endpoint de agendamento recusaria.
    /// </summary>
    internal static async Task<Guid> SeedSessionAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid clientId,
        Guid? packId,
        DateTimeOffset startsAt,
        CancellationToken cancellationToken,
        SessionStatus? status = null)
    {
        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var session = new Session(
            trainerId,
            clientId,
            packId,
            startsAt,
            durationMinutes: 60,
            "Studio",
            "personal",
            null,
            TrainerTenantSeeder.SeedInstant);

        if (status is not null && status != SessionStatus.Scheduled)
            ApplyStatus(session, status, TrainerTenantSeeder.SeedInstant);

        context.Sessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);

        return session.Id;
    }

    /// <summary>Lê o saldo actual de um pack atribuído.</summary>
    internal static async Task<int> ReadPackBalanceAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid packId,
        CancellationToken cancellationToken)
    {
        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var pack = await context.ClientSessionPacks.FindAsync([packId], cancellationToken)
            ?? throw new InvalidOperationException("Client session pack seed missing.");

        return pack.SessionsRemaining;
    }

    private static void ApplyStatus(Session session, SessionStatus status, DateTime now)
    {
        if (status == SessionStatus.Completed)
            session.Complete(now);
        else if (status == SessionStatus.CancelledByClient)
            session.CancelByClient(now);
        else if (status == SessionStatus.CancelledByTrainer)
            session.CancelByTrainer(now);
        else if (status == SessionStatus.NoShow)
            session.MarkNoShow(now);
        else
            throw new ArgumentOutOfRangeException(nameof(status), status.Value, null);
    }

    private static AsyncServiceScope CreateAdministrativeScope(
        ApiWebApplicationFactory factory,
        Guid superuserId)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(null, superuserId, "superuser", TenantOrigin.System, true);

        return scope;
    }

    private static AsyncServiceScope CreateTrainerScope(
        ApiWebApplicationFactory factory,
        Guid trainerId)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(trainerId, trainerId, "trainer", TenantOrigin.System, false);

        return scope;
    }
}
