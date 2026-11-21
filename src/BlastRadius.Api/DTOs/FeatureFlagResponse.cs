namespace BlastRadius.Api.DTOs;

public sealed class FeatureFlagResponse
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; }
    public int RolloutPercentage { get; init; }
    public long Version { get; init; }
    public bool FallbackValue { get; init; }
    public string? KillReason { get; init; }
    public string Environment { get; init; } = string.Empty;
    public BlastRadiusPolicyDto? BlastRadiusPolicy { get; init; }
    public List<TargetingRuleDto> TargetingRules { get; init; } = new();
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class AuditLogResponse
{
    public Guid Id { get; init; }
    public string FlagKey { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string TriggeredBy { get; init; } = string.Empty;
    public string? Details { get; init; }
    public long Version { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
}

public sealed class TelemetryStatusResponse
{
    public string FlagKey { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public long TotalRequestsInWindow { get; init; }
    public long ErrorRequestsInWindow { get; init; }
    public double ErrorRatePercent { get; init; }
    public int SlidingWindowSeconds { get; init; }
    public bool BreachedThreshold { get; init; }
    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
