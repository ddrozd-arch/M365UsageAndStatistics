namespace GraphSdk;

public sealed class GraphClientOptions
{
    public int MaxRetries { get; set; } = 5;

    public int DelayBetweenRequestsMs { get; set; } = 0;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
}