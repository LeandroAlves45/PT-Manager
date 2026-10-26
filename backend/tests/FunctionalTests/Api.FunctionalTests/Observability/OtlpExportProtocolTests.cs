using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Api.FunctionalTests.Support;
using Application.Common.Observability;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Api.FunctionalTests.Observability;

/// <summary>
/// Traces e métricas saem em OTLP http/protobuf, o único protocolo que o Grafana Cloud aceita.
/// Com o protocolo por omissão (gRPC), nada chegava e nenhum erro aparecia.
/// </summary>
/// <remarks>
/// Um coletor falso em loopback regista a primeira linha e o Content-Type de cada pedido e
/// responde 200. Em gRPC a primeira linha seria o preface do HTTP/2 (<c>PRI * HTTP/2.0</c>).
/// Fica na coleção da API para as spans e medições forçadas não chegarem a outros testes.
/// </remarks>
[Collection(ApiTestCollection.Name)]
public sealed class OtlpExportProtocolTests
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused";

    [Fact]
    public async Task TracesAndMetrics_AreExportedAsHttpProtobuf()
    {
        using var collector = new FakeOtlpCollector();
        using var factory = new ApiWebApplicationFactory(UnusedConnectionString)
        {
            AdditionalSettings = new Dictionary<string, string>
            {
                ["Observability:Enabled"] = "true",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = collector.Endpoint,
            }
        };

        var tracer = factory.Services.GetRequiredService<TracerProvider>();
        var meter = factory.Services.GetRequiredService<MeterProvider>();

        using (PtManagerTelemetry.ActivitySource.StartActivity("otlp-protocol-probe")) { }
        PtManagerTelemetry.RequestDbCommands.Record(
            1, new KeyValuePair<string, object?>("http.route", "otlp-protocol-probe"));

        tracer.ForceFlush(timeoutMilliseconds: 5_000);
        meter.ForceFlush(timeoutMilliseconds: 5_000);

        var requests = await collector.WaitForAsync(2, TimeSpan.FromSeconds(5));
        var lines = string.Join(" | ", requests.Select(request => request.FirstLine));

        Assert.Contains(requests, request =>
            request.FirstLine == "POST /v1/traces HTTP/1.1" &&
            request.ContentType == "application/x-protobuf");
        Assert.True(
            requests.Any(request =>
                request.FirstLine == "POST /v1/metrics HTTP/1.1" &&
                request.ContentType == "application/x-protobuf"),
            $"Expected an OTLP/HTTP metrics export; the collector saw: {lines}");
    }

    private sealed record CollectedRequest(string FirstLine, string? ContentType);

    /// <summary>Servidor HTTP/1.1 mínimo: lê cabeçalhos e corpo, responde 200 sem corpo.</summary>
    private sealed class FakeOtlpCollector : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentQueue<CollectedRequest> _requests = new();

        public FakeOtlpCollector()
        {
            _listener.Start();
            _ = AcceptLoopAsync();
        }

        public string Endpoint => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        public async Task<IReadOnlyList<CollectedRequest>> WaitForAsync(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (_requests.Count < count && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            return _requests.ToArray();
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _stop.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    _ = ServeAsync(client);
                }
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    while (!_stop.IsCancellationRequested)
                    {
                        var head = await ReadHeadAsync(stream);
                        if (head is null)
                            return;

                        var lines = head.Split("\r\n");
                        var headers = lines.Skip(1)
                            .Select(line => line.Split(':', 2))
                            .Where(parts => parts.Length == 2)
                            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
                        _requests.Enqueue(new CollectedRequest(
                            lines[0], headers.GetValueOrDefault("Content-Type")));

                        // gRPC: não há resposta HTTP/1.1 possível; o exporter desiste.
                        if (!lines[0].EndsWith("HTTP/1.1", StringComparison.Ordinal))
                            return;

                        if (headers.TryGetValue("Content-Length", out var length) && int.Parse(length) > 0)
                            await ReadExactlyAsync(stream, int.Parse(length));

                        var response = Encoding.ASCII.GetBytes(
                            "HTTP/1.1 200 OK\r\nContent-Type: application/x-protobuf\r\nContent-Length: 0\r\n\r\n");
                        await stream.WriteAsync(response, _stop.Token);
                    }
                }
                catch (Exception) when (_stop.IsCancellationRequested)
                {
                }
                catch (IOException)
                {
                }
            }
        }

        private async Task<string?> ReadHeadAsync(NetworkStream stream)
        {
            var buffer = new List<byte>();
            var one = new byte[1];
            while (buffer.Count < 64 * 1024)
            {
                if (await stream.ReadAsync(one, _stop.Token) == 0)
                    return null;

                buffer.Add(one[0]);
                var count = buffer.Count;
                if (count >= 4 && buffer[count - 4] == '\r' && buffer[count - 3] == '\n' &&
                    buffer[count - 2] == '\r' && buffer[count - 1] == '\n')
                    return Encoding.ASCII.GetString(buffer.ToArray(), 0, count - 4);
            }

            return null;
        }

        private async Task ReadExactlyAsync(NetworkStream stream, int length)
        {
            var body = new byte[length];
            await stream.ReadExactlyAsync(body, _stop.Token);
        }
    }
}
