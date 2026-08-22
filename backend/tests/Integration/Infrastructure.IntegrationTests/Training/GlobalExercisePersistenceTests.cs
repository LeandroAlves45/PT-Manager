using Application.Features.Training.Exercises.Abstractions;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Training;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Training;

[Collection(PostgresCollection.Name)]
public sealed class GlobalExercisePersistenceTests
{
    private static readonly DateTime Now =
        new(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public GlobalExercisePersistenceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_WritesExerciseAndAuditAtomically()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actorUserId = Guid.NewGuid();
        await using var context = CreateAdminContext(actorUserId);
        var store = new GlobalExerciseStore(context, new PostgresConstraintTranslator());

        var outcome = await store.CreateAsync(
            actorUserId,
            "Squat",
            null,
            "legs",
            "barbell",
            "medium",
            null,
            Now,
            cancellationToken);

        Assert.Equal(GlobalExerciseStoreResult.Status.Created, outcome.Kind);
        Assert.True(await context.AdministrativeAuditEntries.AnyAsync(
            entry => entry.ResourceId == outcome.Exercise!.Id && entry.Action == "create",
            cancellationToken));
    }

    [Fact]
    public async Task Update_WhenReferenced_ReturnsReferencedWithoutAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var exerciseId = await CreateGlobalExerciseAsync(tenant.TrainerId, cancellationToken);
        await using (var trainer = _fixture.CreateContext(tenant.TrainerId))
        {
            await IntegrationTestData.SeedTrainingPlanReferencingExerciseAsync(
                trainer,
                tenant.TrainerId,
                tenant.ClientId,
                exerciseId,
                Now,
                cancellationToken);
        }
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalExerciseStore(context, new PostgresConstraintTranslator());

        var outcome = await store.UpdateAsync(
            tenant.TrainerId,
            exerciseId,
            "Back squat",
            null,
            "legs",
            "barbell",
            "medium",
            null,
            Now.AddMinutes(1),
            cancellationToken);

        Assert.Equal(GlobalExerciseStoreResult.Status.Referenced, outcome.Kind);
        Assert.False(await context.AdministrativeAuditEntries.AnyAsync(
            entry => entry.ResourceId == exerciseId && entry.Action == "update",
            cancellationToken));
    }

    [Fact]
    public async Task Delete_WhenReferenced_ReturnsHasReferencesAndPreservesExercise()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var exerciseId = await CreateGlobalExerciseAsync(tenant.TrainerId, cancellationToken);
        await using (var trainer = _fixture.CreateContext(tenant.TrainerId))
        {
            await IntegrationTestData.SeedTrainingPlanReferencingExerciseAsync(
                trainer,
                tenant.TrainerId,
                tenant.ClientId,
                exerciseId,
                Now,
                cancellationToken);
        }
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalExerciseStore(context, new PostgresConstraintTranslator());

        var outcome = await store.DeleteAsync(
            tenant.TrainerId,
            exerciseId,
            Now.AddMinutes(1),
            cancellationToken);

        Assert.Equal(GlobalExerciseStoreResult.Status.HasReferences, outcome.Kind);
        Assert.True(await context.Exercises.IgnoreQueryFilters()
            .AnyAsync(exercise => exercise.Id == exerciseId, cancellationToken));
    }

    [Fact]
    public async Task Archive_WhenReferenced_ChangesStateAndWritesAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var exerciseId = await CreateGlobalExerciseAsync(tenant.TrainerId, cancellationToken);
        await using (var trainer = _fixture.CreateContext(tenant.TrainerId))
        {
            await IntegrationTestData.SeedTrainingPlanReferencingExerciseAsync(
                trainer,
                tenant.TrainerId,
                tenant.ClientId,
                exerciseId,
                Now,
                cancellationToken);
        }
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalExerciseStore(context, new PostgresConstraintTranslator());

        var outcome = await store.SetActiveAsync(
            tenant.TrainerId,
            exerciseId,
            false,
            Now.AddMinutes(1),
            cancellationToken);

        Assert.Equal(GlobalExerciseStoreResult.Status.Changed, outcome.Kind);
        Assert.True(await context.AdministrativeAuditEntries.AnyAsync(
            entry => entry.ResourceId == exerciseId && entry.Action == "archive",
            cancellationToken));
    }

    [Fact]
    public async Task Delete_WhenUnreferenced_RemovesExerciseAndPreservesAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actorUserId = Guid.NewGuid();
        var exerciseId = await CreateGlobalExerciseAsync(actorUserId, cancellationToken);
        await using var context = CreateAdminContext(actorUserId);
        var store = new GlobalExerciseStore(context, new PostgresConstraintTranslator());

        var outcome = await store.DeleteAsync(
            actorUserId,
            exerciseId,
            Now.AddMinutes(1),
            cancellationToken);

        Assert.Equal(GlobalExerciseStoreResult.Status.Deleted, outcome.Kind);
        Assert.False(await context.Exercises.IgnoreQueryFilters()
            .AnyAsync(exercise => exercise.Id == exerciseId, cancellationToken));
        Assert.True(await context.AdministrativeAuditEntries.AnyAsync(
            entry => entry.ResourceId == exerciseId && entry.Action == "delete",
            cancellationToken));
    }

    private async Task<Guid> CreateGlobalExerciseAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var context = CreateAdminContext(actorUserId);
        var store = new GlobalExerciseStore(context, new PostgresConstraintTranslator());
        var outcome = await store.CreateAsync(
            actorUserId,
            "Squat",
            null,
            "legs",
            "barbell",
            "medium",
            null,
            Now,
            cancellationToken);
        return outcome.Exercise!.Id;
    }

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
