using Application.Features.TrainerSettings.Dtos;

namespace Application.Features.TrainerSettings.Abstractions;

/// <summary>Consulta definições completas do próprio personal trainer.</summary>
public interface ITrainerSettingsQueries
{
    Task<TrainerSettingsDto?> GetAsync(Guid trainerId, CancellationToken cancellationToken);
}
