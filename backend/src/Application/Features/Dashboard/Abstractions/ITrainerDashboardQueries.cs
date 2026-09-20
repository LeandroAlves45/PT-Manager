using Application.Features.Dashboard.Dtos;

namespace Application.Features.Dashboard.Abstractions;

/// <summary>Janela temporária já resolvida no fuso horário do personal trainer.
/// A infrastructure não calcula datas: recebe os limites prontos para que a regra de "hoje"
/// e "mês" viva num só sitío.
/// </summary>
public sealed record TrainerDashboardWindow(
    DateOnly LocalToday,
    DateTimeOffset TodayStartUtc,
    DateTimeOffset TodayEndUtc,
    DateTime NowUtc,
    DateTime ReviewOverdueBeforeUtc,
    DateOnly PackEndingUntil,
    DateOnly PlanExpiringUntil,
    DateOnly CurrentMonthStart,
    DateOnly PreviousMonthStart,
    DateOnly NextMonthStart);

/// <summary>Leituras agregadas do dashboard; orçamento fixo de comandos, sem N+1.</summary>
public interface ITrainerDashboardQueries
{
    Task<TrainerDashboardData> GetAsync(
        Guid trainerId,
        TrainerDashboardWindow window,
        CancellationToken cancellationToken);
}
