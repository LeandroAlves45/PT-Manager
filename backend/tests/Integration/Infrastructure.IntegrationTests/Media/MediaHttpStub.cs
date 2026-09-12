using System.Net;
using System.Text;

namespace Infrastructure.IntegrationTests.Media;

/// <summary>
/// HttpMessageHandler guionizado para os adapters de media. Grava cada pedido,
/// incluindo o corpo já materializado, para que os testes possam provar o que
/// atravessa a fronteira sem rede real.
/// </summary>
internal sealed class MediaHttpStub : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public MediaHttpStub Respond(HttpStatusCode status, string body)
    {
        _responses.Enqueue(() => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        return this;
    }

    public MediaHttpStub Respond(HttpContent content)
    {
        _responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        return this;
    }

    public MediaHttpStub Throw(Exception exception)
    {
        _responses.Enqueue(() => throw exception);
        return this;
    }

    public HttpClient CreateClient(string baseAddress) =>
        new(this) { BaseAddress = new Uri(baseAddress) };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (request.Content is MultipartFormDataContent multipart)
        {
            foreach (var part in multipart)
            {
                var name = part.Headers.ContentDisposition?.Name?.Trim('"');
                if (name is not null && part is StringContent)
                    fields[name] = await part.ReadAsStringAsync(cancellationToken);
            }
        }

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString(),
            body,
            fields));

        if (_responses.Count == 0)
            throw new InvalidOperationException("No scripted response left.");

        return _responses.Dequeue()();
    }

    internal sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        string Body,
        IReadOnlyDictionary<string, string> Fields);
}

/// <summary>Simula headers imediatos e um corpo lento, sem depender de rede.</summary>
internal sealed class DelayedMediaContent(string body) : HttpContent
{
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(
        Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(body), cancellationToken);
    }
}
