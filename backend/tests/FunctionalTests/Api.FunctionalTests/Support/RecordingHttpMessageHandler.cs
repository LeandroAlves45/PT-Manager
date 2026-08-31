using System.Collections.Concurrent;
using System.Net;

namespace Api.FunctionalTests.Support;

/// <summary>Grava os pedidos HTTP de saída sem os enviar.</summary>
public sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    /// <summary>Status devolvido ao adapter. Alterável para provar caminhos de falha.</summary>
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;

    /// <summary>Pedidos gravados, pela ordem em que foram feitos.</summary>
    public IReadOnlyCollection<RecordedRequest> Requests => _requests;

    /// <summary>Esvazia o histórico entre testes da mesma coleção.</summary>
    public void Clear() => _requests.Clear();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // O corpo é lido aqui e guardado como string. Depois de a resposta
        // ser devolvida, o HttpClient liberta o conteúdo do pedido e a asserção
        // no teste encontraria um stream já fechado.
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        _requests.Enqueue(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.ToString(),
            body));

        return new HttpResponseMessage(ResponseStatusCode)
        {
            RequestMessage = request
        };
    }

    /// <summary>Instantâneo imutável de um pedido de saída.</summary>
    public sealed record RecordedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        string? Authorization,
        string Body);
}
