using System.Text.RegularExpressions;
using Infrastructure.Data;
using Infrastructure.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Persistence.Common;

public sealed class GlobalSupplementReferenceQueryTranslationTests
{
    [Theory]
    [InlineData("UNION ALL")]
    [InlineData("meal_plan_meal_supplements")]
    [InlineData("client_supplement_assignments")]
    public void ReferenceCheck_WithNpgsqlProvider_ContainsRequiredSqlFragment(
        string expectedFragment)
    {
        var sql = BuildReferenceCheckSql();

        Assert.Contains(expectedFragment, sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReferenceCheck_WithNpgsqlProvider_UsesExactlyOneExists()
    {
        var sql = BuildReferenceCheckSql();

        Assert.Equal(1, Regex.Count(sql, @"\bEXISTS\b", RegexOptions.IgnoreCase));
    }

    private static string BuildReferenceCheckSql()
    {
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=translation_only")
            .Options;
        using var context = new PtManagerDbContext(options, TestTenantContext.Administrator());
        var supplementId = Guid.NewGuid();

        var references = context.MealPlanMealSupplements
            .IgnoreQueryFilters()
            .Where(item => item.SupplementId == supplementId)
            .Select(_ => 1)
            .Concat(context.ClientSupplementAssignments
                .IgnoreQueryFilters()
                .Where(item => item.SupplementId == supplementId)
                .Select(_ => 1));

        // Any fica na árvore para provar a tradução sem antecipar a execução PostgreSQL.
        return context.Supplements
            .IgnoreQueryFilters()
            .Where(item => item.Id == supplementId && references.Any())
            .Select(item => item.Id)
            .ToQueryString();
    }
}
