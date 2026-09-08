using Domain.Entities.Billing;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Jobs;
using Npgsql;

namespace Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Verifica a validação de tenant antes de qualquer handler correr.
/// O trainer é lido do item persistido e nunca do payload. Um job sem tenant
/// válido não pode executar: na Fase 5A não existem tipos globais na allowlist,
/// por isso "sem trainer" significa sempre recusa.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class JobTenantValidatorTests
{
    private readonly PostgresContainerFixture _fixture;

    public JobTenantValidatorTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task IsAvailable_WhenTrainerIsActiveWithActiveSubscription_ReturnsTrue()
    {
        var seed = await SeedAsync();
        await SeedSubscriptionAsync(seed.TrainerId);

        Assert.True(await IsAvailableAsync(seed.TrainerId));
    }

    [Fact]
    public async Task IsAvailable_WhenTrainerHasNoSubscription_ReturnsFalse()
    {
        var seed = await SeedAsync();

        // Sem subscrição não há direito a consumir trabalho durável.
        Assert.False(await IsAvailableAsync(seed.TrainerId));
    }

    [Theory]
    [InlineData("INACTIVE")]
    [InlineData("SUSPENDED")]
    [InlineData("CANCELLED")]
    public async Task IsAvailable_WhenSubscriptionIsNotActive_ReturnsFalse(string status)
    {
        var seed = await SeedAsync();
        await SeedSubscriptionAsync(seed.TrainerId);
        await SetSubscriptionStatusAsync(seed.TrainerId, status);

        Assert.False(await IsAvailableAsync(seed.TrainerId));
    }

    [Fact]
    public async Task IsAvailable_WhenTrainerIsInactive_ReturnsFalse()
    {
        var seed = await SeedAsync();
        await SeedSubscriptionAsync(seed.TrainerId);
        await _fixture.ExecuteSqlAsync(
            "UPDATE users SET is_active = false WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", seed.TrainerId));

        Assert.False(await IsAvailableAsync(seed.TrainerId));
    }

    [Fact]
    public async Task IsAvailable_WhenTrainerIsSoftDeleted_ReturnsFalse()
    {
        var seed = await SeedAsync();
        await SeedSubscriptionAsync(seed.TrainerId);
        await _fixture.ExecuteSqlAsync(
            "UPDATE users SET is_deleted = true WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", seed.TrainerId));

        Assert.False(await IsAvailableAsync(seed.TrainerId));
    }

    [Fact]
    public async Task IsAvailable_WhenUserIsNotATrainer_ReturnsFalse()
    {
        var seed = await SeedAsync();

        // Um utilizador cliente nunca pode ser o tenant de um durable job.
        Assert.False(await IsAvailableAsync(seed.ClientUserId));
    }

    [Fact]
    public async Task IsAvailable_WhenTrainerDoesNotExist_ReturnsFalse()
    {
        Assert.False(await IsAvailableAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IsAvailable_WhenTrainerIdIsAbsent_ReturnsFalse(bool useEmptyGuid)
    {
        // Não existem jobs globais na allowlist da Fase 5A.
        Assert.False(await IsAvailableAsync(useEmptyGuid ? Guid.Empty : null));
    }

    private async Task<bool> IsAvailableAsync(Guid? trainerId)
    {
        await using var context = _fixture.CreateAdministrativeContext();
        var validator = new JobTenantValidator(context);

        return await validator.IsAvailableAsync(
            trainerId, TestContext.Current.CancellationToken);
    }

    /// <summary>Cria a subscrição pela entidade, que nasce activa.</summary>
    private async Task SeedSubscriptionAsync(Guid trainerId)
    {
        var now = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

        await using var context = _fixture.CreateContext(trainerId);
        context.TrainerSubscriptions.Add(
            new TrainerSubscription(trainerId, now.AddDays(30), now));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Altera apenas o estado. A entidade não expõe transições arbitrárias, e o
    /// que está em prova aqui é a query do validador, não as regras de billing.
    /// </summary>
    private Task SetSubscriptionStatusAsync(Guid trainerId, string status) =>
        _fixture.ExecuteSqlAsync(
            "UPDATE trainer_subscriptions SET subscription_status = @status "
                + "WHERE trainer_id = @trainer_id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("status", status),
            new NpgsqlParameter("trainer_id", trainerId));

    private Task<PostgresContainerFixture.TestTenantSeed> SeedAsync() =>
        _fixture.SeedTenantWithClientAsync(
            $"tenantvalidator-{Guid.NewGuid():N}",
            TestContext.Current.CancellationToken);
}
