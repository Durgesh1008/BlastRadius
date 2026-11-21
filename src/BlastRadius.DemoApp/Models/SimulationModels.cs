namespace BlastRadius.DemoApp.Models;

/// <summary>
/// Runtime chaos configuration for injecting controlled faults into the new feature.
/// </summary>
public sealed class ChaosConfig
{
    /// <summary>
    /// Simulated failure rate percentage when executing the new feature path (default: 15%).
    /// </summary>
    public double FailureRatePercent { get; set; } = 15.0;

    /// <summary>
    /// Simulated payment processing latency in milliseconds.
    /// </summary>
    public int SimulateLatencyMs { get; set; } = 30;

    /// <summary>
    /// Whether chaos injection is actively enabled.
    /// </summary>
    public bool IsFaultInjectionActive { get; set; } = true;
}

public sealed class BatchSimulationRequest
{
    public int TotalOrders { get; init; } = 50;
    public double? OverrideFailureRate { get; init; }
    public int Concurrency { get; init; } = 5;
    public string? FlagKey { get; init; } = "new-upi-checkout";
}

public sealed class BatchSimulationResponse
{
    public int TotalOrders { get; init; }
    public int SuccessOrders { get; init; }
    public int FailedOrders { get; init; }
    public int NewUpiOrders { get; init; }
    public int LegacyOrders { get; init; }
    public double ActualErrorRatePercent { get; init; }
    public long DurationMs { get; init; }
    public bool BreakerTrippedDuringRun { get; init; }
    public List<CheckoutResponse> SampleTransactions { get; init; } = new();
}

public sealed class LiveStatsResponse
{
    public long TotalTransactions { get; init; }
    public long SuccessfulTransactions { get; init; }
    public long FailedTransactions { get; init; }
    public long NewUpiCount { get; init; }
    public long LegacyCardCount { get; init; }
    public double ErrorRatePercent { get; init; }
    public bool IsNewUpiFlagEnabled { get; init; }
    public int RolloutPercentage { get; init; }
    public long FlagVersion { get; init; }
    public string? KillReason { get; init; }
    public ChaosConfig Chaos { get; init; } = new();
    public List<CheckoutResponse> RecentTransactions { get; init; } = new();
}

/// <summary>
/// Payload to evaluate any feature flag in real time with dynamic user context.
/// </summary>
public sealed class FlagEvaluationRequest
{
    public string FlagKey { get; init; } = "new-upi-checkout";
    public string UserId { get; init; } = string.Empty;
    public string? UserTier { get; init; }
    public string? City { get; init; }
    public Dictionary<string, string>? CustomAttributes { get; init; }
}

/// <summary>
/// Real-time evaluation output with complete MurmurHash3 mathematical proof.
/// </summary>
public sealed class FlagEvaluationResponse
{
    public string FlagKey { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public int RolloutPercentage { get; init; }
    public bool Value { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? MatchedRuleId { get; init; }
    public int? Bucket { get; init; }
    public long Version { get; init; }
    public long ExecutionTimeMicroseconds { get; init; }
    public uint RawHash { get; init; }
    public string CompositeKey { get; init; } = string.Empty;
    public string? KillReason { get; init; }
}
