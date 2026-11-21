namespace BlastRadius.Sdk.Telemetry;

/// <summary>
/// Contract for non-blocking feature flag telemetry ingestion.
/// Feeds sliding-window error rate aggregations for autonomous circuit breaking.
/// </summary>
public interface ITelemetryReporter : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Enqueues a non-blocking telemetry event for the given feature flag.
    /// Completes in sub-microsecond time via lock-free channel buffering.
    /// </summary>
    /// <param name="flagKey">Key of the evaluated feature flag.</param>
    /// <param name="isError"><c>true</c> if execution encountered an unhandled exception or business fault; <c>false</c> if successful.</param>
    void Record(string flagKey, bool isError);

    /// <summary>
    /// Starts the background channel consumer and batch flusher.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);
}
