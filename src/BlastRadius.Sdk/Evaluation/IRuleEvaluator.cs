using BlastRadius.Sdk.Models;

namespace BlastRadius.Sdk.Evaluation;

/// <summary>
/// Defines the contract for evaluating feature flags against incoming execution contexts.
/// Implementations must be thread-safe, lock-free, and guarantee zero exception propagation.
/// </summary>
public interface IRuleEvaluator
{
    /// <summary>
    /// Evaluates a feature flag configuration against an evaluation context.
    /// </summary>
    /// <param name="flag">The feature flag configuration to evaluate.</param>
    /// <param name="context">The contextual attributes and user identifier.</param>
    /// <param name="defaultValue">Fallback value if the flag is disabled or encounters an evaluation anomaly.</param>
    /// <returns>A comprehensive <see cref="EvaluationResult"/> containing the decision and telemetry metadata.</returns>
    EvaluationResult Evaluate(FeatureFlagConfig? flag, EvaluationContext? context, bool defaultValue = false);
}
