using System.Diagnostics;

namespace Application.Common.Observability;

/// <summary>Gera o correlation id dos jobs e mensagens criados por um pedido.</summary>
public static class OperationCorrelation
{
    /// <summary>
    /// Devolve o trace id da operação atual como <see cref="Guid"/>, ou um Guid novo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O trace id tem 16 bytes, como um Guid. O formato <c>N</c> do Guid devolvido é igual ao
    /// trace id em hexadecimal, por isso, a partir do pedido no Grafana, chega-se ao job (tag
    /// <c>ptm.correlation_id</c>) e aos logs (scope <c>CorrelationId</c>) sem migration.
    /// </para>
    /// <para>
    /// Um trace que veio de fora (header <c>traceparent</c>) não serve: o cliente escolhe o
    /// valor, e o correlation id entra em chaves de idempotência como
    /// <c>trainer-logo.delete:{id}</c>. Dois pedidos com o mesmo <c>traceparent</c> colidiriam.
    /// Sem <see cref="Activity"/> (telemetria desligada) também cai no Guid aleatório.
    /// </para>
    /// </remarks>
    public static Guid NewId()
    {
        var activity = Activity.Current;
        if (activity is null || activity.TraceId == default)
            return Guid.NewGuid();

        // O pai remoto marca a raiz local do trace; uma span filha herda o mesmo trace id.
        var root = activity;
        while (root.Parent is not null)
            root = root.Parent;

        // A raiz local de um trace não tem pai. Um ParentId na raiz é sempre um pai de fora, quer
        // venha de um contexto remoto (ActivitySource) quer de SetParentId(string) (hosting sem listener).
        return root.ParentId is not null
            ? Guid.NewGuid()
            : Guid.ParseExact(activity.TraceId.ToHexString(), "N");
    }
}
