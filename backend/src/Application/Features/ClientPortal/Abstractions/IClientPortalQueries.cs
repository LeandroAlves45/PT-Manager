using Application.Features.ClientPortal.Dtos;

namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Resolve o plano de treino ativo do cliente autenticado.</summary>
public interface IMyTrainingPlanQueries
{
    Task<MyTrainingPlanDto?> GetActiveAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken
    );
}

/// <summary>Lê os planos alimentares do cliente autenticado e o que ele escolheu seguir.</summary>
public interface IMyNutritionPlanQueries
{
    /// <summary>
    /// Plano que o cliente escolheu, se ainda estiver ativo. Uma escolha de um plano arquivado
    /// conta como nenhuma: a app nunca abre outro plano sozinha.
    /// </summary>
    Task<MyNutritionPlanDto?> GetSelectedAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken
    );

    /// <summary>Planos ativos do cliente, do mais recente para o mais antigo.</summary>
    Task<IReadOnlyList<MyMealPlanSummaryDto>> ListActiveAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken
    );
}

/// <summary>Grava o plano alimentar que o cliente escolheu seguir.</summary>
public interface IMyMealPlanSelectionStore
{
    /// <summary>
    /// Escolhe o plano se for do próprio cliente e estiver ativo. Devolve false quando não é.
    /// </summary>
    Task<bool> SelectAsync(
        Guid trainerId,
        Guid clientUserId,
        Guid mealPlanId,
        CancellationToken cancellationToken
    );
}

/// <summary>Resolve o perfil do cliente autenticado.</summary>
public interface IMyProfileQueries
{
    Task<MyProfileDto?> GetAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken
    );
}
