namespace BlastRadius.Sdk.Models;

/// <summary>
/// Represents the cached, in-memory configuration of an individual feature flag.
/// Stored in a thread-safe <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/> for sub-microsecond lookups.
/// </summary>
public sealed class FeatureFlagConfig
{
    /// <summary>
    /// Unique identifier key for the feature flag (e.g., "new-upi-checkout", "v2-search-ranking").
    /// </summary>
    public string FlagKey { get; init; } = string.Empty;

    /// <summary>
    /// Master toggle for the feature. If <c>false</c>, evaluation short-circuits immediately without evaluating rules or percentage.
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// Percentage of users (0 to 100) that receive the feature via MurmurHash3 deterministic bucketing.
    /// Only evaluated if no targeting rule matched.
    /// </summary>
    public int RolloutPercentage { get; set; } = 0;

    /// <summary>
    /// Monotonically increasing version number. Ensures newer updates from Redis Pub/Sub or HTTP snapshots
    /// never get overwritten by stale or out-of-order updates.
    /// </summary>
    public long Version { get; set; } = 1;

    /// <summary>
    /// Default fallback boolean value returned if unexpected exceptions occur.
    /// </summary>
    public bool FallbackValue { get; init; } = false;

    /// <summary>
    /// Collection of targeted audience rules, evaluated in ascending order of <see cref="TargetingRule.Priority"/>.
    /// </summary>
    public IReadOnlyList<TargetingRule> Rules { get; init; } = Array.Empty<TargetingRule>();

    /// <summary>
    /// Blast-radius automated containment policy defining error rate limits and circuit breaker trip criteria.
    /// </summary>
    public BlastRadiusPolicy? BlastRadiusPolicy { get; init; }

    /// <summary>
    /// If the flag was disabled autonomously by the circuit breaker or an operator, describes the reason.
    /// </summary>
    public string? KillReason { get; set; }

    /// <summary>
    /// Target deployment environment (e.g., "production", "staging", "development").
    /// </summary>
    public string Environment { get; init; } = "production";

    /// <summary>
    /// Timestamp when this configuration was last modified.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional metadata tags for filtering and grouping.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
