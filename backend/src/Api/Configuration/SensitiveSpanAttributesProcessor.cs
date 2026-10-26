using System.Diagnostics;
using OpenTelemetry;

namespace Api.Configuration;

/// <summary>Tira das spans os atributos que levam ids, query ou user-agent.</summary>
/// <remarks>
/// <para>
/// A span do servidor pode levar <c>url.path</c> (com os ids do cliente e do personal
/// trainer), <c>url.query</c> e <c>user_agent.original</c>: aparecem sob o TestServer e não
/// aparecem sob o Kestrel com esta versão, por isso a regra não depende do host. O HttpClient
/// grava sempre o <c>url.full</c>, onde os caminhos do R2 levam ids. Tudo isto sairia para o
/// Grafana Cloud, contra a regra S7-11. O IP do cliente não está na lista porque nenhuma das
/// duas instrumentações o grava.
/// </para>
/// <para>
/// O que fica chega para diagnosticar: <c>http.route</c> (o template), o método, o status,
/// o <c>server.address</c> do fornecedor e o <c>ptm.correlation_id</c>. Corre no fim da
/// span, antes de qualquer exporter, porque o <c>http.route</c> só existe depois do routing.
/// </para>
/// </remarks>
internal sealed class SensitiveSpanAttributesProcessor : BaseProcessor<Activity>
{
    private static readonly string[] RemovedAttributes =
    [
        "url.path",
        "url.query",
        "url.full",
        "user_agent.original",
    ];

    public override void OnEnd(Activity data)
    {
        foreach (var attribute in RemovedAttributes)
            data.SetTag(attribute, null);
    }
}
