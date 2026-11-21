using System.Diagnostics;
using System.Globalization;
using BlastRadius.Sdk.Hashing;
using BlastRadius.Sdk.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlastRadius.Sdk.Evaluation;

/// <summary>
/// Production-grade implementation of the feature flag evaluation engine.
/// 
/// EVALUATION PIPELINE:
/// 1. Flag Existence and Master Switch: If null or IsEnabled == false, short-circuits immediately.
/// 2. Targeted Audience Rules: Evaluated strictly in order of ascending priority.
///    Supports whitelist/blacklist logic (Equals, In, EndsWith, Contains, etc.).
///    The first matching rule determines the outcome and short-circuits evaluation.
/// 3. Deterministic Percentage Rollout: If no targeting rule matches, computes MurmurHash3
///    hash of (flagKey + ":" + userId) modulo 100.
/// 4. Strict Fail-Safe Wrapper: If any unexpected runtime error occurs, catches all exceptions,
///    logs an alert, and returns the caller's safe fallback value. Never crashes the host application.
/// </summary>
public sealed class RuleEvaluator : IRuleEvaluator
{
    private readonly ILogger<RuleEvaluator> _logger;

    public RuleEvaluator(ILogger<RuleEvaluator>? logger = null)
    {
        _logger = logger ?? NullLogger<RuleEvaluator>.Instance;
    }

    /// <inheritdoc />
    public EvaluationResult Evaluate(FeatureFlagConfig? flag, EvaluationContext? context, bool defaultValue = false)
    {
        long startTimestamp = Stopwatch.GetTimestamp();

        if (flag == null)
        {
            long elapsedUs = GetElapsedMicroseconds(startTimestamp);
            return EvaluationResult.DefaultFallback("unknown", defaultValue, EvaluationReason.DefaultFallback, elapsedUs);
        }

        try
        {
            // Step 1: Master Toggle Check
            // If the flag was disabled manually or tripped by the 50ms blast-radius circuit breaker,
            // short-circuit immediately to avoid wasting CPU cycles on rule evaluation.
            if (!flag.IsEnabled)
            {
                long elapsedUs = GetElapsedMicroseconds(startTimestamp);
                return EvaluationResult.Disabled(flag.FlagKey, flag.Version, elapsedUs);
            }

            // Step 2: Context Normalization
            context ??= EvaluationContext.Anonymous();

            // Step 3: Priority-Ordered Targeting Rules
            if (flag.Rules is { Count: > 0 })
            {
                // Rules must be evaluated in ascending priority order (e.g. 1 before 2)
                var sortedRules = flag.Rules.OrderBy(r => r.Priority);

                foreach (var rule in sortedRules)
                {
                    if (EvaluateRule(rule, context))
                    {
                        long elapsedUs = GetElapsedMicroseconds(startTimestamp);
                        return EvaluationResult.TargetingRuleMatch(
                            flag.FlagKey,
                            rule.Id,
                            rule.ServeValue,
                            flag.Version,
                            elapsedUs);
                    }
                }
            }

            // Step 4: Canary Percentage Rollout
            // If rollout is 0%, no general traffic qualifies
            if (flag.RolloutPercentage <= 0)
            {
                long elapsedUs = GetElapsedMicroseconds(startTimestamp);
                return EvaluationResult.PercentageRolloutMiss(flag.FlagKey, 0, flag.Version, elapsedUs);
            }

            // If rollout is 100%, all traffic qualifies
            if (flag.RolloutPercentage >= 100)
            {
                long elapsedUs = GetElapsedMicroseconds(startTimestamp);
                return EvaluationResult.PercentageRolloutMatch(flag.FlagKey, 0, flag.Version, elapsedUs);
            }

            // If subject has no userId, we cannot deterministically bucket them
            if (string.IsNullOrWhiteSpace(context.UserId))
            {
                long elapsedUs = GetElapsedMicroseconds(startTimestamp);
                // Return default fallback when userId is missing for a percentage-based flag
                return EvaluationResult.DefaultFallback(flag.FlagKey, defaultValue, EvaluationReason.DefaultFallback, elapsedUs);
            }

            // Step 5: MurmurHash3 Consistent Hash Bucketing
            int bucket = MurmurHash3.ComputeBucket(flag.FlagKey, context.UserId);

            long elapsedTotalUs = GetElapsedMicroseconds(startTimestamp);

            if (bucket < flag.RolloutPercentage)
            {
                return EvaluationResult.PercentageRolloutMatch(flag.FlagKey, bucket, flag.Version, elapsedTotalUs);
            }

            return EvaluationResult.PercentageRolloutMiss(flag.FlagKey, bucket, flag.Version, elapsedTotalUs);
        }
        catch (Exception ex)
        {
            // STRICT FAIL-SAFE:
            // Under zero circumstances may a feature flag evaluation throw an unhandled exception
            // to caller code (e.g., in a checkout or payment flow).
            long elapsedUs = GetElapsedMicroseconds(startTimestamp);
            _logger.LogError(ex, "Unexpected exception evaluating feature flag '{FlagKey}'. Serving safe fallback.", flag.FlagKey);
            return EvaluationResult.ErrorFallback(flag.FlagKey, defaultValue, elapsedUs);
        }
    }

