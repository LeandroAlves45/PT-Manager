using OpenTelemetry.Context.Propagation;

namespace Api.Configuration;

/// <summary>
/// Par de <see cref="InboundTraceContextIgnoringPropagator"/> para o SDK do OpenTelemetry.
/// </summary>
/// <remarks>
/// A instrumentação de ASP.NET Core volta a ler o <c>traceparent</c> e o <c>baggage</c> com o
/// propagator por omissão do SDK e, se encontrar um contexto válido, recria a span do pedido
/// com esse pai remoto. Este propagator não extrai nem injeta nada: a span criada pelo
/// hosting, sem pai, é a que fica. As chamadas de saída propagam pelo HttpClient nativo do
/// .NET, que não usa este propagator.
/// </remarks>
internal sealed class InboundTraceContextIgnoringTextMapPropagator : TextMapPropagator
{
    public override ISet<string> Fields { get; } = new HashSet<string>();

    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
    }

    public override PropagationContext Extract<T>(
        PropagationContext context,
        T carrier,
        Func<T, string, IEnumerable<string>?> getter) => context;
}
