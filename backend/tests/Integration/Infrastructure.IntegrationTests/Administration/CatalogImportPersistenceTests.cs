using Application.Features.Administration.CatalogImport;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Training.Exercises.Abstractions;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Nutrition;
using Infrastructure.Persistence.Training;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Administration;

/// <summary>
/// Importação CSV do catálogo global contra PostgreSQL real: auditoria por item aceite pelo
/// interceptor, duplicados saltados (incluindo arquivados) e simulação sem escrita.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CatalogImportPersistenceTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public CatalogImportPersistenceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ImportFoods_WritesEveryFoodWithItsOwnImportAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actorUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        await using var context = CreateAdminContext(actorUserId);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());

        var outcome = await store.ImportAsync(
            actorUserId,
            [Food(2, $"Arroz {suffix}"), Food(3, $"Ovo {suffix}")],
            dryRun: false,
            Now,
            cancellationToken);

        Assert.Equal(2, outcome.Created);
        Assert.Empty(outcome.Skipped);
        var foods = await context.Foods.IgnoreQueryFilters()
            .Where(food => food.Name.EndsWith(suffix))
            .ToListAsync(cancellationToken);
        Assert.Equal(2, foods.Count);
        Assert.All(foods, food => Assert.Null(food.OwnerTrainerId));
        var foodIds = foods.Select(food => food.Id).ToList();
        Assert.Equal(2, await context.AdministrativeAuditEntries.CountAsync(
            entry => foodIds.Contains(entry.ResourceId) && entry.Action == "import" &&
                entry.ActorUserId == actorUserId,
            cancellationToken));
    }

    [Fact]
    public async Task ImportFoods_ExistingOrArchivedName_IsSkippedRegardlessOfCaseAndAccents()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actorUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        await using (var setup = CreateAdminContext(actorUserId))
        {
            var setupStore = new GlobalFoodStore(setup, new PostgresConstraintTranslator());
            var archived = await setupStore.CreateAsync(
                actorUserId, $"Feijão {suffix}", null, 9m, 14m, 0.5m, null, null, Now, cancellationToken);
            await setupStore.SetActiveAsync(
                actorUserId, archived.Food!.Id, false, Now, cancellationToken);
        }

        await using var context = CreateAdminContext(actorUserId);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());

        var outcome = await store.ImportAsync(
            actorUserId,
            [Food(2, $"FEIJAO {suffix}"), Food(3, $"Lentilhas {suffix}")],
            dryRun: false,
            Now,
            cancellationToken);

        Assert.Equal(1, outcome.Created);
        var skipped = Assert.Single(outcome.Skipped);
        Assert.Equal((2, CatalogImportSkipReasons.AlreadyExists), (skipped.Line, skipped.Reason));
        Assert.Equal(2, await context.Foods.IgnoreQueryFilters()
            .CountAsync(food => food.Name.EndsWith(suffix), cancellationToken));
    }

    [Fact]
    public async Task ImportFoods_DryRun_CountsButWritesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actorUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        await using var context = CreateAdminContext(actorUserId);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());

        var outcome = await store.ImportAsync(
            actorUserId, [Food(2, $"Batata {suffix}")], dryRun: true, Now, cancellationToken);

        Assert.Equal(1, outcome.Created);
        Assert.False(await context.Foods.IgnoreQueryFilters()
            .AnyAsync(food => food.Name.EndsWith(suffix), cancellationToken));
        Assert.False(await context.AdministrativeAuditEntries.AnyAsync(
            entry => entry.ActorUserId == actorUserId, cancellationToken));
    }

    [Fact]
    public async Task ImportExercises_WritesExercisesAndAuditAndSkipsExistingNames()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actorUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        await using (var setup = CreateAdminContext(actorUserId))
        {
            await new GlobalExerciseStore(setup, new PostgresConstraintTranslator()).CreateAsync(
                actorUserId, $"Supino {suffix}", null, "chest", "Barra", null, null, Now, cancellationToken);
        }

        await using var context = CreateAdminContext(actorUserId);
        var store = new GlobalExerciseStore(context, new PostgresConstraintTranslator());

        var outcome = await store.ImportAsync(
            actorUserId,
            [
                new GlobalExerciseImportItem(2, $"supino {suffix}", null, "chest", "Barra", "Intermédio"),
                new GlobalExerciseImportItem(3, $"Prancha {suffix}", "Core estável.", "core", null, "Iniciante"),
            ],
            dryRun: false,
            Now,
            cancellationToken);

        Assert.Equal(1, outcome.Created);
        Assert.Equal(2, Assert.Single(outcome.Skipped).Line);
        var prancha = await context.Exercises.IgnoreQueryFilters()
            .SingleAsync(exercise => exercise.Name == $"Prancha {suffix}", cancellationToken);
        Assert.Equal(("core", "Iniciante", (string?)null), (prancha.MuscleGroups, prancha.DifficultyLevel, prancha.VideoUrl));
        Assert.True(await context.AdministrativeAuditEntries.AnyAsync(
            entry => entry.ResourceId == prancha.Id && entry.Action == "import", cancellationToken));
    }

    private static GlobalFoodImportItem Food(int line, string name) =>
        new(line, name, null, 10m, 20m, 5m, 1m, 100m);

    private PtManagerDbContext CreateAdminContext(Guid actorUserId)
    {
        var tenantContext = TestTenantContext.Administrator(actorUserId);
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(new TenantWriteValidationInterceptor(tenantContext))
            .Options;
        return new PtManagerDbContext(options, tenantContext);
    }
}
