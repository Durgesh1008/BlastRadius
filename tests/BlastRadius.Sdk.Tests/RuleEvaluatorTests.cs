using BlastRadius.Sdk.Evaluation;
using BlastRadius.Sdk.Models;

namespace BlastRadius.Sdk.Tests;

public class RuleEvaluatorTests
{
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_ReturnsDisabled_WhenFlagIsEnabledIsFalse()
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "test-flag",
            IsEnabled = false,
            RolloutPercentage = 100
        };

        var result = _evaluator.Evaluate(flag, EvaluationContext.ForUser("user_1"));

        Assert.False(result.Value);
        Assert.Equal(EvaluationReason.Disabled, result.Reason);
    }

    [Fact]
    public void Evaluate_ReturnsDefaultFallback_WhenFlagIsNull()
    {
        var result = _evaluator.Evaluate(null, EvaluationContext.ForUser("user_1"), defaultValue: true);

        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.DefaultFallback, result.Reason);
    }

    [Fact]
    public void Evaluate_EvaluatesTargetingRulesInAscendingPriorityOrder()
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "tiered-feature",
            IsEnabled = true,
            RolloutPercentage = 0,
            Rules = new List<TargetingRule>
            {
                new()
                {
                    Id = "rule-p2-block",
                    Priority = 2,
                    AttributeName = "plan",
                    Operator = RuleOperator.Equals,
                    Values = new[] { "Enterprise" },
                    ServeValue = false
                },
                new()
                {
                    Id = "rule-p1-allow",
                    Priority = 1,
                    AttributeName = "plan",
                    Operator = RuleOperator.Equals,
                    Values = new[] { "Enterprise" },
                    ServeValue = true
                }
            }
        };

        var context = new EvaluationContext("user_1")
            .SetAttribute("plan", "Enterprise");

        var result = _evaluator.Evaluate(flag, context);

        // Priority 1 rule must win
        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.TargetingRuleMatch, result.Reason);
        Assert.Equal("rule-p1-allow", result.MatchedRuleId);
    }

    [Theory]
    [InlineData("alice@acme.com", "@acme.com", true)]
    [InlineData("bob@gmail.com", "@acme.com", false)]
    public void Evaluate_TargetingRule_EndsWithOperator(string email, string suffix, bool expectedMatch)
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "internal-feature",
            IsEnabled = true,
            RolloutPercentage = 0,
            Rules = new List<TargetingRule>
            {
                new()
                {
                    Id = "rule-internal-domain",
                    Priority = 1,
                    AttributeName = "email",
                    Operator = RuleOperator.EndsWith,
                    Values = new[] { suffix },
                    ServeValue = true
                }
            }
        };

        var context = new EvaluationContext("user_x").SetAttribute("email", email);
        var result = _evaluator.Evaluate(flag, context);

        Assert.Equal(expectedMatch, result.Value);
    }

    [Theory]
    [InlineData("Bangalore", true)]
    [InlineData("Mumbai", true)]
    [InlineData("Delhi", false)]
    public void Evaluate_TargetingRule_InOperator(string city, bool expectedMatch)
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "metro-pilot",
            IsEnabled = true,
            RolloutPercentage = 0,
            Rules = new List<TargetingRule>
            {
                new()
                {
                    Id = "rule-cities",
                    Priority = 1,
                    AttributeName = "city",
                    Operator = RuleOperator.In,
                    Values = new[] { "Bangalore", "Mumbai" },
                    ServeValue = true
                }
            }
        };

        var context = new EvaluationContext("user_x").SetAttribute("city", city);
        var result = _evaluator.Evaluate(flag, context);

        Assert.Equal(expectedMatch, result.Value);
    }

    [Fact]
    public void Evaluate_Rollout100_AlwaysMatches()
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "full-rollout",
            IsEnabled = true,
            RolloutPercentage = 100
        };

        var result = _evaluator.Evaluate(flag, EvaluationContext.ForUser("random_user_99"));

        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.PercentageRolloutMatch, result.Reason);
    }

    [Fact]
    public void Evaluate_Rollout0_AlwaysMisses()
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "zero-rollout",
            IsEnabled = true,
            RolloutPercentage = 0
        };

        var result = _evaluator.Evaluate(flag, EvaluationContext.ForUser("random_user_99"));

        Assert.False(result.Value);
        Assert.Equal(EvaluationReason.PercentageRolloutMiss, result.Reason);
    }

    [Fact]
    public void Evaluate_GuaranteesZeroUnhandledExceptions()
    {
        // Malformed flag with null inner fields should still return fallback without throwing
        var flag = new FeatureFlagConfig
        {
            FlagKey = "malformed",
            IsEnabled = true,
            RolloutPercentage = 50,
            Rules = new List<TargetingRule>
            {
                new()
                {
                    Id = "corrupt-rule",
                    AttributeName = "score",
                    Operator = RuleOperator.GreaterThan,
                    Values = new[] { "invalid-number" }
                }
            }
        };

        var context = new EvaluationContext("user_1").SetAttribute("score", "not-a-number");

        // Evaluator should not throw
        var result = _evaluator.Evaluate(flag, context, defaultValue: false);
        Assert.NotNull(result);
    }
}
