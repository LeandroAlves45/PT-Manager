using Domain.Entities.Jobs;
using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.Data;

namespace Infrastructure.IntegrationTests.Support;

/// <summary>Cria entidades válidas e pequenas para testes de integração.</summary>
internal static class IntegrationTestData
{
    public static DurableJob Job(
        DateTime scheduledAt, DateTime now, string? idempotencyKey = null) => new(
            null, "integration_test", 1, "{\"value\":1}",
            idempotencyKey ?? Guid.NewGuid().ToString("N"), Guid.NewGuid(),
            scheduledAt, now);

    public static OutboxMessage Message(
        DateTime now, string? idempotencyKey = null) => new(
            null, "integration_test", "{\"value\":1}",
            idempotencyKey ?? Guid.NewGuid().ToString("N"), Guid.NewGuid(), now);

    public static Food Food(Guid? ownerTrainerId, DateTime now) =>
        new(ownerTrainerId, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, now);

    public static Exercise Exercise(Guid? ownerTrainerId, DateTime now) =>
        new(ownerTrainerId, "Squat", null, "legs", "barbell", "medium", null, now);

    public static Supplement Supplement(
        Guid? ownerTrainerId, DateTime now, Guid? createdByUserId = null) => new(
            ownerTrainerId, createdByUserId ?? ownerTrainerId ?? Guid.NewGuid(),
            "Creatine", null, "grams", "5 g", "Daily", null, now);

    public static async Task SeedMealPlanReferencingFoodAsync(
        PtManagerDbContext context,
        Guid trainerId,
        Guid clientId,
        Guid foodId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var macros = MacroTargetCalculator.CalculateFromManualGrams(
            2_000m,
            new ManualMacroInput(150m, 200m, 66.67m));
        var snapshot = NutritionCalculationSnapshot.FromManualEnergy(80m, macros, now);
        var plan = new MealPlan(
            trainerId,
            clientId,
            "Referenced food plan",
            null,
            DateOnly.FromDateTime(now),
            null,
            snapshot,
            now);
        var meal = plan.AddMeal("Lunch", 1, now);
        meal.AddItem(foodId, 100m, 1, now);

        context.MealPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task SeedTrainingPlanReferencingExerciseAsync(
        PtManagerDbContext context,
        Guid trainerId,
        Guid clientId,
        Guid exerciseId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var plan = new TrainingPlan(
            trainerId,
            clientId,
            "Referenced exercise plan",
            null,
            null,
            null,
            DateOnly.FromDateTime(now),
            null,
            now);
        var day = plan.AddDay(1, 1, null, now);
        day.AddExercise(exerciseId, 1, null, null, null, now);

        context.TrainingPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
    }
}
