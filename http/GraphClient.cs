using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GraphSdk;

public sealed class GraphClient
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly GraphClientOptions _options;

    public GraphClient(
        HttpClient httpClient,
        string token,
        GraphClientOptions? options = null)
    {
        _httpClient = httpClient;
        _token = token;
        _options = options ?? new GraphClientOptions();

        _httpClient.Timeout = _options.Timeout;
    }

    public async Task<GraphResponse> GetAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var retry = 0;

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);

            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            // THROTTLING
            if ((int)response.StatusCode == 429)
            {
                retry++;

                if (retry > _options.MaxRetries)
                {
                    throw new Exception("Max retry exceeded.");
                }

                var retryAfter =
                    response.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(30);

                await Task.Delay(retryAfter, cancellationToken);

                continue;
            }

            // RETRY 5xx
            if ((int)response.StatusCode >= 500)
            {
                retry++;

                if (retry > _options.MaxRetries)
                {
                    throw new Exception("Max retry exceeded.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Pow(2, retry)),
                    cancellationToken);

                continue;
            }

            response.EnsureSuccessStatusCode();

            if (_options.DelayBetweenRequestsMs > 0)
            {
                await Task.Delay(
                    _options.DelayBetweenRequestsMs,
                    cancellationToken);
            }

            var contentType =
                response.Content.Headers.ContentType?.MediaType?.ToLower();

            var type = DetectContentType(contentType);

            var stream =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            return new GraphResponse
            {
                StatusCode = response.StatusCode,
                ContentType = type,
                ContentTypeHeader = contentType,
                ContentStream = stream,
                RawResponse = response
            };
        }
    }

    public async Task<List<T>> GetPagedAsync<T>(
        string url,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<T>();

        while (!string.IsNullOrWhiteSpace(url))
        {
            using var response =
                await GetAsync(url, cancellationToken);

            if (response.ContentType != GraphContentType.Json)
            {
                throw new Exception(
                    "Paged endpoint must return JSON.");
            }

            var odata =
                await JsonSerializer.DeserializeAsync<ODataResponse<T>>(
                    response.ContentStream,
                    jsonOptions,
                    cancellationToken);

            if (odata?.Value != null)
            {
                results.AddRange(odata.Value);
            }

            url = odata?.NextLink ?? string.Empty;
        }

        return results;
    }

    public async Task<string> ReadAsStringAsync(
        GraphResponse response,
        CancellationToken cancellationToken = default)
    {
        using var reader =
            new StreamReader(response.ContentStream);

        return await reader.ReadToEndAsync(cancellationToken);
    }

    public async Task SaveToFileAsync(
        GraphResponse response,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var file =
            File.Create(filePath);

        await response.ContentStream.CopyToAsync(
            file,
            cancellationToken);
    }

    private static GraphContentType DetectContentType(
        string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return GraphContentType.Unknown;
        }

        if (contentType.Contains("json"))
        {
            return GraphContentType.Json;
        }

        if (contentType.Contains("csv") ||
            contentType.Contains("text/plain"))
        {
            return GraphContentType.Csv;
        }

        return GraphContentType.Binary;
    }
}