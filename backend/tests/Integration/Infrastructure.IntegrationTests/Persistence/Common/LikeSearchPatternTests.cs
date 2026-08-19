using Infrastructure.Persistence.Common;

namespace Infrastructure.IntegrationTests.Persistence.Common;

public sealed class LikeSearchPatternTests
{
    [Fact]
    public void Build_WhenSearchHasOuterWhitespace_TrimsAndWrapsWithWildcards()
    {
        var pattern = LikeSearchPattern.Build("  creatine  ");

        Assert.Equal("%creatine%", pattern);
    }

    [Fact]
    public void Build_WhenSearchContainsLikeMetacharacters_EscapesEveryMetacharacter()
    {
        var pattern = LikeSearchPattern.Build(@"100%_pure\dose");

        Assert.Equal(@"%100\%\_pure\\dose%", pattern);
    }
}
