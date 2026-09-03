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

/// <summary>Resolve o plano alimentar ativo do cliente autenticado.</summary>
public interface IMyNutritionPlanQueries
{
    Task<MyNutritionPlanDto?> GetActiveAsync(
        Guid trainerId,
        Guid clientUserId,
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
