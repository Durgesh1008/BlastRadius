using BlastRadius.Sdk.Models;

namespace BlastRadius.Sdk.Store;

/// <summary>
/// Thread-safe in-memory cache and synchronization store for feature flags.
/// Serves sub-microsecond local reads and synchronizes state via Redis Pub/Sub and HTTP pre-warming.
/// </summary>
public interface IInMemoryFlagStore : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Retrieves a flag configuration by key from the local thread-safe memory store.
    /// Returns null if the flag is not registered.
    /// </summary>
    FeatureFlagConfig? GetFlag(string flagKey);

    /// <summary>
    /// Returns all feature flags currently cached in memory.
    /// </summary>
    IReadOnlyCollection<FeatureFlagConfig> GetAllFlags();

    /// <summary>
    /// Updates or inserts a flag configuration into the local memory store.
    /// Enforces optimistic version checks to prevent stale overwrites.
    /// </summary>
    void UpdateOrAddFlag(FeatureFlagConfig flag);

    /// <summary>
    /// Removes a flag configuration from the local cache.
    /// </summary>
    void RemoveFlag(string flagKey);

    /// <summary>
    /// Atomically trips a flag's kill switch in memory, disabling it immediately (in less than 1 millisecond).
    /// </summary>
    void MarkKilled(string flagKey, string killReason, long version);

    /// <summary>
    /// Pre-warms the cache from the Control Plane HTTP snapshot and subscribes to Redis Pub/Sub.
    /// Safe to call during application startup (e.g., hosted service or DI container initialization).
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether the store has completed its initial snapshot bootstrap.
    /// </summary>
    bool IsInitialized { get; }
}
