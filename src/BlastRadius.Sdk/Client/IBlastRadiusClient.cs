using BlastRadius.Sdk.Models;

namespace BlastRadius.Sdk.Client;

/// <summary>
/// Primary entry point for evaluating feature flags in host applications.
/// Guarantees sub-millisecond local in-memory evaluation and absolute fail-safe operation.
/// </summary>
public interface IBlastRadiusClient : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Evaluates whether a feature flag is enabled for the provided execution context.
    /// Returns boolean result directly for high-frequency if/else branching.
    /// </summary>
    /// <param name="flagKey">The unique feature flag key (e.g., "new-upi-checkout").</param>
    /// <param name="context">The contextual attributes (userId, attributes, etc.).</param>
    /// <param name="defaultValue">Fallback value if the flag is missing, disabled, or an anomaly occurs.</param>
    /// <returns><c>true</c> if the feature should be served; otherwise <c>false</c>.</returns>
    bool IsEnabled(string flagKey, EvaluationContext? context = null, bool defaultValue = false);

    /// <summary>
    /// Evaluates a feature flag and returns detailed diagnostic metadata (<see cref="EvaluationResult"/>).
    /// Useful for logging, debugging, or response header tagging.
    /// </summary>
    /// <param name="flagKey">The unique feature flag key.</param>
    /// <param name="context">The contextual attributes.</param>
    /// <param name="defaultValue">Fallback value if the flag is missing or disabled.</param>
    /// <returns>Complete evaluation result including bucket index, reason, and elapsed microseconds.</returns>
    EvaluationResult Evaluate(string flagKey, EvaluationContext? context = null, bool defaultValue = false);

    /// <summary>
    /// Reports an execution outcome (success or error) for automated sliding-window circuit breaker tracking.
    /// Call with <c>isError: true</c> in application exception handlers to feed the blast-radius kill switch.
    /// </summary>
    /// <param name="flagKey">The flag key associated with the executed feature.</param>
    /// <param name="isError"><c>true</c> if execution threw an exception or failed; <c>false</c> if successful.</param>
    void RecordTelemetry(string flagKey, bool isError);

    /// <summary>
    /// Returns all feature flags currently loaded in the local in-memory store.
    /// </summary>
    IReadOnlyCollection<FeatureFlagConfig> GetAllFlags();

    /// <summary>
    /// Pre-warms the cache from the Control Plane HTTP API and establishes Redis Pub/Sub connectivity.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
