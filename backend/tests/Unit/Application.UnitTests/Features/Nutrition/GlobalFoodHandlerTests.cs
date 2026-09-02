using Application.Common.Abstractions;
using Application.Features.Nutrition;
using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Features.Nutrition.Foods.ListGlobalFoods;
using Application.Pagination;
using Application.Results;

namespace Application.UnitTests.Features.Nutrition;

public sealed class GlobalFoodHandlerTests
{
    [Fact]
    public async Task ListGlobalFoods_WithoutAdministrativeContext_ReturnsForbidden()
    {
        var handler = new ListGlobalFoodsHandler(
            new ListGlobalFoodsQueryValidator(),
            new TestTenantContext("superuser", isAdministrative: false),
            new EmptyGlobalFoodQueries());

        var result = await handler.HandleAsync(
            new ListGlobalFoodsQuery(null, GlobalFoodActivityFilter.Active, 1, 50),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(NutritionErrors.AdministratorOnly.Code, result.Error!.Code);
    }

    private sealed class TestTenantContext(string? role, bool isAdministrative) : ITenantContext
    {
        public Guid? TrainerId => null;
        public Guid? UserId => Guid.NewGuid();
        public string? Role { get; } = role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative { get; } = isAdministrative;
    }

    private sealed class EmptyGlobalFoodQueries : IGlobalFoodQueries
    {
        public Task<GlobalFoodDto?> GetAsync(Guid foodId, CancellationToken cancellationToken) =>
            Task.FromResult<GlobalFoodDto?>(null);

        public Task<PageResult<GlobalFoodDto>> ListAsync(
            string? search,
            GlobalFoodActivityFilter activity,
            PageRequest page,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PageResult<GlobalFoodDto>([], 0));
    }
}
