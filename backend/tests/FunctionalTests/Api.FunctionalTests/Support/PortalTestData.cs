using Application.Common.Abstractions;
using Domain.Entities.Assessments;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Support;

/// <summary>
/// Semeia o estado mínimo do portal do cliente: utilizador associado, planos activos,
/// check-in devido e atribuição de suplemento.
/// </summary>
internal static class PortalTestData
{
    /// <summary>Campos internos usados nos testes de exposição.</summary>
    internal const string SecretObjective = "Internal personal trainer objective";
    internal const string SecretNotes = "Internal personal trainer notes";
    internal const string SecretMedicalConditions = "Type 2 diabetes";

    /// <summary>
    /// Cria ou reutiliza um tenant com cliente autenticado e dados completos do portal.
    /// </summary>
    internal static async Task<(Guid TrainerId, Guid ClientUserId)> SeedActiveClientAsync(
        ApiWebApplicationFactory factory,
        CancellationToken cancellationToken,
        Guid? trainerId = null)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (trainerId.HasValue)
            return (trainerId.Value, await SeedSecondClientSameTrainerAsync(
                factory, trainerId.Value, cancellationToken));

        var now = TrainerTenantSeeder.SeedInstant;
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            factory,
            $"portal-{Guid.NewGuid():N}",
            cancellationToken);

        var clientUserId = await SeedClientUserAsync(factory, cancellationToken);
        var clientId = await SeedLinkedClientAsync(
            factory,
            trainer.TrainerId,
            clientUserId,
            cancellationToken);

        await SeedTrainingPlanAsync(
            factory, trainer.TrainerId, clientId, cancellationToken);
        await SeedMealPlanAsync(
            factory, trainer.TrainerId, clientId, cancellationToken);
        await SeedDueCheckInAsync(
            factory, trainer.TrainerId, clientId, cancellationToken);
        await SeedInitialAssessmentAsync(
            factory, trainer.TrainerId, clientId, cancellationToken);
        await SeedSupplementAssignmentAsync(
            factory, trainer.TrainerId, clientUserId, cancellationToken);

        return (trainer.TrainerId, clientUserId);
    }

    /// <summary>Cria uma atribuição activa de suplemento para o cliente indicado.</summary>
    internal static async Task<Guid> SeedSupplementAssignmentAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var now = TrainerTenantSeeder.SeedInstant;

        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var client = await context.Clients.SingleAsync(
            item => item.OwnerTrainerId == trainerId && item.UserId == clientUserId,
            cancellationToken);

        var supplement = new Supplement(
            trainerId,
            trainerId,
            "Portal creatine",
            null,
            "grams",
            "5 g",
            "Daily",
            null,
            now);

        context.Supplements.Add(supplement);

        var assignment = new ClientSupplementAssignment(
            trainerId,
            client.Id,
            supplement.Id,
            "5 g",
            "After workout",
            "Take with water",
            now);

        context.ClientSupplementAssignments.Add(assignment);
        await context.SaveChangesAsync(cancellationToken);

        return assignment.Id;
    }

    /// <summary>Segundo cliente do mesmo personal trainer, para testes de titularidade.</summary>
    internal static async Task<Guid> SeedSecondClientSameTrainerAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var clientUserId = await SeedClientUserAsync(factory, cancellationToken);
        await SeedLinkedClientAsync(
            factory,
            trainerId,
            clientUserId,
            cancellationToken,
            name: "Second portal client");

        return clientUserId;
    }

    /// <summary>Bloqueia o exercício do plano activo do cliente.</summary>
    internal static async Task BlockExerciseInActivePlanAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var now = TrainerTenantSeeder.SeedInstant;

        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var client = await context.Clients.SingleAsync(
            item => item.OwnerTrainerId == trainerId && item.UserId == clientUserId,
            cancellationToken);

        var exerciseId = await (
            from plan in context.TrainingPlans
            join day in context.TrainingPlanDays on plan.Id equals day.TrainingPlanId
            join item in context.TrainingPlanDayExercises on day.Id equals item.TrainingPlanDayId
            where plan.OwnerTrainerId == trainerId &&
                plan.ClientId == client.Id &&
                plan.IsActive
            select item.ExerciseId).FirstAsync(cancellationToken);

        var exercise = await context.Exercises.FindAsync([exerciseId], cancellationToken)
            ?? throw new InvalidOperationException("Exercise seed missing.");

        exercise.Block(PlatformEnforcementReason.MaliciousContent, now);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Bloqueia o alimento do plano alimentar activo do cliente.</summary>
    internal static async Task BlockFoodInActiveMealPlanAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var now = TrainerTenantSeeder.SeedInstant;

        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var client = await context.Clients.SingleAsync(
            item => item.OwnerTrainerId == trainerId && item.UserId == clientUserId,
            cancellationToken);

        var foodId = await (
            from plan in context.MealPlans
            join meal in context.MealPlanMeals on plan.Id equals meal.MealPlanId
            join item in context.MealPlanMealItems on meal.Id equals item.MealPlanMealId
            where plan.OwnerTrainerId == trainerId &&
                plan.ClientId == client.Id &&
                plan.IsActive
            select item.FoodId).FirstAsync(cancellationToken);

        var food = await context.Foods.FindAsync([foodId], cancellationToken)
            ?? throw new InvalidOperationException("Food seed missing.");

        food.Block(PlatformEnforcementReason.MaliciousContent, now);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Guid> SeedClientUserAsync(
        ApiWebApplicationFactory factory,
        CancellationToken cancellationToken)
    {
        var now = TrainerTenantSeeder.SeedInstant;

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var user = new User(
            new EmailAddress($"client-{Guid.NewGuid():N}@example.test"),
            "client",
            "Portal Client",
            now);
        user.SetPasswordHash("functional-test-password-hash", now);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }

    private static async Task<Guid> SeedLinkedClientAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken,
        string name = "Portal client")
    {
        var now = TrainerTenantSeeder.SeedInstant;

        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var client = new Client(
            trainerId,
            name,
            $"client-{Guid.NewGuid():N}@example.test",
            $"+3519{Random.Shared.Next(10_000_000, 99_999_999)}",
            BirthDate.Create(new DateOnly(1992, 5, 15), DateOnly.FromDateTime(now)),
            BiologicalSex.Female,
            SecretObjective,
            SecretNotes,
            "Emergency contact",
            $"+3519{Random.Shared.Next(10_000_000, 99_999_999)}",
            now);

        client.AttachUser(clientUserId, now);
        context.Clients.Add(client);

        var subscription = await context.TrainerSubscriptions
            .SingleAsync(item => item.TrainerId == trainerId, cancellationToken);
        subscription.RegisterClientAdded(now);

        await context.SaveChangesAsync(cancellationToken);

        return client.Id;
    }

    private static async Task SeedTrainingPlanAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var now = TrainerTenantSeeder.SeedInstant;
        var exerciseId = await TrainingTestData.SeedPrivateExerciseAsync(
            factory,
            trainerId,
            "Portal squat",
            cancellationToken);

        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var plan = new TrainingPlan(
            trainerId,
            clientId,
            "Portal plan",
            "Portal plan description",
            "strength",
            null,
            DateOnly.FromDateTime(now),
            null,
            now);

        var day = plan.AddDay(dayOfWeek: 1, weekNumber: 1, notes: null, now);
        var prescription = day.AddExercise(exerciseId, 1, null, null, null, now);
        prescription.AddSet(1, 10, 60m, 60, 90, now);

        context.TrainingPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedMealPlanAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var now = TrainerTenantSeeder.SeedInstant;

        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var food = new Food(
            trainerId,
            "Portal rice",
            null,
            7.5m,
            78m,
            0.6m,
            1.4m,
            now);
        var supplement = new Supplement(
            trainerId,
            trainerId,
            "Portal whey",
            null,
            "grams",
            "30 g",
            "Breakfast",
            null,
            now);

        context.Foods.Add(food);
        context.Supplements.Add(supplement);

        var macros = MacroTargetCalculator.CalculateFromManualGrams(
            2_000m,
            new ManualMacroInput(150m, 200m, 66.67m));
        var snapshot = NutritionCalculationSnapshot.FromManualEnergy(80m, macros, now);
        var plan = new MealPlan(
            trainerId,
            clientId,
            "Portal meal plan",
            null,
            DateOnly.FromDateTime(now),
            null,
            snapshot,
            now);

        var meal = plan.AddMeal("Lunch", 1, now);
        meal.AddItem(food.Id, 150m, 1, now);
        meal.AddSupplement(supplement.Id, null, 30m, 1, now);

        context.MealPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDueCheckInAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var now = TrainerTenantSeeder.SeedInstant;
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var checkIn = new CheckIn(
            trainerId,
            clientId,
            dueDate,
            dueDate.AddDays(14),
            now);

        context.CheckIns.Add(checkIn);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedInitialAssessmentAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var now = TrainerTenantSeeder.SeedInstant;

        await using var scope = CreateTrainerScope(factory, trainerId);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var assessment = new InitialAssessment(
            trainerId,
            clientId,
            weightKg: 72m,
            heightCm: 175,
            bodyFatPercentage: 18m,
            medicalConditions: SecretMedicalConditions,
            fitnessLevel: "intermediate",
            activityLevel: ActivityLevel.ModeratelyActive,
            goals: "Gain muscle mass",
            profession: "Engineer",
            bodyMeasurements: null,
            nutritionIntake: null,
            now);

        context.InitialAssessments.Add(assessment);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static AsyncServiceScope CreateTrainerScope(
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
