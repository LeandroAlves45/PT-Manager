using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Data;

[Collection(PostgresCollection.Name)]
public sealed class CatalogReferenceValidationTests
{
    private static readonly DateTime Now = new(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public CatalogReferenceValidationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // ---------- Food ----------

    [Fact]
    public async Task SaveChanges_WhenFoodIsGlobal_PersistsMealPlanItem()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedFoodAsync(ownerTrainerId: null, cancellationToken);
        var plan = CreateMealPlanWithItem(tenant.TrainerId, tenant.ClientId, foodId);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.MealPlans.Add(plan);

        // Act
        await context.SaveChangesAsync(cancellationToken);

        // Assert
        Assert.True(await context.MealPlans
            .AnyAsync(value => value.Id == plan.Id, cancellationToken));
    }

    [Fact]
    public async Task SaveChanges_WhenFoodBelongsToTenant_PersistsMealPlanItem()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedFoodAsync(tenant.TrainerId, cancellationToken);
        var plan = CreateMealPlanWithItem(tenant.TrainerId, tenant.ClientId, foodId);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.MealPlans.Add(plan);

        // Act
        await context.SaveChangesAsync(cancellationToken);

        // Assert
        Assert.True(await context.MealPlans
            .AnyAsync(value => value.Id == plan.Id, cancellationToken));
    }

    [Fact]
    public async Task SaveChanges_WhenFoodBelongsToAnotherTenant_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = await _fixture.SeedTenantWithClientAsync(
            $"owner-{Guid.NewGuid():N}", cancellationToken);
        var requester = await _fixture.SeedTenantWithClientAsync(
            $"requester-{Guid.NewGuid():N}", cancellationToken);
        var foodId = await SeedFoodAsync(owner.TrainerId, cancellationToken);
        var plan = CreateMealPlanWithItem(requester.TrainerId, requester.ClientId, foodId);

        await using var context = _fixture.CreateContext(requester.TrainerId);
        context.MealPlans.Add(plan);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task SaveChanges_WhenFoodIsDeleted_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedFoodAsync(tenant.TrainerId, cancellationToken, deleted: true);
        var plan = CreateMealPlanWithItem(tenant.TrainerId, tenant.ClientId, foodId);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.MealPlans.Add(plan);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task SaveChanges_WhenFoodDoesNotExist_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var plan = CreateMealPlanWithItem(tenant.TrainerId, tenant.ClientId, Guid.NewGuid());

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.MealPlans.Add(plan);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    // ---------- Exercise ----------

    [Fact]
    public async Task SaveChanges_WhenExerciseIsGlobal_PersistsPrescription()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var exerciseId = await SeedExerciseAsync(ownerTrainerId: null, cancellationToken);
        var (plan, day, prescription) = CreateTrainingPlanWithExercise(
            tenant.TrainerId, tenant.ClientId, exerciseId);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.TrainingPlans.Add(plan);
        context.TrainingPlanDays.Add(day);
        context.TrainingPlanDayExercises.Add(prescription);

        // Act
        await context.SaveChangesAsync(cancellationToken);

        // Assert
        Assert.True(await context.TrainingPlanDayExercises
            .AnyAsync(value => value.Id == prescription.Id, cancellationToken));
    }

    [Fact]
    public async Task SaveChanges_WhenExerciseBelongsToTenant_PersistsPrescription()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var exerciseId = await SeedExerciseAsync(tenant.TrainerId, cancellationToken);
        var (plan, day, prescription) = CreateTrainingPlanWithExercise(
            tenant.TrainerId, tenant.ClientId, exerciseId);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.TrainingPlans.Add(plan);
        context.TrainingPlanDays.Add(day);
        context.TrainingPlanDayExercises.Add(prescription);

        // Act
        await context.SaveChangesAsync(cancellationToken);

        // Assert
        Assert.True(await context.TrainingPlanDayExercises
            .AnyAsync(value => value.Id == prescription.Id, cancellationToken));
    }

    [Fact]
    public async Task SaveChanges_WhenExerciseBelongsToAnotherTenant_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = await _fixture.SeedTenantWithClientAsync(
            $"owner-{Guid.NewGuid():N}", cancellationToken);
        var requester = await _fixture.SeedTenantWithClientAsync(
            $"requester-{Guid.NewGuid():N}", cancellationToken);
        var exerciseId = await SeedExerciseAsync(owner.TrainerId, cancellationToken);
        var (plan, day, prescription) = CreateTrainingPlanWithExercise(
            requester.TrainerId, requester.ClientId, exerciseId);

        await using var context = _fixture.CreateContext(requester.TrainerId);
        context.TrainingPlans.Add(plan);
        context.TrainingPlanDays.Add(day);
        context.TrainingPlanDayExercises.Add(prescription);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task SaveChanges_WhenExerciseIsDeleted_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var exerciseId = await SeedExerciseAsync(tenant.TrainerId, cancellationToken, deleted: true);
        var (plan, day, prescription) = CreateTrainingPlanWithExercise(
            tenant.TrainerId, tenant.ClientId, exerciseId);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.TrainingPlans.Add(plan);
        context.TrainingPlanDays.Add(day);
        context.TrainingPlanDayExercises.Add(prescription);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task SaveChanges_WhenExerciseDoesNotExist_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var (plan, day, prescription) = CreateTrainingPlanWithExercise(
            tenant.TrainerId, tenant.ClientId, Guid.NewGuid());

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.TrainingPlans.Add(plan);
        context.TrainingPlanDays.Add(day);
        context.TrainingPlanDayExercises.Add(prescription);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    // ---------- Supplement ----------
    // Movidos de TenantWriteValidationInterceptorTests para fechar a matriz
    // simÃ©trica de catÃ¡logos neste ficheiro (ver doc 05, secÃ§Ã£o "Cobertura
    // simÃ©trica obrigatÃ³ria dos catÃ¡logos"). Os cenÃ¡rios de FK composta,
    // Ã­ndice Ãºnico de assignment ativo e persistÃªncia global/privada
    // continuam em Catalogs/ClientSupplementAssignmentTests.cs.

    [Fact]
    public async Task SaveChanges_WhenAssignmentReferencesArchivedSupplement_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplementId = await SeedSupplementAsync(tenant.TrainerId, cancellationToken, archived: true);
        var assignment = CreateAssignment(tenant, supplementId);

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.ClientSupplementAssignments.Add(assignment);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    [Fact]
    public async Task SaveChanges_WhenAssignmentReferencesMissingSupplement_ThrowsDomainException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var assignment = CreateAssignment(tenant, Guid.NewGuid());

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.ClientSupplementAssignments.Add(assignment);

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DomainException>(action);
    }

    // ---------- Helpers ----------

    private async Task<Guid> SeedFoodAsync(
        Guid? ownerTrainerId,
        CancellationToken cancellationToken,
        bool deleted = false)
    {
        var food = IntegrationTestData.Food(ownerTrainerId, Now);
        if (deleted)
            food.SoftDelete(Now.AddMinutes(1));

        await using var context = ownerTrainerId is null
            ? _fixture.CreateAdministrativeContext()
            : _fixture.CreateContext(ownerTrainerId);
        context.Foods.Add(food);
        await context.SaveChangesAsync(cancellationToken);

        return food.Id;
    }

    private async Task<Guid> SeedExerciseAsync(
        Guid? ownerTrainerId,
        CancellationToken cancellationToken,
        bool deleted = false)
    {
        var exercise = IntegrationTestData.Exercise(ownerTrainerId, Now);
        if (deleted)
            exercise.SoftDelete(Now.AddMinutes(1));

        await using var context = ownerTrainerId is null
            ? _fixture.CreateAdministrativeContext()
            : _fixture.CreateContext(ownerTrainerId);
        context.Exercises.Add(exercise);
        await context.SaveChangesAsync(cancellationToken);

        return exercise.Id;
    }

    private async Task<Guid> SeedSupplementAsync(
        Guid? ownerTrainerId,
        CancellationToken cancellationToken,
        bool archived = false)
    {
        var supplement = IntegrationTestData.Supplement(ownerTrainerId, Now);
        if (archived)
            supplement.Archive(Now.AddMinutes(1));

        await using var context = ownerTrainerId is null
            ? _fixture.CreateAdministrativeContext()
            : _fixture.CreateContext(ownerTrainerId);
        context.Supplements.Add(supplement);
        await context.SaveChangesAsync(cancellationToken);

        return supplement.Id;
    }

    private static MealPlan CreateMealPlanWithItem(Guid trainerId, Guid clientId, Guid foodId)
    {
        var plan = new MealPlan(
            trainerId,
            clientId,
            "Integration plan",
            null,
            new DateOnly(2026, 8, 5),
            null,
            CreateNutritionSnapshot(),
            Now);
        var meal = plan.AddMeal("Lunch", 1, Now);
        meal.AddItem(foodId, 100m, 1, Now);

        return plan;
    }

    private static (TrainingPlan Plan, TrainingPlanDay Day, TrainingPlanDayExercise Prescription)
        CreateTrainingPlanWithExercise(Guid trainerId, Guid clientId, Guid exerciseId)
    {
        var plan = new TrainingPlan(
            trainerId,
            clientId,
            "Integration plan",
            null,
            null,
            null,
            new DateOnly(2026, 8, 5),
            null,
            Now);
        var day = new TrainingPlanDay(plan.Id, 0, 1, null, Now);
        var prescription = new TrainingPlanDayExercise(
            day.Id,
            exerciseId,
            1,
            null,
            null,
            null,
            Now);

        return (plan, day, prescription);
    }

    private static NutritionCalculationSnapshot CreateNutritionSnapshot()
    {
        var macros = MacroTargetCalculator.CalculateFromManualGrams(
            2_000m,
            new ManualMacroInput(150m, 200m, 66.67m));

        return NutritionCalculationSnapshot.FromManualEnergy(80m, macros, Now);
    }

    private static ClientSupplementAssignment CreateAssignment(
        PostgresContainerFixture.TestTenantSeed tenant,
        Guid supplementId) =>
        new(
            tenant.TrainerId,
            tenant.ClientId,
            supplementId,
            "1 capsule",
            "With breakfast",
            null,
            Now.AddMinutes(5));
}
