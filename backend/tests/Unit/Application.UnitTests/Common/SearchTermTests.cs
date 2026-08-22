using Application.Common;

namespace Application.UnitTests.Common;

public sealed class SearchTermTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_WhenBlank_ReturnsNull(string? input)
    {
        var result = SearchTerm.Normalize(input);

        Assert.Null(result);
    }

    [Fact]
    public void Normalize_WhenTextHasOuterWhitespace_ReturnsTrimmedText()
    {
        var result = SearchTerm.Normalize("  chicken  ");

        Assert.Equal("chicken", result);
    }
}
