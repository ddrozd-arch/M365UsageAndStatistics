using System.Text.Json.Serialization;

namespace GraphSdk;

public sealed class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T>? Value { get; set; }

    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; set; }
}