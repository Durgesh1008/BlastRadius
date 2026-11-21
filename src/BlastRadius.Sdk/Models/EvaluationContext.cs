namespace BlastRadius.Sdk.Models;

/// <summary>
/// Contextual information provided by the host application per evaluation request.
/// Contains the user subject identifier and arbitrary key-value attributes for targeting rules.
/// </summary>
public sealed class EvaluationContext
{
    private readonly Dictionary<string, string> _attributes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Unique identifier for the subject (e.g., userId, accountId, deviceId).
    /// Used as the hash seed for deterministic MurmurHash3 percentage rollouts.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Optional IP address of the client (useful for geo-targeting or fraud containment).
    /// </summary>
    public string? ClientIp { get; init; }

    /// <summary>
    /// Target environment (e.g., "production", "staging").
    /// </summary>
    public string Environment { get; init; } = "production";

    /// <summary>
    /// Read-only view of context attributes (e.g., plan, email, country, role, version).
    /// Case-insensitive dictionary lookups.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes => _attributes;

    /// <summary>
    /// Initializes an empty evaluation context.
    /// </summary>
    public EvaluationContext()
    {
    }

    /// <summary>
    /// Initializes an evaluation context with a specific user identifier.
    /// </summary>
    public EvaluationContext(string? userId)
    {
        UserId = userId;
    }

    /// <summary>
    /// Initializes an evaluation context with a user identifier and pre-populated attributes.
    /// </summary>
    public EvaluationContext(string? userId, IDictionary<string, string>? attributes)
    {
        UserId = userId;
        if (attributes != null)
        {
            foreach (var kvp in attributes)
            {
                _attributes[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Fluent helper to attach an attribute to this context.
    /// </summary>
    public EvaluationContext SetAttribute(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _attributes[key] = value ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Fluent builder helper to create a context for a specific user.
    /// </summary>
    public static EvaluationContext ForUser(string userId) => new(userId);

    /// <summary>
    /// Creates an anonymous evaluation context with no user identifier.
    /// </summary>
    public static EvaluationContext Anonymous() => new(null);
}
