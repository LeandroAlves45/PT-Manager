using Application.Features.Clients.ListClients;
using Xunit;

namespace Application.UnitTests.Pagination;

/// <summary>
/// Verifica os limites partilhados através de um validator público que aplica
/// PaginationValidationRules.
/// </summary>
public sealed class PaginationValidationRulesTests
{
    private static readonly ListClientsQueryValidator Validator = new();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 50)]
    [InlineData(1, 100)]
    public void ValidBounds_Pass(int pageNumber, int pageSize)
    {
        var query = new ListClientsQuery(null, ClientActivityFilter.Active, pageNumber, pageSize);

        var result = Validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidPageNumber_ReturnsStableCode(int pageNumber)
    {
        var query = new ListClientsQuery(null, ClientActivityFilter.Active, pageNumber, 50);

        var result = Validator.Validate(query);

        Assert.Contains(result.Errors, failure => failure.ErrorCode == "page_number_invalid");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void InvalidPageSize_ReturnsStableCode(int pageSize)
    {
        var query = new ListClientsQuery(null, ClientActivityFilter.Active, 1, pageSize);

        var result = Validator.Validate(query);

        Assert.Contains(result.Errors, failure => failure.ErrorCode == "page_size_invalid");
    }
}
