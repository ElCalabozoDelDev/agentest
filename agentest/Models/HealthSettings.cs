namespace agentest.Models;

public sealed class HealthSettings
{
    public string Endpoint { get; set; } = "";
    public string Method { get; set; } = "GET";
    public int IntervalSeconds { get; set; } = 30;
    public int TimeoutSeconds { get; set; } = 5;
    public int ExpectedStatusCode { get; set; } = 200;
    public string ResponseValuePath { get; set; } = "";
    public string ExpectedValue { get; set; } = "ok";
    public string AuthenticationHeader { get; set; } = "Authorization";
    public string AuthenticationPrefix { get; set; } = "Bearer";
}

public sealed record AgentHealth(string State, string Detail, DateTimeOffset CheckedAt, long LatencyMilliseconds);
