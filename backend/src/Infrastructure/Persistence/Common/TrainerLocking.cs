using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Common;

/// <summary>
/// Lock pessimista partilhado sobre a linha de personal trainer em users.
/// Usado por qualquer store que precise de serializar escritas relativas
/// a um personal trainer especifico.
/// </summary>
internal static class TrainerLocking
{
    /// <summary>
    /// Bloqueia a linha do trainer com FOR UPDATE. Lança se o personal trainer
    /// não existir, estiver apagado ou não tiver o role esperado — a
    /// existência de um trainer efetivo é um invariante técnico, não uma
    /// validação de negócio do chamador.
    /// </summary>
    public static async Task LockTrainerAsync(
        this PtManagerDbContext dbContext,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var lockedId = await dbContext.Database.SqlQuery<Guid>(
                $"SELECT id AS \"Value\" FROM users WHERE id = {trainerId} AND role = 'trainer' AND is_deleted = false FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

        if (lockedId == Guid.Empty)
            throw new InvalidOperationException(
                "The effective personal trainer must exist before writing trainer-scoped data.");
    }
}
