using System.ComponentModel.DataAnnotations;

namespace BlastRadius.Api.DTOs;

public sealed class UpdateFeatureFlagRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public bool IsEnabled { get; init; }

    [Range(0, 100)]
    public int RolloutPercentage { get; init; }

    public bool FallbackValue { get; init; }

    public BlastRadiusPolicyDto? BlastRadiusPolicy { get; init; }

    public List<TargetingRuleDto> TargetingRules { get; init; } = new();

    /// <summary>
    /// Version expected by the caller for optimistic concurrency check.
    /// </summary>
    public long ExpectedVersion { get; init; }
}

public sealed class UpdateRolloutRequest
{
    [Range(0, 100)]
    public int RolloutPercentage { get; init; }

    public long ExpectedVersion { get; init; }
}

public sealed class KillFeatureFlagRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string TriggeredBy { get; init; } = "Operator";
}
