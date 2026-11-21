namespace BlastRadius.Sdk.Telemetry;

/// <summary>
/// Individual telemetry data point recorded after an evaluation or feature execution.
/// </summary>
public readonly record struct TelemetryMetric(
    string FlagKey,
    bool IsError,
    DateTimeOffset TimestampUtc);
