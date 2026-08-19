using Domain.Entities.Clients;
using Domain.Entities.Supplements;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Supplements;

/// <summary>Locks pessimistas partilhados pelos stores de suplementos e atribuições.</summary>
internal static class SupplementLocking
{
    public static Task<Client?> LockClientAsync(
        this PtManagerDbContext dbContext,
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken) => dbContext.Clients
        .FromSqlInterpolated($$"""
            SELECT * FROM clients
            WHERE owner_trainer_id = {{trainerId}}
                AND id = {{clientId}}
                AND is_deleted = false
            FOR UPDATE
            """)
        .SingleOrDefaultAsync(cancellationToken);

    public static Task<Supplement?> LockVisibleSupplementAsync(
        this PtManagerDbContext dbContext,
        Guid trainerId,
        Guid supplementId,
        CancellationToken cancellationToken) => dbContext.Supplements
        .FromSqlInterpolated($$"""
            SELECT * FROM supplements
            WHERE id = {{supplementId}}
                AND (owner_trainer_id IS NULL OR owner_trainer_id = {{trainerId}})
            FOR UPDATE
            """)
        .SingleOrDefaultAsync(cancellationToken);

    public static Task<ClientSupplementAssignment?> LockAssignmentAsync(
        this PtManagerDbContext dbContext,
        Guid trainerId,
        Guid assignmentId,
        CancellationToken cancellationToken) => dbContext.ClientSupplementAssignments
        .FromSqlInterpolated($$"""
            SELECT * FROM client_supplement_assignments
            WHERE owner_trainer_id = {{trainerId}}
                AND id = {{assignmentId}}
            FOR UPDATE
            """)
        .SingleOrDefaultAsync(cancellationToken);

    public static Task<Supplement?> LockGlobalSupplementAsync(
        this PtManagerDbContext dbContext,
        Guid supplementId,
        CancellationToken cancellationToken) => dbContext.Supplements
        .FromSqlInterpolated($$"""
            SELECT * FROM supplements
            WHERE id = {{supplementId}}
                AND owner_trainer_id IS NULL
            FOR UPDATE
            """)
        .IgnoreQueryFilters()
        .SingleOrDefaultAsync(cancellationToken);
}
