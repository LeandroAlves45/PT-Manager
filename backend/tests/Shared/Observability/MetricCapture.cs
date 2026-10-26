using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace TestSupport.Observability;

/// <summary>Captura as medições de um instrumento feitas no fluxo assíncrono do teste.</summary>
/// <remarks>
/// Os meters do PT Manager são estáticos e os testes correm em paralelo: outro teste a
/// rejeitar um webhook ao mesmo tempo contaria aqui. O <see cref="AsyncLocal{T}"/> marca o
/// fluxo deste teste e o callback do listener, que corre no fluxo de quem mede, ignora o
/// resto. Para o pedido HTTP herdar o fluxo, o TestServer precisa de
/// <c>PreserveExecutionContext = true</c>.
/// </remarks>
internal sealed class MetricCapture : IDisposable
{
    private static readonly AsyncLocal<MetricCapture?> Owner = new();

    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<CapturedMeasurement> _measurements = new();

    public MetricCapture(string meterName, params string[] instrumentNames)
    {
        Owner.Value = this;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == meterName && instrumentNames.Contains(instrument.Name))
                listener.EnableMeasurementEvents(instrument, this);
        };
        _listener.SetMeasurementEventCallback<long>(OnMeasurement);
        _listener.SetMeasurementEventCallback<int>(OnMeasurement);
        _listener.SetMeasurementEventCallback<double>(OnMeasurement);
        _listener.Start();
    }

    public IReadOnlyList<CapturedMeasurement> Measurements => _measurements.ToArray();

    public void Dispose() => _listener.Dispose();

    private void OnMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
        where T : struct
    {
        if (!ReferenceEquals(Owner.Value, state))
            return;

        _measurements.Enqueue(new CapturedMeasurement(
            instrument.Name,
            Convert.ToDouble(measurement, System.Globalization.CultureInfo.InvariantCulture),
            tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
    }
}

internal sealed record CapturedMeasurement(
    string Instrument,
    double Value,
    IReadOnlyDictionary<string, object?> Tags);
