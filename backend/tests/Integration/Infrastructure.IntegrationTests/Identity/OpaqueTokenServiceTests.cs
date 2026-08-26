using Infrastructure.Identity;

namespace Infrastructure.IntegrationTests.Identity;

public sealed class OpaqueTokenServiceTests
{
    [Fact]
    public void Generate_ProducesUrlSafeRawTokenAndSha256Hash()
    {
        var service = new OpaqueTokenService();

        var token = service.Generate();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", token.RawToken);
        Assert.Matches("^[A-F0-9]{64}$", token.TokenHash);
        Assert.NotEqual(token.RawToken, token.TokenHash);
    }

    [Fact]
    public void Generate_ConsecutiveCallsProduceDifferentSecrets()
    {
        var service = new OpaqueTokenService();

        var first = service.Generate();
        var second = service.Generate();

        Assert.NotEqual(first.RawToken, second.RawToken);
        Assert.NotEqual(first.TokenHash, second.TokenHash);
    }

    [Fact]
    public void Hash_SameRawTokenProducesSameHash()
    {
        var service = new OpaqueTokenService();
        const string RawToken = "test-token";

        var first = service.Hash(RawToken);
        var second = service.Hash(RawToken);

        Assert.Equal(first, second);
    }
}
