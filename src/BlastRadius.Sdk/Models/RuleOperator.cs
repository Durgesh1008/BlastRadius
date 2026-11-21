namespace BlastRadius.Sdk.Models;

/// <summary>
/// Defines the set of comparison operators supported when evaluating feature flag targeting rules.
/// </summary>
public enum RuleOperator
{
    /// <summary>
    /// Exact case-insensitive string equality match.
    /// </summary>
    Equals = 1,

    /// <summary>
    /// Exact case-insensitive string inequality match.
    /// </summary>
    NotEquals = 2,

    /// <summary>
    /// Checks whether the user's attribute value exists in a set of allowed values.
    /// Example: context.Attributes["plan"] IN ["Enterprise", "BetaTester"]
    /// </summary>
    In = 3,

    /// <summary>
    /// Checks whether the user's attribute value does NOT exist in a set of values.
    /// </summary>
    NotIn = 4,

    /// <summary>
    /// Case-insensitive substring containment.
    /// </summary>
    Contains = 5,

    /// <summary>
    /// Checks whether the attribute string starts with the specified prefix.
    /// </summary>
    StartsWith = 6,

    /// <summary>
    /// Checks whether the attribute string ends with the specified suffix.
    /// Example: context.Attributes["email"] EndsWith "@mycompany.com"
    /// </summary>
    EndsWith = 7,

    /// <summary>
    /// Performs numeric or version greater-than comparison.
    /// </summary>
    GreaterThan = 8,

    /// <summary>
    /// Performs numeric or version less-than comparison.
    /// </summary>
    LessThan = 9
}
