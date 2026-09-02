using Application.Common.Abstractions;
using Application.Features.Nutrition.Foods.Abstractions;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Support;

/// <summary>
/// Semeia o mínimo necessário para os testes de Nutrition, sem depender de endpoints
/// que os próprios testes estão a provar.
/// </summary>
internal static class NutritionTestData
{
    internal sealed record MealPlanCatalogSeed(
        Guid TrainerId,
        Guid ClientId,
        Guid FoodId,
        Guid SupplementId);

    internal sealed record SeededMealPlan(
        Guid TrainerId,
        Guid ClientId,
        Guid FoodId,
        Guid SupplementId,
        Guid MealPlanId);

    internal static Task<SeededTrainer> SeedTrainerAsync(
        ApiWebApplicationFactory factory,
        CancellationToken cancellationToken) =>
        TrainerTenantSeeder.SeedTrainerAsync(
            factory,
            $"nutrition-{Guid.NewGuid():N}",
            cancellationToken);

    internal static Task<Guid> SeedSuperuserAsync(
        ApiWebApplicationFactory factory,
        CancellationToken cancellationToken) =>
        SeedUserAsync(factory, "superuser", cancellationToken);

    internal static async Task<MealPlanCatalogSeed> SeedMealPlanCatalogAsync(
        ApiWebApplicationFactory factory,
        CancellationToken cancellationToken)
    {
        var trainer = await SeedTrainerAsync(factory, cancellationToken);
        var now = TrainerTenantSeeder.SeedInstant;

        Guid clientId;
        Guid foodId;
        Guid supplementId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            EstablishTenant(
                scope.ServiceProvider,
                trainer.TrainerId,
                trainer.TrainerId,
                "trainer",
                false);
            var dbContext = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

            var client = new Client(
                trainer.TrainerId,
                "Cliente Meal Plan",
                null,
                "+351900000001",
                BirthDate.Create(new DateOnly(1990, 1, 1), DateOnly.FromDateTime(now)),
                BiologicalSex.Female,
                null,
                null,
                null,
                null,
                now);
            var food = new Food(
                trainer.TrainerId,
                "Arroz teste",
                null,
                7.5m,
                78m,
                0.6m,
                1.4m,
                now);
            var supplement = new Supplement(
                trainer.TrainerId,
                trainer.TrainerId,
                "Creatina",
                null,
                "grams",
                "5 g",
                "Daily",
                null,
                now);

            dbContext.Clients.Add(client);
            dbContext.Foods.Add(food);
            dbContext.Supplements.Add(supplement);
            await dbContext.SaveChangesAsync(cancellationToken);

            clientId = client.Id;
            foodId = food.Id;
            supplementId = supplement.Id;
        }

