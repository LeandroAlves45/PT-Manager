namespace Application.Features.Notifications.SessionReminders;

/// <summary>
/// Procura as sessões da véspera e põe na fila um email ao cliente e outro ao personal trainer
/// por sessão. A véspera é o dia anterior no fuso do personal trainer. Repetir o scan no mesmo
/// dia não volta a enfileirar a mesma sessão para a mesma data.
/// </summary>
public interface ISessionReminderScanner
{
    Task<SessionReminderScanResult> ScanAsync(CancellationToken cancellationToken);
}

/// <summary>Contagens de uma passagem do scan, para diagnóstico.</summary>
/// <param name="SessionCount">Sessões encontradas para amanhã.</param>
/// <param name="Queued">Emails novos postos na fila.</param>
/// <param name="AlreadyQueued">Emails que já estavam na fila ou já tinham saído.</param>
/// <param name="ClientsWithoutEmail">Sessões cujo cliente não tem email de contacto.</param>
public sealed record SessionReminderScanResult(
    int SessionCount,
    int Queued,
    int AlreadyQueued,
    int ClientsWithoutEmail);
