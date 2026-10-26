using System.Diagnostics;
using Application.Common.Observability;

namespace Application.UnitTests.Common.Observability;

/// <summary>
/// O correlation id de um job criado num pedido é o trace id desse pedido, salvo quando
/// o trace veio de fora.
/// </summary>
public sealed class OperationCorrelationTests : IDisposable
{
    private readonly ActivitySource _source = new($"ptm-tests-{Guid.NewGuid():N}");
    private readonly ActivityListener _listener;

    public OperationCorrelationTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source == _source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public void WithoutActivity_ReturnsADistinctRandomId()
    {
        Assert.Null(Activity.Current);

        var first = OperationCorrelation.NewId();
        var second = OperationCorrelation.NewId();

        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void LocalRootActivity_ReturnsTheTraceIdInNFormat()
    {
        using var request = _source.StartActivity("request")!;

        var id = OperationCorrelation.NewId();

        Assert.Equal(request.TraceId.ToHexString(), id.ToString("N"));
    }

    [Fact]
    public void ChildOfLocalRoot_ReturnsTheSameTraceId()
    {
        using var request = _source.StartActivity("request")!;
        using var child = _source.StartActivity("query")!;

        var id = OperationCorrelation.NewId();

        Assert.Equal(request.TraceId.ToHexString(), id.ToString("N"));
    }

    [Fact]
    public void RemoteParent_IsNotTrusted()
    {
        // O traceparent é escolhido pelo cliente: dois pedidos com o mesmo valor
        // colidiriam nas chaves de idempotência que usam o correlation id.
        var remote = new ActivityContext(
            ActivityTraceId.CreateFromString("7a7a7a7a7a7a7a7a7a7a7a7a7a7a7a7a"),
            ActivitySpanId.CreateFromString("1111111111111111"),
            ActivityTraceFlags.Recorded,
            isRemote: true);
        using var request = _source.StartActivity("request", ActivityKind.Server, remote)!;
        using var child = _source.StartActivity("handler")!;

        var id = OperationCorrelation.NewId();

        Assert.True(request.HasRemoteParent);
        Assert.NotEqual(request.TraceId.ToHexString(), id.ToString("N"));
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public void RootWithStringParentId_IsNotTrusted()
    {
        // O caminho do hosting sem listener: new Activity + SetParentId(traceparent).
        using var request = new Activity("request")
            .SetParentId("00-7a7a7a7a7a7a7a7a7a7a7a7a7a7a7a7a-1111111111111111-01")
            .Start();

        var id = OperationCorrelation.NewId();

        Assert.NotEqual(request.TraceId.ToHexString(), id.ToString("N"));
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }
}
