using System.Security.Cryptography;
using System.Text;
using Infrastructure.Jobs.QStash;
using Infrastructure.IntegrationTests.Support;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Verifica a deduplicação de activações QStash contra PostgreSQL real.
/// A protecção contra replay depende de uma restrição de unicidade e do
/// comportamento real de `ON CONFLICT DO NOTHING` sob concorrência.
/// Um substituto em memória não exercita a condição que interessa: duas transacções
/// a tentar inserir o mesmo `jti` ao mesmo tempo.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class QStashReplayStoreTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public QStashReplayStoreTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task TryConsume_FirstUse_IsAcceptedAndPersistsReceipt()
    {
        var hash = NewHash();

        var consumed = await ConsumeAsync(hash);

        Assert.True(consumed);
        Assert.Equal(1, await CountReceiptsAsync(hash));
    }

    [Fact]
    public async Task TryConsume_SameJtiTwice_IsReplayOnSecondUse()
    {
        var hash = NewHash();

        Assert.True(await ConsumeAsync(hash));
        Assert.False(await ConsumeAsync(hash));

        // Um replay não pode duplicar o recibo nem apagar o original.
        Assert.Equal(1, await CountReceiptsAsync(hash));
    }

    [Fact]
    public async Task TryConsume_WhenReceiptAlreadyCommitted_ReturnsFalse()
    {
        var hash = NewHash();
        await ConsumeAsync(hash);

        // Confirmação perdida após commit: o retry observa conflito e trata
        // a execução ambígua como replay já consumido, nunca como primeiro uso.
        Assert.False(await ConsumeAsync(hash));
        Assert.Equal(1, await CountReceiptsAsync(hash));
    }

    [Fact]
    public async Task TryConsume_WhenStoreIsDisposed_ThrowsInsteadOfReportingReplay()
    {
        var options = Options.Create(new QStashOptions
        {
            ReplayRetention = TimeSpan.FromDays(1)
        });
        var context = _fixture.CreateAdministrativeContext();
        var store = new QStashReplayStore(context, options);
        await context.DisposeAsync();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.TryConsumeAsync(
                NewHash(),
                Now.AddMinutes(5),
                Now,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryConsume_SameJtiInSeparateScopes_IsStillReplay()
    {
        var hash = NewHash();

        // Cada consumo usa um DbContext próprio, tal como acontece entre pedidos
        // HTTP distintos e entre instâncias da aplicação.
        Assert.True(await ConsumeAsync(hash));
        Assert.False(await ConsumeAsync(hash));
        Assert.False(await ConsumeAsync(hash));
    }

    [Fact]
    public async Task TryConsume_DifferentJti_AreIndependent()
    {
        Assert.True(await ConsumeAsync(NewHash()));
        Assert.True(await ConsumeAsync(NewHash()));
    }

    [Fact]
    public async Task TryConsume_ConcurrentSameJti_AcceptsExactlyOne()
    {
        var hash = NewHash();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => ConsumeAsync(hash)));

        // Esta é a garantia central: sob concorrência real, uma e só uma
        // activação pode ser aceite para o mesmo token.
        Assert.Equal(1, results.Count(accepted => accepted));
        Assert.Equal(1, await CountReceiptsAsync(hash));
    }

    [Fact]
    public async Task TryConsume_RemovesReceiptsBeyondRetention()
    {
        var expiredHash = NewHash();
        var retention = TimeSpan.FromHours(1);

        // Recibo cujo token expirou muito antes da janela de retenção.
        await ConsumeAsync(
            expiredHash,
            tokenExpiresAt: Now.AddHours(-5),
            now: Now.AddHours(-5),
            retention: retention);
        Assert.Equal(1, await CountReceiptsAsync(expiredHash));

        await ConsumeAsync(NewHash(), now: Now, retention: retention);

        Assert.Equal(0, await CountReceiptsAsync(expiredHash));
    }

    [Fact]
    public async Task TryConsume_KeepsReceiptsInsideRetention()
    {
        var recentHash = NewHash();
        var retention = TimeSpan.FromHours(1);

        await ConsumeAsync(
            recentHash,
            tokenExpiresAt: Now.AddMinutes(-5),
            now: Now.AddMinutes(-5),
            retention: retention);

        await ConsumeAsync(NewHash(), now: Now, retention: retention);

        // Apagar cedo demais reabriria a janela de replay.
        Assert.Equal(1, await CountReceiptsAsync(recentHash));
        Assert.False(await ConsumeAsync(recentHash, now: Now, retention: retention));
    }

    [Fact]
    public async Task StoredReceipt_KeepsOnlyTheHashAndIsNormalized()
    {
        var jti = $"msg_{Guid.NewGuid():N}";
        var hash = Sha256Hex(jti);

        await ConsumeAsync(hash);

        var stored = await _fixture.QueryScalarAsync<string>(
            "SELECT jti_hash FROM qstash_dispatch_receipts WHERE jti_hash = @hash",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("hash", hash));

        Assert.NotNull(stored);
        // A coluna é character(64): comparar sem padding à direita.
        var normalized = stored.TrimEnd();
        Assert.Equal(64, normalized.Length);
        Assert.Equal(normalized, normalized.ToLowerInvariant());

        // O identificador original nunca é persistido.
        var originalCount = await _fixture.QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM qstash_dispatch_receipts WHERE jti_hash = @jti",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("jti", jti));
        Assert.Equal(0, originalCount);
    }

    private async Task<bool> ConsumeAsync(
        string jtiHash,
        DateTime? tokenExpiresAt = null,
        DateTime? now = null,
        TimeSpan? retention = null)
    {
        var effectiveNow = now ?? Now;
        var options = Options.Create(new QStashOptions
        {
            ReplayRetention = retention ?? TimeSpan.FromDays(1)
        });

        await using var context = _fixture.CreateAdministrativeContext();
        var store = new QStashReplayStore(context, options);

        return await store.TryConsumeAsync(
            jtiHash,
            tokenExpiresAt ?? effectiveNow.AddMinutes(5),
            effectiveNow,
            TestContext.Current.CancellationToken);
    }

    private async Task<long> CountReceiptsAsync(string jtiHash) =>
        await _fixture.QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM qstash_dispatch_receipts WHERE jti_hash = @hash",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("hash", jtiHash));

    private static string NewHash() => Sha256Hex(Guid.NewGuid().ToString("N"));

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
