namespace BlastRadius.Api.Services;

/// <summary>
/// Contract for reading sliding-window telemetry counters from Redis.
/// Feeds real-time anomaly detection into the automated circuit-breaker worker.
/// </summary>
public interface ITelemetryReader
{
    /// <summary>
    /// Reads aggregated request and error counts for a specific flag over a sliding time window.
    /// </summary>
    /// <param name="flagKey">The feature flag key.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="slidingWindowSeconds">Duration of the sliding window in seconds (e.g., 60s).</param>
    /// <returns>Total requests, error count, and error rate percentage.</returns>
    Task<(long TotalRequests, long ErrorRequests, double ErrorRatePercent)> GetMetricsInSlidingWindowAsync(
        string flagKey,
        string environment,
        int slidingWindowSeconds);
}
