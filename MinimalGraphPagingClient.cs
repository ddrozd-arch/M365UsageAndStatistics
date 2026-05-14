using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace MyCompany.Graph;

public class MinimalGraphPagingClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private readonly ILogger<MinimalGraphPagingClient> _logger;

    public MinimalGraphPagingClient(
        HttpClient httpClient,
        TokenCredential credential,
        ILogger<MinimalGraphPagingClient> logger)
    {
        _httpClient = httpClient;
        _credential = credential;
        _logger = logger;
    }

    public async IAsyncEnumerable<JsonElement> GetPagedAsync(
        string endpoint,
        bool beta = false,
        TimeSpan? delayBetweenRequests = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        endpoint = endpoint.TrimStart('/');

        var version = beta ? "beta" : "v1.0";

        string? nextUrl =
            $"https://graph.microsoft.com/{version}/{endpoint}";

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            if (delayBetweenRequests.HasValue)
            {
                await Task.Delay(
                    delayBetweenRequests.Value,
                    cancellationToken);
            }

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    nextUrl);

            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(new[]
                {
                    "https://graph.microsoft.com/.default"
                }),
                cancellationToken);

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token.Token);

            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));

            _logger.LogInformation(
                "Graph GET {Url}",
                nextUrl);

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            // THROTTLING
            if (response.StatusCode == (HttpStatusCode)429)
            {
                var retryAfter =
                    response.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(30);

                _logger.LogWarning(
                    "Graph throttling detected. Waiting {Delay}",
                    retryAfter);

                await Task.Delay(
                    retryAfter,
                    cancellationToken);

                continue;
            }

            var content =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Graph error {StatusCode}: {Content}",
                    response.StatusCode,
                    content);

                throw new Exception(
                    $"Graph GET failed: {response.StatusCode}");
            }

            using var document =
                JsonDocument.Parse(content);

            if (document.RootElement.TryGetProperty(
                    "value",
                    out var values))
            {
                foreach (var item in values.EnumerateArray())
                {
                    yield return item;
                }
            }

            nextUrl = null;

            if (document.RootElement.TryGetProperty(
                    "@odata.nextLink",
                    out var nextLink))
            {
                nextUrl = nextLink.GetString();
            }
        }
    }
}