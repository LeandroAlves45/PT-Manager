using Application.Features.Administration.ContentModeration.Abstractions;
using Domain.Entities.Identity;
using Domain.Entities.Nutrition;
using Domain.Exceptions;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Administration;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.Administration;

[Collection(PostgresCollection.Name)]
public sealed class PrivateCatalogModerationStoreTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public PrivateCatalogModerationStoreTests(PostgresContainerFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task BlockFood_ChangesStateAndAuditAtomically()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);
        var store = new PrivateCatalogModerationStore(context);

        var outcome = await store.BlockFoodAsync(actor.Id, foodId,
            PlatformEnforcementReason.MaliciousContent, Now, cancellationToken);

        var state = await context.Foods.IgnoreQueryFilters().Where(food => food.Id == foodId)
            .Select(food => food.PlatformEnforcementStatus.Value).SingleAsync(cancellationToken);
        var audits = await context.AdministrativeAuditEntries.CountAsync(
            entry => entry.ResourceId == foodId && entry.Action == "food_platform_blocked",
            cancellationToken);
        Assert.Equal((PrivateCatalogModerationStoreResult.Changed, "blocked", 1),
            (outcome, state, audits));
    }

    [Fact]
    public async Task BlockFood_PersistsReasonAndTimestamp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);

        await new PrivateCatalogModerationStore(context).BlockFoodAsync(actor.Id, foodId,
            PlatformEnforcementReason.DangerousInformation, Now, cancellationToken);

        var persisted = await context.Foods.IgnoreQueryFilters().AsNoTracking()
            .Where(food => food.Id == foodId)
            .Select(food => new
            {
                Reason = food.PlatformEnforcementReason!.Value,
                food.PlatformEnforcedAt
            })
            .SingleAsync(cancellationToken);

        Assert.Equal(("dangerous_information", (DateTime?)Now),
            (persisted.Reason, persisted.PlatformEnforcedAt));
    }

    [Fact]
    public async Task BlockFood_WithSameReason_IsNoOpWithoutSecondAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);
        var store = new PrivateCatalogModerationStore(context);
        await store.BlockFoodAsync(actor.Id, foodId,
            PlatformEnforcementReason.ProhibitedContent, Now, cancellationToken);

        var outcome = await store.BlockFoodAsync(actor.Id, foodId,
            PlatformEnforcementReason.ProhibitedContent, Now.AddMinutes(1), cancellationToken);

        var audits = await context.AdministrativeAuditEntries.CountAsync(
            entry => entry.ResourceId == foodId, cancellationToken);
        Assert.Equal((PrivateCatalogModerationStoreResult.AlreadyInRequestedState, 1),
            (outcome, audits));
    }

    [Fact]
    public async Task BlockFood_WithDifferentReason_CreatesSecondAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);
        var store = new PrivateCatalogModerationStore(context);
        await store.BlockFoodAsync(actor.Id, foodId,
            PlatformEnforcementReason.ProhibitedContent, Now, cancellationToken);

        var outcome = await store.BlockFoodAsync(actor.Id, foodId,
            PlatformEnforcementReason.MaliciousContent, Now.AddMinutes(1), cancellationToken);

        var audits = await context.AdministrativeAuditEntries.CountAsync(
            entry => entry.ResourceId == foodId, cancellationToken);
        Assert.Equal((PrivateCatalogModerationStoreResult.Changed, 2), (outcome, audits));
    }

    [Fact]
    public async Task UnblockFood_WhenAlreadyAllowed_IsNoOpWithoutAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);

        var outcome = await new PrivateCatalogModerationStore(context)
            .UnblockFoodAsync(actor.Id, foodId, Now, cancellationToken);

        var audits = await context.AdministrativeAuditEntries.CountAsync(
            entry => entry.ResourceId == foodId, cancellationToken);
        Assert.Equal((PrivateCatalogModerationStoreResult.AlreadyInRequestedState, 0),
            (outcome, audits));
    }

    [Fact]
    public async Task UnblockFood_AfterBlock_RestoresAllowedState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);
        var store = new PrivateCatalogModerationStore(context);
        await store.BlockFoodAsync(actor.Id, foodId,
            PlatformEnforcementReason.MaliciousContent, Now, cancellationToken);

        var outcome = await store.UnblockFoodAsync(actor.Id, foodId, Now.AddMinutes(1), cancellationToken);

        var persisted = await context.Foods.IgnoreQueryFilters().AsNoTracking()
            .Where(food => food.Id == foodId)
            .Select(food => new
            {
                Status = food.PlatformEnforcementStatus.Value,
                Reason = food.PlatformEnforcementReason!.Value,
                food.PlatformEnforcedAt
            })
            .SingleAsync(cancellationToken);

        Assert.Equal(
            (PrivateCatalogModerationStoreResult.Changed, "allowed", (string?)null, (DateTime?)null),
            (outcome, persisted.Status, persisted.Reason, persisted.PlatformEnforcedAt));
    }

    [Fact]
    public async Task BlockFood_WhenResourceIsGlobal_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedGlobalFoodAsync(cancellationToken);
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);

        var outcome = await new PrivateCatalogModerationStore(context).BlockFoodAsync(
            actor.Id, foodId, PlatformEnforcementReason.MaliciousContent, Now, cancellationToken);

        Assert.Equal(PrivateCatalogModerationStoreResult.NotPrivate, outcome);
    }

    [Fact]
    public async Task BlockFood_WhenResourceMissing_ReturnsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actor = await SeedSuperuserAsync(cancellationToken);
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);

        var outcome = await new PrivateCatalogModerationStore(context).BlockFoodAsync(
            actor.Id, Guid.NewGuid(), PlatformEnforcementReason.MaliciousContent, Now, cancellationToken);

        Assert.Equal(PrivateCatalogModerationStoreResult.NotFound, outcome);
    }

    [Fact]
    public async Task BlockFood_WhenActorIsNotSuperuser_IsRejectedWithoutChange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await using var context = _fixture.CreateAdministrativeContext(tenant.TrainerId);

        var outcome = await new PrivateCatalogModerationStore(context).BlockFoodAsync(
            tenant.TrainerId, foodId, PlatformEnforcementReason.MaliciousContent, Now, cancellationToken);

        var status = await context.Foods.IgnoreQueryFilters().AsNoTracking()
            .Where(food => food.Id == foodId)
            .Select(food => food.PlatformEnforcementStatus.Value).SingleAsync(cancellationToken);
        Assert.Equal((PrivateCatalogModerationStoreResult.ActorInvalid, "allowed"), (outcome, status));
    }

    [Fact]
    public async Task BlockFood_WhenActorIsDeactivatedSuperuser_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await _fixture.ExecuteSqlAsync(
            "UPDATE users SET is_active = false WHERE id = @id",
            cancellationToken,
            new NpgsqlParameter("id", actor.Id));
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);

        var outcome = await new PrivateCatalogModerationStore(context).BlockFoodAsync(
            actor.Id, foodId, PlatformEnforcementReason.MaliciousContent, Now, cancellationToken);

        Assert.Equal(PrivateCatalogModerationStoreResult.ActorInvalid, outcome);
    }

    [Fact]
    public async Task BlockFood_WhenActorIsDeletedSuperuser_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await _fixture.ExecuteSqlAsync(
            "UPDATE users SET is_deleted = true WHERE id = @id",
            cancellationToken,
            new NpgsqlParameter("id", actor.Id));
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);

        var outcome = await new PrivateCatalogModerationStore(context).BlockFoodAsync(
            actor.Id, foodId, PlatformEnforcementReason.MaliciousContent, Now, cancellationToken);

        Assert.Equal(PrivateCatalogModerationStoreResult.ActorInvalid, outcome);
    }

    [Fact]
    public async Task BlockExercise_ChangesStateAndAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId, cancellationToken);
        await using var context = _fixture.CreateAdministrativeContext(actor.Id);

        var outcome = await new PrivateCatalogModerationStore(context).BlockExerciseAsync(
            actor.Id, exerciseId, PlatformEnforcementReason.ProhibitedContent, Now, cancellationToken);

        var audits = await context.AdministrativeAuditEntries.CountAsync(
            entry => entry.ResourceId == exerciseId && entry.Action == "exercise_platform_blocked",
            cancellationToken);
        Assert.Equal((PrivateCatalogModerationStoreResult.Changed, 1), (outcome, audits));
    }

    [Fact]
    public async Task NewMealPlanReference_ToBlockedFood_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await using (var admin = _fixture.CreateAdministrativeContext(actor.Id))
            await new PrivateCatalogModerationStore(admin).BlockFoodAsync(actor.Id, foodId,
                PlatformEnforcementReason.DangerousInformation, Now, cancellationToken);

        await using var trainer = _fixture.CreateContext(tenant.TrainerId);
        var action = () => IntegrationTestData.SeedMealPlanReferencingFoodAsync(
            trainer, tenant.TrainerId, tenant.ClientId, foodId, Now, cancellationToken);

        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task NewTrainingPlanReference_ToBlockedExercise_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var exerciseId = await SeedPrivateExerciseAsync(tenant.TrainerId, cancellationToken);
        await using (var admin = _fixture.CreateAdministrativeContext(actor.Id))
            await new PrivateCatalogModerationStore(admin).BlockExerciseAsync(actor.Id, exerciseId,
                PlatformEnforcementReason.DangerousInformation, Now, cancellationToken);

        await using var trainer = _fixture.CreateContext(tenant.TrainerId);
        var action = () => IntegrationTestData.SeedTrainingPlanReferencingExerciseAsync(
            trainer, tenant.TrainerId, tenant.ClientId, exerciseId, Now, cancellationToken);

        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task HistoricalMealPlanReference_SurvivesLaterBlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var actor = await SeedSuperuserAsync(cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);
        await using (var trainer = _fixture.CreateContext(tenant.TrainerId))
            await IntegrationTestData.SeedMealPlanReferencingFoodAsync(
                trainer, tenant.TrainerId, tenant.ClientId, foodId, Now, cancellationToken);

        await using (var admin = _fixture.CreateAdministrativeContext(actor.Id))
            await new PrivateCatalogModerationStore(admin).BlockFoodAsync(actor.Id, foodId,
                PlatformEnforcementReason.MaliciousContent, Now.AddMinutes(1), cancellationToken);

        var remaining = await _fixture.QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM meal_plan_meal_items WHERE food_id = @id",
            cancellationToken,
            new NpgsqlParameter("id", foodId));
        Assert.Equal(1L, remaining);
    }

    [Fact]
    public async Task BlockedGlobalFood_IsRejectedByCheckConstraint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var foodId = await SeedGlobalFoodAsync(cancellationToken);

        var action = () => _fixture.ExecuteSqlAsync(
            """
            UPDATE foods
            SET platform_enforcement_status = 'blocked',
                platform_enforcement_reason = 'malicious_content',
                platform_enforced_at = now()
            WHERE id = @id
            """,
            cancellationToken,
            new NpgsqlParameter("id", foodId));

        await Assert.ThrowsAsync<PostgresException>(action);
    }

    [Fact]
    public async Task BlockedFoodWithoutReason_IsRejectedByCheckConstraint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);

        var action = () => _fixture.ExecuteSqlAsync(
            """
            UPDATE foods
            SET platform_enforcement_status = 'blocked',
                platform_enforcement_reason = NULL,
                platform_enforced_at = now()
            WHERE id = @id
            """,
            cancellationToken,
            new NpgsqlParameter("id", foodId));

        await Assert.ThrowsAsync<PostgresException>(action);
    }

    [Fact]
    public async Task AllowedFoodWithReason_IsRejectedByCheckConstraint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);

        var action = () => _fixture.ExecuteSqlAsync(
            """
            UPDATE foods
            SET platform_enforcement_status = 'allowed',
                platform_enforcement_reason = 'malicious_content',
                platform_enforced_at = now()
            WHERE id = @id
            """,
            cancellationToken,
            new NpgsqlParameter("id", foodId));

        await Assert.ThrowsAsync<PostgresException>(action);
    }

    [Fact]
    public async Task UnknownReasonCode_IsRejectedByCheckConstraint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);

        var action = () => _fixture.ExecuteSqlAsync(
            """
            UPDATE foods
            SET platform_enforcement_status = 'blocked',
                platform_enforcement_reason = 'free_text',
                platform_enforced_at = now()
            WHERE id = @id
            """,
            cancellationToken,
            new NpgsqlParameter("id", foodId));

        await Assert.ThrowsAsync<PostgresException>(action);
    }

    [Fact]
    public async Task ExistingRowsAfterMigration_AreAllowedByDefault()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedPrivateFoodAsync(tenant.TrainerId, cancellationToken);

        var status = await _fixture.QueryScalarAsync<string>(
            "SELECT platform_enforcement_status FROM foods WHERE id = @id",
            cancellationToken,
            new NpgsqlParameter("id", foodId));

        Assert.Equal("allowed", status);
    }

    private async Task<User> SeedSuperuserAsync(CancellationToken cancellationToken)
    {
        var user = new User(new EmailAddress($"admin-{Guid.NewGuid():N}@example.test"),
            "superuser", "Administrator", Now);
        user.SetPasswordHash("integration-test-password-hash", Now);
        await using var context = _fixture.CreateAdministrativeContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<Guid> SeedPrivateFoodAsync(Guid trainerId, CancellationToken cancellationToken)
    {
        var food = new Food(trainerId, "Private food", null, 1, 1, 1, null, Now);
        await using var context = _fixture.CreateContext(trainerId);
        context.Foods.Add(food);
        await context.SaveChangesAsync(cancellationToken);
        return food.Id;
    }

    /// <summary>
    /// Insere catálogo global por SQL. A escrita global via DbContext exigiria uma
    /// entrada de auditoria administrativa, que não é o comportamento sob teste aqui.
    /// </summary>
    private async Task<Guid> SeedGlobalFoodAsync(CancellationToken cancellationToken)
    {
        var foodId = Guid.NewGuid();
        await _fixture.ExecuteSqlAsync(
            """
            INSERT INTO foods (
                id, owner_trainer_id, name, description, protein, carbs, fats, fiber,
                is_active, platform_enforcement_status, created_at, updated_at)
            VALUES (@id, NULL, 'Global food', NULL, 1, 1, 1, NULL,
                true, 'allowed', now(), now())
            """,
            cancellationToken,
            new NpgsqlParameter("id", foodId));
        return foodId;
    }

    private async Task<Guid> SeedPrivateExerciseAsync(Guid trainerId, CancellationToken cancellationToken)
    {
        var exercise = IntegrationTestData.Exercise(trainerId, Now);
        await using var context = _fixture.CreateContext(trainerId);
        context.Exercises.Add(exercise);
        await context.SaveChangesAsync(cancellationToken);
        return exercise.Id;
    }
}
