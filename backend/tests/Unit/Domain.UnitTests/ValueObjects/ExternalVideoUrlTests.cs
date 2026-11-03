using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.ValueObjects;

/// <summary>Só HTTPS do YouTube e do Vimeo, com o host exato, chega ao cliente.</summary>
public sealed class ExternalVideoUrlTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://vimeo.com/76979871")]
    public void OrNull_AllowedLink_IsKept(string value)
    {
        Assert.True(ExternalVideoUrl.IsAllowed(value));
        Assert.Equal(value, ExternalVideoUrl.OrNull(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://www.youtube.com/watch?v=x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://youtube.com.evil.example/x")]
    [InlineData("https://www.youtube.com@evil.example/x")]
    [InlineData("https://www.youtube.com:8443/x")]
    [InlineData("https://videos.example/agachamento")]
    public void OrNull_AnythingElse_IsDropped(string? value)
    {
        Assert.False(ExternalVideoUrl.IsAllowed(value));
        Assert.Null(ExternalVideoUrl.OrNull(value));
    }

    [Fact]
    public void IsAllowed_TooLong_IsRefused()
    {
        Assert.False(ExternalVideoUrl.IsAllowed("https://youtu.be/" + new string('a', 490)));
    }
}
