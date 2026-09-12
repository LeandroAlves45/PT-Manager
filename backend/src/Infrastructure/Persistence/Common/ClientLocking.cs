using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Common;

/// <summary>Lock pessimista sobre a ficha do cliente associada á conta autenticada.</summary>
internal static class ClientLocking
{
    /// <summary>Bloqueia a ficha ativa do cliente com FOR UPDATE.</summary>
    public static Task<Guid> LockOwnClientAsync(
        this PtManagerDbContext dbContext,
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value"
            FROM clients
            WHERE owner_trainer_id = {trainerId}
                AND user_id = {clientUserId}
                AND is_active = true
                AND is_deleted = false
            FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
}
