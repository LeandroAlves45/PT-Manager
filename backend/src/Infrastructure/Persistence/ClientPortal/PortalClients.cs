using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>Resolve o cliente do utilizador autenticado dentro do tenant efetivo.</summary>
internal static class PortalClients
{
    /// <summary>
    /// Devolve o Id do cliente ativo e não eliminado do utilizador, ou null. Inexistente e
    /// arquivado respondem igual para não revelar o estado da conta.
    /// </summary>
    internal static async Task<Guid?> FindActiveIdAsync(
        PtManagerDbContext dbContext,
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return await dbContext.Clients
            .AsNoTracking()
            .Where(client =>
                client.OwnerTrainerId == trainerId &&
                client.UserId == clientUserId &&
                client.IsActive)
            .Select(client => (Guid?)client.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
