using System.Net;
using System.Text.Json.Serialization;
using Application.Features.Jobs.Dispatching;
using Application.Features.Notifications.SessionReminders;

namespace Api.Configuration;

/// <summary>
/// Endpoint de desenvolvimento para correr à mão o scan da véspera e a ativação do dispatcher,
/// enquanto o horário automático (QStash) não está ligado. Só é mapeado em Development, fica
/// fora do OpenAPI e só responde a pedidos da própria máquina.
/// </summary>
internal static class DevelopmentSessionReminderEndpoint
{
    internal const string Path = "/api/internal/dev/session-reminders/run";

    /// <summary>Mapeia o endpoint. Chamar só quando o ambiente é Development.</summary>
    internal static IEndpointRouteBuilder MapDevelopmentSessionReminders(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Path, RunAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> RunAsync(
        HttpContext httpContext,
        ISessionReminderScanner scanner,
        IJobDispatchActivation activation,
        CancellationToken cancellationToken)
    {
        var remote = httpContext.Connection.RemoteIpAddress;
        if (remote is null || !IPAddress.IsLoopback(remote))
            return Results.NotFound();

        var scan = await scanner.ScanAsync(cancellationToken);
        // A ativação volta a correr o scan (já não enfileira nada novo) e envia o que está na fila.
        await activation.ActivateAsync(cancellationToken);

        return Results.Ok(new RunResponse(
            scan.SessionCount,
            scan.Queued,
            scan.AlreadyQueued,
            scan.ClientsWithoutEmail));
    }

    private sealed record RunResponse(
        [property: JsonPropertyName("session_count")] int SessionCount,
        [property: JsonPropertyName("queued")] int Queued,
        [property: JsonPropertyName("already_queued")] int AlreadyQueued,
        [property: JsonPropertyName("clients_without_email")] int ClientsWithoutEmail);
}