        return new MealPlanCatalogSeed(
            trainer.TrainerId,
            clientId,
            foodId,
            supplementId);
    }

    internal static async Task<SeededMealPlan> SeedMealPlanAsync(
        ApiWebApplicationFactory factory,
        CancellationToken cancellationToken,
        int mealCount = 2)
    {
        var catalog = await SeedMealPlanCatalogAsync(factory, cancellationToken);
        var now = TrainerTenantSeeder.SeedInstant;

        Guid mealPlanId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            EstablishTenant(
                scope.ServiceProvider,
                catalog.TrainerId,
                catalog.TrainerId,
                "trainer",
                false);
            var dbContext = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
            var macros = MacroTargetCalculator.CalculateFromManualGrams(
                2_000m,
                new ManualMacroInput(150m, 200m, 66.67m));
            var snapshot = NutritionCalculationSnapshot.FromManualEnergy(80m, macros, now);
            var plan = new MealPlan(
                catalog.TrainerId,
                catalog.ClientId,
                "Plano semeado",
                null,
                new DateOnly(2026, 8, 10),
                null,
                snapshot,
                now);

            for (var order = 1; order <= mealCount; order++)
            {
                var meal = plan.AddMeal($"Refeição {order}", order, now);
                meal.AddItem(catalog.FoodId, 100m + order, 1, now);
                meal.AddSupplement(catalog.SupplementId, null, 5m, 1, now);
            }

            dbContext.MealPlans.Add(plan);
            await dbContext.SaveChangesAsync(cancellationToken);
            mealPlanId = plan.Id;
        }

        return new SeededMealPlan(
            catalog.TrainerId,
            catalog.ClientId,
            catalog.FoodId,
            catalog.SupplementId,
            mealPlanId);
    }

    internal static async Task<Guid> SeedInactiveFoodAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var now = TrainerTenantSeeder.SeedInstant;

        await using var scope = factory.Services.CreateAsyncScope();
        EstablishTenant(scope.ServiceProvider, trainerId, trainerId, "trainer", false);
        var dbContext = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var food = new Food(
            trainerId,
            "Alimento inactivo",
            null,
            10m,
            10m,
            5m,
            null,
            now);
        food.SetActive(false, now);

        dbContext.Foods.Add(food);
        await dbContext.SaveChangesAsync(cancellationToken);

        return food.Id;
    }

    internal static async Task<Guid> SeedPrivateFoodAsync(
        ApiWebApplicationFactory factory,
        Guid trainerId,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        EstablishTenant(scope.ServiceProvider, trainerId, trainerId, "trainer", false);
        var dbContext = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var food = new Food(
            ownerTrainerId: trainerId,
            name: name,
            description: null,
            protein: 20m,
            carbs: 10m,
            fats: 5m,
            fiber: null,
            now: TrainerTenantSeeder.SeedInstant);

        dbContext.Add(food);
        await dbContext.SaveChangesAsync(cancellationToken);

        return food.Id;
    }

    internal static async Task<ReferencedGlobalFoodSeed> SeedReferencedGlobalFoodAsync(
        ApiWebApplicationFactory factory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var now = TrainerTenantSeeder.SeedInstant;
        var trainer = await SeedTrainerAsync(factory, cancellationToken);
        var superuserId = await SeedUserAsync(factory, "superuser", cancellationToken);

        Guid clientId;
        await using (var trainerScope = factory.Services.CreateAsyncScope())
        {
            EstablishTenant(
                trainerScope.ServiceProvider,
                trainer.TrainerId,
                trainer.TrainerId,
                "trainer",
                false);
            var dbContext = trainerScope.ServiceProvider
                .GetRequiredService<PtManagerDbContext>();
            var client = new Client(
                trainer.TrainerId,
                "Cliente Nutrition",
                null,
                "+351900000000",
                BirthDate.Create(new DateOnly(1995, 1, 1), DateOnly.FromDateTime(now)),
                BiologicalSex.Male,
                null,
                null,
                null,
                null,
                now);

            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync(cancellationToken);
            clientId = client.Id;
        }

        Guid foodId;
        await using (var adminScope = factory.Services.CreateAsyncScope())
        {
            EstablishTenant(
                adminScope.ServiceProvider,
                null,
                superuserId,
                "superuser",
                true);
            var store = adminScope.ServiceProvider.GetRequiredService<IGlobalFoodStore>();
            var created = await store.CreateAsync(
                superuserId,
                "Alimento global referenciado",
                null,
                20m,
                10m,
                5m,
                null,
                now,
                cancellationToken);

            foodId = created.Food?.Id
                ?? throw new InvalidOperationException("Global food seed failed.");
        }

        await using (var trainerScope = factory.Services.CreateAsyncScope())
        {
            EstablishTenant(
                trainerScope.ServiceProvider,
                trainer.TrainerId,
                trainer.TrainerId,
                "trainer",
                false);
            var dbContext = trainerScope.ServiceProvider
                .GetRequiredService<PtManagerDbContext>();
            var macros = MacroTargetCalculator.CalculateFromManualGrams(
                2_000m,
                new ManualMacroInput(150m, 200m, 66.67m));
            var snapshot = NutritionCalculationSnapshot.FromManualEnergy(80m, macros, now);
            var plan = new MealPlan(
                trainer.TrainerId,
                clientId,
                "Plano com alimento global",
                null,
                DateOnly.FromDateTime(now),
                null,
                snapshot,
                now);
            var meal = plan.AddMeal("Almoço", 1, now);
            meal.AddItem(foodId, 100m, 1, now);

            dbContext.MealPlans.Add(plan);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ReferencedGlobalFoodSeed(foodId, superuserId);
    }

    private static async Task<Guid> SeedUserAsync(
        ApiWebApplicationFactory factory,
        string role,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var user = new User(
            new EmailAddress($"{role}-{Guid.NewGuid():N}@example.test"),
            role,
            "Nutrition Test",
            TrainerTenantSeeder.SeedInstant);

        user.SetPasswordHash("functional-test-password-hash", TrainerTenantSeeder.SeedInstant);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    private static void EstablishTenant(
        IServiceProvider services,
        Guid? trainerId,
        Guid userId,
        string role,
        bool isAdministrative) =>
        services.GetRequiredService<ITenantContextInitializer>().Establish(
            trainerId,
            userId,
            role,
            TenantOrigin.Http,
            isAdministrative);
}

/// <summary>Identidades necessárias para exercer a eliminação administrativa.</summary>
internal sealed record ReferencedGlobalFoodSeed(Guid FoodId, Guid SuperuserId);
