using Application.Errors;

namespace Application.Features.Dashboard;

/// <summary>Erros estáveis do dashboard agregado ao personal trainer.</summary>
public static class DashboardErrors
{
    public static readonly Error TrainerOnly = Error.Create(
        "dashboard_trainer_only",
        ErrorCategory.Forbidden,
        "Only an authorized personal trainer can read the dashboard.");
}
