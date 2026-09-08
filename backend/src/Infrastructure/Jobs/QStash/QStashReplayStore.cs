using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Jobs.QStash;

/// <summary>Deduplica ativações QStash em PostgreSQL.</summary>
internal sealed class QStashReplayStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly QStashOptions _options;

    public QStashReplayStore(
        PtManagerDbContext dbContext,
        IOptions<QStashOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<bool> TryConsumeAsync(
        string jtiHash,
        DateTime tokenExpiresAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        _ = new QStashDispatchReceipt(jtiHash, tokenExpiresAt, now);
        var deleteBefore = now.Subtract(_options.ReplayRetention);
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            // A limpeza é segura para retry e mantém o custo limitado ao índice temporal.
            await _dbContext.QStashDispatchReceipts
                .Where(receipt => receipt.TokenExpiresAt <= deleteBefore)
                .ExecuteDeleteAsync(cancellationToken);

            var inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO qstash_dispatch_receipts (jti_hash, token_expires_at, consumed_at)
                VALUES ({jtiHash}, {tokenExpiresAt}, {now})
                ON CONFLICT (jti_hash) DO NOTHING
                """, cancellationToken);

            // Se a confirmação do primeiro commit se perdeu, o retry observa conflito
            // e converte corretamente a execução ambígua num replay já consumido.
            return inserted == 1;
        });
    }
}