    /// <summary>
    /// Evaluates whether an individual targeting rule matches the incoming context.
    /// </summary>
    private static bool EvaluateRule(TargetingRule rule, EvaluationContext context)
    {
        string? attributeValue = ResolveAttributeValue(rule.AttributeName, context);
        if (attributeValue == null)
        {
            return false;
        }

        return rule.Operator switch
        {
            RuleOperator.Equals => rule.Values.Any(v => string.Equals(attributeValue, v, StringComparison.OrdinalIgnoreCase)),
            
            RuleOperator.NotEquals => rule.Values.All(v => !string.Equals(attributeValue, v, StringComparison.OrdinalIgnoreCase)),
            
            RuleOperator.In => rule.Values.Any(v => string.Equals(attributeValue, v, StringComparison.OrdinalIgnoreCase)),
            
            RuleOperator.NotIn => rule.Values.All(v => !string.Equals(attributeValue, v, StringComparison.OrdinalIgnoreCase)),
            
            RuleOperator.Contains => rule.Values.Any(v => attributeValue.Contains(v, StringComparison.OrdinalIgnoreCase)),
            
            RuleOperator.StartsWith => rule.Values.Any(v => attributeValue.StartsWith(v, StringComparison.OrdinalIgnoreCase)),
            
            RuleOperator.EndsWith => rule.Values.Any(v => attributeValue.EndsWith(v, StringComparison.OrdinalIgnoreCase)),
            
            RuleOperator.GreaterThan => CompareNumericOrLexical(attributeValue, rule.Values.FirstOrDefault()) > 0,
            
            RuleOperator.LessThan => CompareNumericOrLexical(attributeValue, rule.Values.FirstOrDefault()) < 0,
            
            _ => false
        };
    }

    /// <summary>
    /// Resolves the attribute string value from the context, prioritizing special fields like userId.
    /// </summary>
    private static string? ResolveAttributeValue(string attributeName, EvaluationContext context)
    {
        if (string.Equals(attributeName, "userId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(attributeName, "id", StringComparison.OrdinalIgnoreCase))
        {
            return context.UserId ?? (context.Attributes.TryGetValue(attributeName, out var v) ? v : null);
        }

        if (string.Equals(attributeName, "clientIp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(attributeName, "ip", StringComparison.OrdinalIgnoreCase))
        {
            return context.ClientIp ?? (context.Attributes.TryGetValue(attributeName, out var v) ? v : null);
        }

        if (string.Equals(attributeName, "environment", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(attributeName, "env", StringComparison.OrdinalIgnoreCase))
        {
            return context.Environment;
        }

        return context.Attributes.TryGetValue(attributeName, out var val) ? val : null;
    }

    /// <summary>
    /// Compares two values numerically if both parse as double; otherwise performs ordinal comparison.
    /// </summary>
    private static int CompareNumericOrLexical(string? a, string? b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        if (double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out double numA) &&
            double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out double numB))
        {
            return numA.CompareTo(numB);
        }

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static long GetElapsedMicroseconds(long startTimestamp)
    {
        long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
        return (elapsedTicks * 1_000_000) / Stopwatch.Frequency;
    }
}
