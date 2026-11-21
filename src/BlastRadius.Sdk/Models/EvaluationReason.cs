namespace BlastRadius.Sdk.Models;

/// <summary>
/// Explains the exact decision path chosen during feature flag evaluation.
/// Crucial for debugging, live audit trails, and client-side decision introspection.
/// </summary>
public enum EvaluationReason
{
    /// <summary>
    /// The flag was explicitly turned off or automatically killed by the blast-radius circuit breaker.
    /// </summary>
    Disabled = 1,

    /// <summary>
    /// An explicit audience targeting rule matched (e.g., internal tester, beta tier, or region whitelist).
    /// </summary>
    TargetingRuleMatch = 2,

    /// <summary>
    /// The user's MurmurHash3 deterministic bucket index fell below the configured canary rollout threshold.
    /// </summary>
    PercentageRolloutMatch = 3,

    /// <summary>
    /// The user's MurmurHash3 deterministic bucket index was greater than or equal to the rollout threshold.
    /// </summary>
    PercentageRolloutMiss = 4,

    /// <summary>
    /// The flag configuration was not present in the local store, so the caller-supplied default value was returned.
    /// </summary>
    DefaultFallback = 5,

    /// <summary>
    /// An unexpected exception occurred during rule evaluation; the engine safely caught it and returned the fallback value.
    /// </summary>
    ErrorFallback = 6
}
