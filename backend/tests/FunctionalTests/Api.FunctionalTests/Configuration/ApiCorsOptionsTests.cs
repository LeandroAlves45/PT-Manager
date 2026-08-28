using Api.Configuration;

namespace Api.FunctionalTests.Configuration;

public sealed class ApiCorsOptionsTests
{
    [Fact]
    public void HasValidOrigins_RejectsEmptyAllowlist()
    {
        var options = new ApiCorsOptions();

        Assert.False(options.HasValidOrigins());
    }

    [Theory]
    [InlineData("http://frontend.test")]
    [InlineData("https://*.frontend.test")]
    [InlineData("https://frontend.test/path")]
    [InlineData("https://frontend.test?preview=true")]
    [InlineData("https://frontend.test#fragment")]
    [InlineData("https://user:secret@frontend.test")]
    [InlineData("/relative")]
    [InlineData("not a uri")]
    [InlineData("")]
    public void HasValidOrigins_RejectsUnsafeOrigin(string origin)
    {
        var options = new ApiCorsOptions { AllowedOrigins = [origin] };

        Assert.False(options.HasValidOrigins());
    }

    [Fact]
    public void HasValidOrigins_RejectsDuplicateOriginsIgnoringCase()
    {
        var options = new ApiCorsOptions
        {
            AllowedOrigins = ["https://app.example.test", "https://APP.example.test"]
        };

        Assert.False(options.HasValidOrigins());
    }

    [Fact]
    public void HasValidOrigins_RejectsAllowlistWithOneInvalidEntry()
    {
        var options = new ApiCorsOptions
        {
            AllowedOrigins = ["https://app.example.test", "http://app.example.test"]
        };

        Assert.False(options.HasValidOrigins());
    }

    [Fact]
    public void HasValidOrigins_AcceptsExactHttpsOrigins()
    {
        var options = new ApiCorsOptions
        {
            AllowedOrigins = ["https://app.example.test", "https://preview.example.test"]
        };

        Assert.True(options.HasValidOrigins());
    }

    [Fact]
    public void HasValidOrigins_AcceptsExplicitPort()
    {
        var options = new ApiCorsOptions { AllowedOrigins = ["https://app.example.test:8443"] };

        Assert.True(options.HasValidOrigins());
    }
}
