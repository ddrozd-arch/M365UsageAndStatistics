using System.Net;

namespace GraphSdk;

public sealed class GraphResponse : IDisposable
{
    public HttpStatusCode StatusCode { get; init; }

    public GraphContentType ContentType { get; init; }

    public string? ContentTypeHeader { get; init; }

    public Stream ContentStream { get; init; } = default!;

    public HttpResponseMessage RawResponse { get; init; } = default!;

    public void Dispose()
    {
        ContentStream.Dispose();
        RawResponse.Dispose();
    }
}