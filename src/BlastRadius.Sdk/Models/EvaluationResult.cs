namespace BlastRadius.Sdk.Models;

/// <summary>
/// Encapsulates the complete decision and diagnostic metadata resulting from a feature flag evaluation.
/// </summary>
public sealed class EvaluationResult
{
    /// <summary>
    /// Key of the evaluated feature flag.
    /// </summary>
    public string FlagKey { get; init; } = string.Empty;

    /// <summary>
    /// The final boolean evaluation result served to the application.
    /// </summary>
    public bool Value { get; init; }

    /// <summary>
    /// Explicit reasoning for how this value was determined.
    /// </summary>
    public EvaluationReason Reason { get; init; }

    /// <summary>
    /// Identifier of the targeting rule that matched, if <see cref="Reason"/> is <see cref="EvaluationReason.TargetingRuleMatch"/>.
    /// </summary>
    public string? MatchedRuleId { get; init; }

    /// <summary>
    /// The computed MurmurHash3 bucket index (0-99) if percentage rollout was evaluated.
    /// </summary>
    public int? Bucket { get; init; }

    /// <summary>
    /// Version of the flag configuration used during this evaluation.
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// Duration of the in-memory evaluation pass in microseconds.
    /// Guaranteed to be well below 500 microseconds (0.5 ms).
    /// </summary>
    public long ExecutionTimeMicroseconds { get; init; }

    public static EvaluationResult Disabled(string flagKey, long version, long elapsedUs) =>
        new()
        {
            FlagKey = flagKey,
            Value = false,
            Reason = EvaluationReason.Disabled,
            Version = version,
            ExecutionTimeMicroseconds = elapsedUs
        };

    public static EvaluationResult TargetingRuleMatch(string flagKey, string ruleId, bool serveValue, long version, long elapsedUs) =>
        new()
        {
            FlagKey = flagKey,
            Value = serveValue,
            Reason = EvaluationReason.TargetingRuleMatch,
            MatchedRuleId = ruleId,
            Version = version,
            ExecutionTimeMicroseconds = elapsedUs
        };

    public static EvaluationResult PercentageRolloutMatch(string flagKey, int bucket, long version, long elapsedUs) =>
        new()
        {
            FlagKey = flagKey,
            Value = true,
            Reason = EvaluationReason.PercentageRolloutMatch,
            Bucket = bucket,
            Version = version,
            ExecutionTimeMicroseconds = elapsedUs
        };

    public static EvaluationResult PercentageRolloutMiss(string flagKey, int bucket, long version, long elapsedUs) =>
        new()
        {
            FlagKey = flagKey,
            Value = false,
            Reason = EvaluationReason.PercentageRolloutMiss,
            Bucket = bucket,
            Version = version,
            ExecutionTimeMicroseconds = elapsedUs
        };

    public static EvaluationResult DefaultFallback(string flagKey, bool fallbackValue, EvaluationReason reason, long elapsedUs) =>
        new()
        {
            FlagKey = flagKey,
            Value = fallbackValue,
            Reason = reason,
            Version = 0,
            ExecutionTimeMicroseconds = elapsedUs
        };

    public static EvaluationResult ErrorFallback(string flagKey, bool fallbackValue, long elapsedUs) =>
        new()
        {
            FlagKey = flagKey,
            Value = fallbackValue,
            Reason = EvaluationReason.ErrorFallback,
            Version = 0,
            ExecutionTimeMicroseconds = elapsedUs
        };
}
