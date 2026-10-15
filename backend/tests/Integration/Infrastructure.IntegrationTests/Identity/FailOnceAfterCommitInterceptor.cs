using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Infrastructure.IntegrationTests.Identity;

/// <summary>
/// Simula a perda da confirmação do commit: o PostgreSQL grava, mas o processo recebe um erro
/// transitório e a execution strategy repete o delegate. Só falha depois de armado.
/// </summary>
internal sealed class FailOnceAfterCommitInterceptor : DbTransactionInterceptor
{
    private bool _isArmed;
    internal int Failures { get; private set; }

    internal void Arm() => _isArmed = true;

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (_isArmed && Failures == 0)
        {
            Failures++;
            throw new NpgsqlException(
                "Simulated loss of the PostgreSQL commit acknowledgement.",
                new TimeoutException());
        }

        return Task.CompletedTask;
    }
}
