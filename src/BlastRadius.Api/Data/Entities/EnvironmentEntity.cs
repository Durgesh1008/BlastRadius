using System.ComponentModel.DataAnnotations;

namespace BlastRadius.Api.Data.Entities;

/// <summary>
/// Represents an isolated deployment environment (e.g., 'production', 'staging', 'development').
/// Feature flag states and telemetry sliding windows are partitioned by environment.
/// </summary>
public sealed class EnvironmentEntity
{
    [Key]
    public string Id { get; set; } = string.Empty; // e.g. "production", "staging"

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<FeatureFlagEntity> FeatureFlags { get; set; } = new List<FeatureFlagEntity>();
}
