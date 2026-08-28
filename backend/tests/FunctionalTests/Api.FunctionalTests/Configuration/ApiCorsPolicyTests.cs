using Api.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.FunctionalTests.Configuration;

public sealed class ApiCorsPolicyTests
{
    [Fact]
    public void AddApiCors_BuildsPolicyWithExactOriginsAndCredentials()
    {
        var provider = BuildProvider("https://app.example.test");

        var policy = provider.GetRequiredService<IOptions<CorsOptions>>()
            .Value.GetPolicy(ApiCorsPolicy.PolicyName);

        Assert.NotNull(policy);
        Assert.Equal(["https://app.example.test"], policy.Origins);
        Assert.True(policy.SupportsCredentials);
        Assert.False(policy.AllowAnyOrigin);
        Assert.False(policy.AllowAnyHeader);
        Assert.False(policy.AllowAnyMethod);
    }

    [Fact]
    public void AddApiCors_FailsClosedWhenAllowlistIsEmpty()
    {
        var provider = BuildProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ApiCorsOptions>>().Value);
    }

    [Fact]
    public void AddApiCors_FailsClosedWhenOriginIsNotHttps()
    {
        var provider = BuildProvider("http://app.example.test");

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<CorsOptions>>()
                .Value.GetPolicy(ApiCorsPolicy.PolicyName));
    }

    private static ServiceProvider BuildProvider(params string[] origins)
    {
        var settings = origins
            .Select((origin, index) =>
                new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin))
            .ToList();

        // Mantém a secção presente mesmo sem origens, para provar a allowlist vazia.
        settings.Add(new KeyValuePair<string, string?>("Cors:Enabled", "true"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new ServiceCollection()
            .AddApiCors(configuration)
            .BuildServiceProvider();
    }
}
