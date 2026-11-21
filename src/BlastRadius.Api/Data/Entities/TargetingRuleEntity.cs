using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BlastRadius.Sdk.Models;

namespace BlastRadius.Api.Data.Entities;

/// <summary>
/// Database persistence entity for an audience targeting rule.
/// Evaluated in strict ascending priority order.
/// </summary>
public sealed class TargetingRuleEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid FeatureFlagId { get; set; }

    [ForeignKey(nameof(FeatureFlagId))]
    public FeatureFlagEntity? FeatureFlag { get; set; }

    /// <summary>
    /// Priority order (1, 2, 3...). Lower numbers evaluated first.
    /// </summary>
    public int Priority { get; set; } = 1;

    [Required]
    [MaxLength(100)]
    public string AttributeName { get; set; } = string.Empty;

    public RuleOperator Operator { get; set; } = RuleOperator.Equals;

    /// <summary>
    /// Serialized JSON string containing list of values (e.g. ["Bangalore", "Mumbai"]).
    /// </summary>
    [Required]
    public string ValuesJson { get; set; } = "[]";

    public bool ServeValue { get; set; } = true;

    [MaxLength(500)]
    public string? Description { get; set; }
}
