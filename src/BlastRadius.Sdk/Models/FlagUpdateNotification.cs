namespace BlastRadius.Sdk.Models;

/// <summary>
/// Distributed real-time event published by the Control Plane to the Redis Pub/Sub channel 'flag-updates'.
/// Fleet SDK instances consume this event to invalidate or update their in-memory cache within milliseconds.
/// </summary>
public sealed class FlagUpdateNotification
{
    /// <summary>
    /// Supported real-time sync actions.
    /// </summary>
    public static class Actions
    {
        public const string Updated = "Updated";
        public const string Deleted = "Deleted";
        public const string CircuitBreakerKilled = "CircuitBreakerKilled";
    }

    /// <summary>
    /// Key of the flag that was modified.
    /// </summary>
    public string FlagKey { get; init; } = string.Empty;

    /// <summary>
    /// Action performed on the flag (<see cref="Actions"/>).
    /// </summary>
    public string Action { get; init; } = Actions.Updated;

    /// <summary>
    /// New version number of the flag configuration.
    /// Used by SDK nodes to ignore out-of-order or stale pub/sub messages.
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// Optional updated flag configuration payload, allowing zero-HTTP-call in-place cache updates.
    /// </summary>
    public FeatureFlagConfig? Config { get; init; }

    /// <summary>
    /// Reason recorded if the flag was terminated by the autonomous circuit breaker.
    /// </summary>
    public string? KillReason { get; init; }

    /// <summary>
    /// UTC timestamp of the mutation.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
