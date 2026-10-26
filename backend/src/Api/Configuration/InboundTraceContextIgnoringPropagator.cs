using System.Diagnostics;

namespace Api.Configuration;

/// <summary>
/// Ignora o <c>traceparent</c>, o <c>tracestate</c> e o <c>baggage</c> que chegam nos pedidos,
/// mas continua a propagá-los nas chamadas que a API faz.
/// </summary>
/// <remarks>
/// <para>
/// A API é pública e nenhum cliente de confiança lhe envia contexto de trace. Aceitá-lo
/// deixava qualquer pedido anónimo escolher o trace id (que vira correlation id dos jobs
/// e entra em chaves de idempotência), forçar a amostragem com a flag <c>01</c> para gastar
/// a quota do Grafana, ou esconder-se com <c>00</c>. Cada pedido começa assim um trace novo.
/// </para>
/// <para>
/// Quando o frontend passar a enviar <c>traceparent</c> (sprint futuro, S7-12), a decisão
/// revê-se com uma allowlist de origens.
/// </para>
/// </remarks>
internal sealed class InboundTraceContextIgnoringPropagator : DistributedContextPropagator
{
    private readonly DistributedContextPropagator _outbound;

    public InboundTraceContextIgnoringPropagator(DistributedContextPropagator outbound) =>
        _outbound = outbound ?? throw new ArgumentNullException(nameof(outbound));

    public override IReadOnlyCollection<string> Fields => _outbound.Fields;

    public override void Inject(Activity? activity, object? carrier, PropagatorSetterCallback? setter) =>
        _outbound.Inject(activity, carrier, setter);

    public override void ExtractTraceIdAndState(
        object? carrier,
        PropagatorGetterCallback? getter,
        out string? traceId,
        out string? traceState)
    {
        traceId = null;
        traceState = null;
    }

    public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(
        object? carrier,
        PropagatorGetterCallback? getter) => null;
}
