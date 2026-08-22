using Application.Features.Nutrition;
using Application.Features.Nutrition.Foods;
using Application.Features.Nutrition.Foods.Abstractions;
using Domain.Entities.Nutrition;

namespace Application.UnitTests.Features.Nutrition;

public sealed class GlobalFoodMappingsTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToDtoResult_WhenCreated_ReturnsGlobalProjection()
    {
        var food = new Food(null, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, Now);
        var outcome = GlobalFoodStoreResult.WithFood(GlobalFoodStoreResult.Status.Created, food);

        var result = outcome.ToDtoResult();

        Assert.Equal("Rice", result.Value.Name);
    }

    [Theory]
    [InlineData(GlobalFoodStoreResult.Status.NotFound, "food_not_found")]
    [InlineData(GlobalFoodStoreResult.Status.Inactive, "food_inactive")]
    [InlineData(GlobalFoodStoreResult.Status.Referenced, "global_food_referenced")]
    public void ToDtoResult_WhenStoreRejectsMutation_ReturnsFeatureError(
        GlobalFoodStoreResult.Status status, string expectedCode)
    {
        var outcome = GlobalFoodStoreResult.For(status);

        var result = outcome.ToDtoResult();

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public void ToTransitionResult_WhenHasReferences_ReturnsDeleteConflict()
    {
        var outcome = GlobalFoodStoreResult.For(GlobalFoodStoreResult.Status.HasReferences);

        var result = outcome.ToTransitionResult();

        Assert.Equal(NutritionErrors.GlobalFoodHasReferences.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData(GlobalFoodStoreResult.Status.Changed)]
    [InlineData(GlobalFoodStoreResult.Status.Deleted)]
    [InlineData(GlobalFoodStoreResult.Status.AlreadyInRequestedState)]
    public void ToTransitionResult_WhenTransitionSucceeded_ReturnsSuccess(
        GlobalFoodStoreResult.Status status)
    {
        var result = GlobalFoodStoreResult.For(status).ToTransitionResult();

        Assert.True(result.IsSuccess);
    }
}
