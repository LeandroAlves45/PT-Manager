using Application.Common.Abstractions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Time;

/// <summary>Resolve o timezone IANA guardado nas settings do personal trainer.</summary>
public sealed class TrainerTimeZoneProvider : ITrainerTimeZoneProvider
{
    private readonly PtManagerDbContext _dbContext;

    public TrainerTimeZoneProvider(PtManagerDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<TimeZoneInfo> GetRequiredAsync(
        Guid trainerId,
        CancellationToken cancellationToken
    )
    {
        var timeZoneId = await _dbContext.TrainerSettings
            .AsNoTracking()
            .Where(settings => settings.TrainerId == trainerId)
            .Select(settings => settings.Timezone)
            .SingleOrDefaultAsync(cancellationToken);

        if (timeZoneId is null)
            throw new InvalidOperationException(
                "TrainerSettings must exist before executing local-time rules."
            );

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex)
            when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Uma configuração persistida inválida é corrupção técnica, não
            // uma validação provocada pelo command atual.
            throw new InvalidOperationException(
                "TrainerSettings contains an invalid timezone identifier.",
                ex
            );
        }
    }
}
