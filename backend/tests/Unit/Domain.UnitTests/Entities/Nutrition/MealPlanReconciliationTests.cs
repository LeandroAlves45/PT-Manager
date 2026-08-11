using Domain.Entities.Nutrition;
using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities.Nutrition;

public sealed class MealPlanReconciliationTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void UpdateDetails_PreservesClientAndCalculation()
    {
        var plan = CreatePlan();
        var originalClientId = plan.ClientId;
        var originalSnapshot = plan.CalculationSnapshot;

        plan.UpdateDetails("Updated", "Notes", new DateOnly(2026, 9, 1), null, Now.AddHours(1));

        Assert.Equal(originalClientId, plan.ClientId);
        Assert.Same(originalSnapshot, plan.CalculationSnapshot);
    }

    [Fact]
    public void ReorderMeals_SwapsOrdersWithoutReplacingMeals()
    {
        var plan = CreatePlan();
        var first = plan.AddMeal("Breakfast", 1, Now);
        var second = plan.AddMeal("Lunch", 2, Now);

        plan.ReorderMeals(
            new Dictionary<Guid, int> { [first.Id] = 2, [second.Id] = 1 },
            Now.AddMinutes(1));

        Assert.Equal(2, first.OrderNumber);
        Assert.Equal(1, second.OrderNumber);
    }

    [Fact]
    public void ReorderMeals_DuplicateFinalOrder_ThrowsBeforeMutation()
    {
        var plan = CreatePlan();
        var first = plan.AddMeal("Breakfast", 1, Now);
        var second = plan.AddMeal("Lunch", 2, Now);

        Assert.Throws<DomainException>(() => plan.ReorderMeals(
            new Dictionary<Guid, int> { [first.Id] = 1, [second.Id] = 1 },
            Now.AddMinutes(1)));
        Assert.Equal(1, first.OrderNumber);
        Assert.Equal(2, second.OrderNumber);
    }

    [Fact]
    public void UpdateItem_ChangesEditableValuesAndPreservesIdentity()
    {
        var meal = CreatePlan().AddMeal("Lunch", 1, Now);
        var item = meal.AddItem(Guid.NewGuid(), 100m, 1, Now);
        var originalId = item.Id;
        var replacementFoodId = Guid.NewGuid();

        meal.UpdateItem(item.Id, replacementFoodId, 125m, 2, Now.AddMinutes(1));

        Assert.Equal(originalId, item.Id);
        Assert.Equal(replacementFoodId, item.FoodId);
        Assert.Equal(125m, item.QuantityInGrams);
        Assert.Equal(2, item.OrderNumber);
    }

    [Fact]
    public void UpdateSupplement_DuplicateReference_ThrowsBeforeMutation()
    {
        var meal = CreatePlan().AddMeal("Lunch", 1, Now);
        var existingSupplementId = Guid.NewGuid();
        meal.AddSupplement(existingSupplementId, null, 5m, 1, Now);
        var candidate = meal.AddSupplement(Guid.NewGuid(), "Before", 2m, 2, Now);
        var originalReference = candidate.SupplementId;

        Assert.Throws<DomainException>(() => meal.UpdateSupplement(
            candidate.Id,
            existingSupplementId,
            "Changed",
            3m,
            2,
            Now.AddMinutes(1)));
        Assert.Equal(originalReference, candidate.SupplementId);
        Assert.Equal("Before", candidate.Notes);
    }

    [Fact]
    public void ArchiveAndReactivate_RepeatedCallsOnlyChangeTimestampOncePerTransition()
    {
        var plan = CreatePlan();
        var archiveTime = Now.AddMinutes(1);
        var reactivateTime = Now.AddMinutes(3);

        Assert.True(plan.Archive(archiveTime));
        Assert.False(plan.Archive(archiveTime.AddMinutes(1)));
        Assert.Equal(archiveTime, plan.UpdatedAt);
        Assert.True(plan.Reactivate(reactivateTime));
        Assert.False(plan.Reactivate(reactivateTime.AddMinutes(1)));
        Assert.Equal(reactivateTime, plan.UpdatedAt);
    }

    private static MealPlan CreatePlan() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Plan",
        null,
        new DateOnly(2026, 8, 10),
        null,
        CreateSnapshot(),
        Now);

    private static NutritionCalculationSnapshot CreateSnapshot()
    {
        var macros = MacroTargetCalculator.CalculateFromPercentage(
            2000m,
            new PercentageMacroInput(30m, 40m, 30m));

        return NutritionCalculationSnapshot.FromManualEnergy(80m, macros, Now);
    }
}
