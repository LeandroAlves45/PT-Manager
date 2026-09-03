using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.FunctionalTests.Support;

/// <summary>
/// Envia contratos da API já serializados com a mesma política de nomes que a API
/// aplica na desserialização.
/// </summary>
internal static class ApiJsonPayload
{
    /// <summary>Opções equivalentes às configuradas em <c>Api.DependencyInjection</c>.</summary>
    internal static readonly JsonSerializerOptions Options = Create();

    internal static Task<HttpResponseMessage> PostAsync<TRequest>(
        HttpClient client,
        string route,
        TRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.PostAsJsonAsync(route, request, Options, cancellationToken);
    }

    internal static Task<HttpResponseMessage> PutAsync<TRequest>(
        HttpClient client,
        string route,
        TRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.PutAsJsonAsync(route, request, Options, cancellationToken);
    }

    internal static Task<HttpResponseMessage> PatchAsync<TRequest>(
        HttpClient client,
        string route,
        TRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.PatchAsJsonAsync(route, request, Options, cancellationToken);
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
