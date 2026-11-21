namespace BlastRadius.Sdk.Models;

/// <summary>
/// Defines blast-radius containment constraints and automated circuit-breaker thresholds for a feature flag.
/// Used by the Control Plane Circuit Breaker worker to autonomously trip flags when error rates spike.
/// </summary>
public sealed class BlastRadiusPolicy
{
    /// <summary>
    /// Maximum allowable error rate percentage before the circuit breaker trips (e.g., 2.0 for 2%).
    /// </summary>
    public double MaxErrorRatePercent { get; init; } = 2.0;

    /// <summary>
    /// Minimum number of total requests required within the sliding window before the circuit breaker is eligible to trip.
    /// Prevents premature circuit-breaking on small sample sizes (e.g., 1 failure out of 2 requests).
    /// </summary>
    public int MinRequestsThreshold { get; init; } = 50;

    /// <summary>
    /// The sliding telemetry evaluation window in seconds (e.g., 60 seconds).
    /// </summary>
    public int SlidingWindowSeconds { get; init; } = 60;

    /// <summary>
    /// Whether autonomous kill-switch execution is enabled for this flag.
    /// </summary>
    public bool AutoKillEnabled { get; init; } = true;

    /// <summary>
    /// Cooldown period in seconds before the flag can be re-evaluated or gradually restored.
    /// </summary>
    public int CooldownSeconds { get; init; } = 300;
}
