using System.ComponentModel.DataAnnotations;
using BlastRadius.Sdk.Models;

namespace BlastRadius.Api.DTOs;

public sealed class CreateFeatureFlagRequest
{
    [Required]
    [RegularExpression(@"^[a-z0-9\-_]+$", ErrorMessage = "Flag key must be lowercase alphanumeric with hyphens or underscores.")]
    [MaxLength(150)]
    public string Key { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public bool IsEnabled { get; init; } = false;

    [Range(0, 100)]
    public int RolloutPercentage { get; init; } = 0;

    public bool FallbackValue { get; init; } = false;

    [Required]
    public string Environment { get; init; } = "production";

    public BlastRadiusPolicyDto? BlastRadiusPolicy { get; init; }

    public List<TargetingRuleDto> TargetingRules { get; init; } = new();
}

public sealed class BlastRadiusPolicyDto
{
    public double MaxErrorRatePercent { get; init; } = 2.0;
    public int MinRequestsThreshold { get; init; } = 50;
    public int SlidingWindowSeconds { get; init; } = 60;
    public bool AutoKillEnabled { get; init; } = true;
    public int CooldownSeconds { get; init; } = 300;
}

public sealed class TargetingRuleDto
{
    public int Priority { get; init; } = 1;
    public string AttributeName { get; init; } = string.Empty;
    public RuleOperator Operator { get; init; } = RuleOperator.Equals;
    public List<string> Values { get; init; } = new();
    public bool ServeValue { get; init; } = true;
    public string? Description { get; init; }
}
