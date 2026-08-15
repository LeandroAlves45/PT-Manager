namespace Application.Common.Abstractions;

/// <summary>Resolve o timezone IANA efetivo do personal trainer.</summary>
public interface ITrainerTimeZoneProvider
{
    /// <summary>
    /// Obtém um timezone válido. A ausência ou corrupção das settings representa
    /// uma violação técnica da criação obrigatória de TrainerSettings.
    /// </summary>
    Task<TimeZoneInfo> GetRequiredAsync(
        Guid trainerId,
        CancellationToken cancellationToken
    );
}
