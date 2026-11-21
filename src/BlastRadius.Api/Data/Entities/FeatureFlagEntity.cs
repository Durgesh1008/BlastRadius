using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlastRadius.Api.Data.Entities;

/// <summary>
/// Database persistence entity for a feature flag.
/// Uses optimistic concurrency (<see cref="Version"/>) to prevent lost updates across concurrent admin sessions.
/// </summary>
public sealed class FeatureFlagEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique key identifier within the environment (e.g. 'new-upi-checkout').
    /// </summary>
    [Required]
    [MaxLength(150)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Master toggle for the feature.
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// Canary rollout percentage (0 to 100).
    /// </summary>
    [Range(0, 100)]
    public int RolloutPercentage { get; set; } = 0;

    /// <summary>
    /// Monotonically increasing version counter for optimistic concurrency and fleet-wide sync tracking.
    /// </summary>
    [ConcurrencyCheck]
    public long Version { get; set; } = 1;

    /// <summary>
    /// Safe fallback value served if an error occurs.
    /// </summary>
    public bool FallbackValue { get; set; } = false;

    /// <summary>
    /// Reason recorded if disabled manually or tripped autonomously by the circuit breaker.
    /// </summary>
    [MaxLength(500)]
    public string? KillReason { get; set; }

    [Required]
    [MaxLength(50)]
    public string EnvironmentId { get; set; } = "production";

    [ForeignKey(nameof(EnvironmentId))]
    public EnvironmentEntity? Environment { get; set; }

    public BlastRadiusPolicyEntity? BlastRadiusPolicy { get; set; }

    public ICollection<TargetingRuleEntity> TargetingRules { get; set; } = new List<TargetingRuleEntity>();

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
