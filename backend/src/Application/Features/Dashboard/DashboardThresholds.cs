namespace Application.Features.Dashboard;

/// <summary>
/// Limiares fixos do dashboard. Estão num único ficheiro para
/// serem ajustados sem procurar regras espalhadas; mudar um valor muda o contrato visível
/// ao personal trainer e exige atualizar os testes que o fixam.
/// </summary>
public static class DashboardThresholds
{
    public const int TopCount = 5;
    public const int PackSessionsRemainingThreshold = 2;
    public const int PackEndingWithinDays = 7;
    public const int PlanExpiringWithinDays = 7;

    /// <summary>
    /// Check-in respondido há mais do que estas horas conta como revisão em atraso.
    /// </summary>
    public const int ReviewOverdueAfterHours = 48;
}
