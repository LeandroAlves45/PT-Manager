using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.FunctionalTests.Configuration;

public sealed class ApiJsonContractTests
{
    private sealed record SamplePayload(Guid TrainerId, string DisplayName, SampleState State);

    private enum SampleState
    {
        AwaitingReview
    }

    [Fact]
    public void AddApi_SerializesPropertiesAndEnumsInSnakeCase()
    {
        var options = BuildProvider()
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;

        var json = JsonSerializer.Serialize(
            new SamplePayload(Guid.Empty, "Ana", SampleState.AwaitingReview),
            options);

        Assert.Contains("\"trainer_id\"", json, StringComparison.Ordinal);
        Assert.Contains("\"display_name\"", json, StringComparison.Ordinal);
        Assert.Contains("\"awaiting_review\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AddApi_RejectsPayloadsThatIgnoreTheSnakeCaseContract()
    {
        var options = BuildProvider()
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;

        var deserialized = JsonSerializer.Deserialize<SamplePayload>(
            """{"TrainerId":"00000000-0000-0000-0000-000000000000","DisplayName":"Ana","State":"awaiting_review"}""",
            options);

        Assert.Null(deserialized!.DisplayName);
    }

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new KeyValuePair<string, string?>("Cors:AllowedOrigins:0", "https://app.example.test")
            ])
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddApi(configuration)
            .BuildServiceProvider();
    }
}
