namespace BlastRadius.Sdk.Configuration;

/// <summary>
/// Configuration options for initializing the BlastRadius SDK.
/// </summary>
public sealed class BlastRadiusOptions
{
    /// <summary>
    /// Base URL of the BlastRadius Control Plane API (e.g., "https://api.blastradius.internal" or "https://localhost:5001").
    /// Used for initial snapshot pre-warming and periodic synchronization.
    /// </summary>
    public string ControlPlaneUrl { get; set; } = string.Empty;

    /// <summary>
    /// Redis connection string (e.g., "your-upstash.upstash.io:6379,password=...,ssl=True").
    /// </summary>
    public string RedisConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Target environment partition (e.g., "production", "staging", "development").
    /// Default: "production".
    /// </summary>
    public string Environment { get; set; } = "production";

    /// <summary>
    /// Enforces TLS/SSL connection to Redis. Defaults to <c>true</c> for Upstash compatibility.
    /// </summary>
    public bool Ssl { get; set; } = true;

    /// <summary>
    /// Connection timeout in milliseconds for Redis client establishment.
    /// Default: 5000 ms.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Redis Pub/Sub channel used for broadcasting real-time flag invalidations and kill signals.
    /// Default: "flag-updates".
    /// </summary>
    public string PubSubChannel { get; set; } = "flag-updates";

    /// <summary>
    /// Interval in seconds for the background snapshot poller.
    /// Serves as a defensive safety net in case Redis Pub/Sub packets are dropped during network partitions.
    /// Default: 60 seconds.
    /// </summary>
    public int SyncIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Enables asynchronous background telemetry reporting for sliding-window circuit breaker tracking.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Interval in milliseconds between asynchronous telemetry flush cycles to Redis.
    /// Default: 1000 ms (1 second).
    /// </summary>
    public int TelemetryFlushIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Maximum count of buffered evaluation events before forcing an immediate batch flush to Redis.
    /// Default: 200 items.
    /// </summary>
    public int TelemetryBatchSize { get; set; } = 200;
}
