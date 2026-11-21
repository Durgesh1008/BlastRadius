namespace BlastRadius.Sdk.Models;

/// <summary>
/// Represents a targeted audience rule that takes precedence over percentage canary rollouts.
/// Rules are evaluated in strict ascending order of <see cref="Priority"/>.
/// </summary>
public sealed class TargetingRule
{
    /// <summary>
    /// Unique identifier for the targeting rule (e.g., "rule-qa-testers", "rule-bangalore-beta").
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Evaluation precedence order. Rules with lower numerical values (e.g., 1) are evaluated before higher numbers (e.g., 10).
    /// </summary>
    public int Priority { get; init; } = 1;

    /// <summary>
    /// The context attribute key to inspect (e.g., "userId", "email", "plan", "country", "device").
    /// </summary>
    public string AttributeName { get; init; } = string.Empty;

    /// <summary>
    /// The comparison operator to execute against the target attribute.
    /// </summary>
    public RuleOperator Operator { get; init; } = RuleOperator.Equals;

    /// <summary>
    /// Set of target values to match against. 
    /// For <see cref="RuleOperator.Equals"/> or <see cref="RuleOperator.EndsWith"/>, the first value is typically used.
    /// For <see cref="RuleOperator.In"/>, the collection represents the allowed whitelist.
    /// </summary>
    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The boolean value to serve if this targeting rule matches. Defaults to <c>true</c>.
    /// Can be set to <c>false</c> for explicit blocklists or exclusions.
    /// </summary>
    public bool ServeValue { get; init; } = true;

    /// <summary>
    /// Human-readable description of why this rule was created.
    /// </summary>
    public string? Description { get; init; }
}
