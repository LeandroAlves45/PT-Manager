using System.Text.Json;
using Api.Contracts.Common;
using Api.FunctionalTests.Support;
using Application.Pagination;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Api.FunctionalTests.Contracts;

public sealed class PagedResponseTests
{
    [Fact]
    public void From_SerializesEnvelopeInSnakeCaseWithFilterTotal()
    {
        var options = BuildSerializerOptions();
        var page = new PageResult<SourceItem>(
            [new SourceItem("first"), new SourceItem("second")],
            57);

        var envelope = PagedResponse<ResponseItem>.From(
            page,
            pageNumber: 3,
            pageSize: 2,
            source => new ResponseItem(source.Label));

        var json = JsonSerializer.Serialize(envelope, options);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Array, root.GetProperty("items").ValueKind);
        Assert.Equal(2, root.GetProperty("items").GetArrayLength());
        Assert.Equal(57, root.GetProperty("total_count").GetInt32());
        Assert.Equal(3, root.GetProperty("page_number").GetInt32());
        Assert.Equal(2, root.GetProperty("page_size").GetInt32());
        Assert.DoesNotContain("TotalCount", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PageNumber", json, StringComparison.Ordinal);
    }

    private static JsonSerializerOptions BuildSerializerOptions() =>
        new ServiceCollection()
            .AddLogging()
            .AddApi(new ConfigurationBuilder()
                .AddInMemoryCollection([
                    new KeyValuePair<string, string?>("Cors:AllowedOrigins:0", "https://app.example.test"),
                    new KeyValuePair<string, string?>("Jwt:Issuer", ApiWebApplicationFactory.Issuer),
                    new KeyValuePair<string, string?>("Jwt:Audience", ApiWebApplicationFactory.Audience),
                    new KeyValuePair<string, string?>("Jwt:SigningKey", ApiWebApplicationFactory.JwtSigningMaterial)
                ])
                .Build(),
                new HostingEnvironment { EnvironmentName = "Development" })
            .BuildServiceProvider()
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;

    private sealed record SourceItem(string Label);

    private sealed record ResponseItem(string Label);
}
