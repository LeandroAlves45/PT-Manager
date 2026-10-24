using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Assessments;

/// <summary>
/// Sem avaliação inicial gravada, o personal trainer não cria plano de treino, plano
/// alimentar, atribuição de suplemento nem avaliação periódica. Sessões e packs não esperam.
/// Marcar a avaliação como revista não conta. A regra vive só aqui; cada store chama-a dentro
/// da própria transação, antes de escrever.
/// </summary>
internal static class InitialAssessmentRequirement
{
    internal static Task<bool> IsMetAsync(
        PtManagerDbContext dbContext,
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken) =>
        dbContext.InitialAssessments
            .AsNoTracking()
            .AnyAsync(
                assessment => assessment.OwnerTrainerId == trainerId &&
                    assessment.ClientId == clientId &&
                    !assessment.IsDeleted,
                cancellationToken);
}
