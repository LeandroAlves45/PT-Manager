using Infrastructure.Jobs.QStash;

namespace Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Verifica a validação de configuração do receptor QStash.
/// Esta validação corre com `ValidateOnStart`.
/// Uma configuração incompleta tem de impedir o arranque
/// em vez de deixar a aplicação viva a aceitar activações que não consegue
/// autenticar correctamente.
/// </summary>
public sealed class QStashOptionsTests
{
    private const string ValidKey = "0123456789abcdef0123456789abcdef";
    private const string CanonicalUrl = "https://api.example.com/api/internal/jobs/dispatch";

    [Fact]
    public void IsValid_WhenDisabled_DoesNotRequireSecrets()
    {
        // Desligado é o estado de deploy: a aplicação tem de arrancar sem chaves.
        var options = new QStashOptions { Enabled = false };

        Assert.True(options.IsValid());
    }

    [Fact]
    public void IsValid_WhenDisabledButLimitsAreAbsurd_StillFails()
    {
        var options = new QStashOptions { Enabled = false, MaximumBodySize = 0 };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void IsValid_WhenEnabledAndFullyConfigured_Succeeds()
    {
        Assert.True(CreateEnabled().IsValid());
    }

    [Fact]
    public void IsValid_WhenEnabledWithoutDestination_Fails()
    {
        var options = CreateEnabled();
        options.DestinationUrl = null;

        Assert.False(options.IsValid());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short")]
    public void IsValid_WhenEnabledWithWeakCurrentKey_Fails(string key)
    {
        var options = CreateEnabled();
        options.CurrentSigningKey = key;

        Assert.False(options.IsValid());
    }

    [Fact]
    public void IsValid_WhenEnabledWithoutNextKey_Fails()
    {
        // A chave seguinte é obrigatória para que a rotação não implique downtime.
        var options = CreateEnabled();
        options.NextSigningKey = string.Empty;

        Assert.False(options.IsValid());
    }

    [Fact]
    public void IsValid_RejectsSigningKeyWithControlCharacters()
    {
        var options = CreateEnabled();
        options.CurrentSigningKey = ValidKey + "\n";

        Assert.False(options.IsValid());
    }

    [Theory]
    [InlineData("https://api.example.com/api/internal/jobs/dispatch?x=1")]
    [InlineData("https://api.example.com/api/internal/jobs/dispatch#fragment")]
    [InlineData("https://user:pass@api.example.com/api/internal/jobs/dispatch")]
    [InlineData("https://api.example.com/api/internal/jobs/dispatch/")]
    [InlineData("https://api.example.com/wrong/path")]
    [InlineData("http://api.example.com/api/internal/jobs/dispatch")]
    public void IsValid_RejectsNonCanonicalDestination(string url)
    {
        // O 'sub' do token é comparado com este URL exacto. Aceitar variações aqui
        // enfraqueceria essa comparação.
        var options = CreateEnabled();
        options.DestinationUrl = new Uri(url);

        Assert.False(options.IsValid());
    }

    [Fact]
    public void IsValid_AllowsLoopbackOverHttpForLocalDevelopment()
    {
        var options = CreateEnabled();
        options.DestinationUrl = new Uri("http://localhost:5000/api/internal/jobs/dispatch");

        Assert.True(options.IsValid());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(64 * 1024 + 1)]
    public void IsValid_RejectsBodySizeOutsideBounds(int size)
    {
        var options = CreateEnabled();
        options.MaximumBodySize = size;

        Assert.False(options.IsValid());
    }

    [Fact]
    public void IsValid_RejectsNegativeClockSkew()
    {
        var options = CreateEnabled();
        options.ClockSkew = TimeSpan.FromSeconds(-1);

        Assert.False(options.IsValid());
    }

    [Fact]
    public void IsValid_RejectsExcessiveClockSkew()
    {
        // Uma tolerância grande alargaria a janela de aceitação de tokens antigos.
        var options = CreateEnabled();
        options.ClockSkew = TimeSpan.FromMinutes(5);

        Assert.False(options.IsValid());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void IsValid_RejectsTokenLifetimeOutsideBounds(int minutes)
    {
        var options = CreateEnabled();
        options.MaximumTokenLifetime = TimeSpan.FromMinutes(minutes);

        Assert.False(options.IsValid());
    }

    [Fact]
    public void IsValid_RejectsRetentionShorterThanTenMinutes()
    {
        // A retenção define durante quanto tempo um replay é detectável.
        var options = CreateEnabled();
        options.ReplayRetention = TimeSpan.FromMinutes(5);

        Assert.False(options.IsValid());
    }

    [Fact]
    public void IsValid_RejectsRetentionLongerThanSevenDays()
    {
        var options = CreateEnabled();
        options.ReplayRetention = TimeSpan.FromDays(8);

        Assert.False(options.IsValid());
    }

    private static QStashOptions CreateEnabled() => new()
    {
        Enabled = true,
        CurrentSigningKey = ValidKey,
        NextSigningKey = ValidKey + "ff",
        DestinationUrl = new Uri(CanonicalUrl)
    };
}
