using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlastRadius.Api.Data.Entities;

/// <summary>
/// Defines automated circuit-breaker thresholds and sliding-window rules for a feature flag.
/// Used by the CircuitBreakerWorker to autonomously isolate failing code paths.
/// </summary>
public sealed class BlastRadiusPolicyEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid FeatureFlagId { get; set; }

    [ForeignKey(nameof(FeatureFlagId))]
    public FeatureFlagEntity? FeatureFlag { get; set; }

    /// <summary>
    /// Max tolerable error rate percentage before tripping the breaker (e.g. 2.0 = 2%).
    /// </summary>
    public double MaxErrorRatePercent { get; set; } = 2.0;

    /// <summary>
    /// Minimum total evaluations required in sliding window before breaker can trip.
    /// Prevents false positives on low-volume traffic.
    /// </summary>
    public int MinRequestsThreshold { get; set; } = 50;

    /// <summary>
    /// Sliding telemetry evaluation window in seconds (default: 60s).
    /// </summary>
    public int SlidingWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Master toggle for autonomous kill-switch execution.
    /// </summary>
    public bool AutoKillEnabled { get; set; } = true;

    /// <summary>
    /// Cooldown period in seconds before manual or automatic reinstatement is permitted.
    /// </summary>
    public int CooldownSeconds { get; set; } = 300;
}
