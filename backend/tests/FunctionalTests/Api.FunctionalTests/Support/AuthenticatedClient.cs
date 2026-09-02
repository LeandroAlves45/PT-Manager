using System.Net.Http.Headers;

namespace Api.FunctionalTests.Support;

/// <summary>Aplica um access token a um cliente HTTP de teste.</summary>
internal static class AuthenticatedClient
{
    internal static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }
}
