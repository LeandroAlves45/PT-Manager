using Application.Features.TrainerSettings.Abstractions;
using Application.Features.TrainerSettings.Dtos;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.TrainerSettings;

/// <summary>Consulta as definições completas do próprio personal trainer sem tracking.</summary>
internal sealed class TrainerSettingsQueries : ITrainerSettingsQueries
{
    private readonly PtManagerDbContext _dbContext;

    public TrainerSettingsQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<TrainerSettingsDto?> GetAsync(
        Guid trainerId,
        CancellationToken cancellationToken) =>
        _dbContext.TrainerSettings
            .AsNoTracking()
            .Where(settings => settings.TrainerId == trainerId)
            .Select(settings => new TrainerSettingsDto(
                settings.AppName,
                settings.LogoUrl,
                settings.PrimaryColor,
                settings.BodyColor,
                settings.Phone,
                settings.Address,
                settings.City,
                settings.Timezone,
                settings.CreatedAt,
                settings.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
}
