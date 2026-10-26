using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Application.Common.Observability;

/// <summary>
/// Fonte única dos traces e das métricas próprias do PT Manager.
/// </summary>
/// <remarks>
/// <para>
/// Vive na Application porque <see cref="ActivitySource"/> e <see cref="Meter"/> são da BCL e o
/// handler do webhook Stripe, que só existe aqui, é quem sabe se um evento era duplicado. A
/// Infrastructure e a Api usam a mesma fonte, para o host só ter de escutar um nome.
/// </para>
/// <para>
/// Sem um listener (OpenTelemetry desligado), <c>StartActivity</c> devolve <c>null</c> e cada
/// <c>Add</c>/<c>Record</c> é um no-op barato. As tags nunca levam identificadores de pessoas,
/// paths crus nem query: só valores de baixa cardinalidade (rota, resultado, tipo, fila).
/// </para>
/// </remarks>
public static class PtManagerTelemetry
{
    /// <summary>Nome comum da <see cref="ActivitySource"/> e do <see cref="Meter"/>.</summary>
    public const string Name = "PTManager";

    /// <summary>Spans próprias: processamento de durable jobs e de mensagens da outbox.</summary>
    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    /// <summary>Desfecho de cada item processado pelos dispatchers.</summary>
    /// <remarks>
    /// Tags: <c>ptm.queue</c> (<c>job</c> ou <c>outbox</c>), <c>ptm.item.type</c> e
    /// <c>ptm.outcome</c> (<c>succeeded</c>, <c>retry</c>, <c>dead_letter</c>, <c>lease_lost</c>).
    /// Um <c>dead_letter</c> é o sinal do alerta de jobs desistidos.
    /// </remarks>
    public static readonly Counter<long> DispatchOutcomes = Meter.CreateCounter<long>(
        "ptm.jobs.outcome",
        unit: "{item}",
        description: "Outcome of each durable job or outbox message processed by the dispatcher.");

    /// <summary>Itens vencidos e ainda pendentes no fim de cada ativação do dispatcher.</summary>
    /// <remarks>
    /// Uma contagem por ativação (de 20 em 20 minutos), nunca um gauge observável que
    /// consultasse o PostgreSQL sempre que o exporter recolhe, o que manteria o Neon acordado.
    /// Tag <c>ptm.queue</c>.
    /// </remarks>
    public static readonly Gauge<long> DispatchBacklog = Meter.CreateGauge<long>(
        "ptm.jobs.pending",
        unit: "{item}",
        description: "Due items still pending after a dispatcher activation.");

    /// <summary>Webhooks Stripe recebidos, por desfecho.</summary>
    /// <remarks>
    /// Tag <c>ptm.outcome</c>: <c>processed</c>, <c>duplicate</c>, <c>ignored</c>,
    /// <c>rejected</c>, <c>failed</c> ou <c>disabled</c>. Nunca o tipo do evento nem o id.
    /// </remarks>
    public static readonly Counter<long> StripeWebhooks = Meter.CreateCounter<long>(
        "ptm.stripe.webhook",
        unit: "{event}",
        description: "Stripe webhook deliveries by outcome.");

    /// <summary>Comandos SQL executados por pedido HTTP, por rota.</summary>
    /// <remarks>
    /// Mede o orçamento de queries do Gate 7A (≤ 5 por leitura) e mostra as leituras
    /// repetitivas que o Gate 7B avalia para cache. Tags <c>http.route</c> (template) e
    /// <c>http.request.method</c>.
    /// </remarks>
    public static readonly Histogram<int> RequestDbCommands = Meter.CreateHistogram<int>(
        "ptm.http.db_commands",
        unit: "{command}",
        description: "Database commands executed while serving one HTTP request.",
        advice: new InstrumentAdvice<int>
        {
            HistogramBucketBoundaries = [0, 1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 30, 50]
        });
}
