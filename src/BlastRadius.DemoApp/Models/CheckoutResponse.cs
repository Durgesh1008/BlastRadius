namespace BlastRadius.DemoApp.Models;

/// <summary>
/// Detailed result returned to the client after checkout evaluation and payment execution.
/// </summary>
public sealed class CheckoutResponse
{
    public string OrderId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string GatewayUsed { get; init; } = "LegacyCard"; // "NewInstantUPI" or "LegacyCard"
    public string Message { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public long LatencyMs { get; init; }
    public int? MurmurHashBucket { get; init; }
    public string EvaluationReason { get; init; } = string.Empty;
    public long FeatureFlagVersion { get; init; }
    public string? ErrorDetails { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
