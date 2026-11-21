using BlastRadius.Sdk.Client;
using BlastRadius.Sdk.Configuration;
using BlastRadius.Sdk.Evaluation;
using BlastRadius.Sdk.Models;
using BlastRadius.Sdk.Store;
using BlastRadius.Sdk.Telemetry;
using Microsoft.Extensions.Options;

namespace BlastRadius.Sdk.Tests;

public class BlastRadiusClientTests
{
    private readonly InMemoryFlagStore _store;
    private readonly RuleEvaluator _evaluator;
    private readonly RedisTelemetryReporter _telemetry;
    private readonly BlastRadiusClient _client;

    public BlastRadiusClientTests()
    {
        var options = Options.Create(new BlastRadiusOptions
        {
            Environment = "test",
            EnableTelemetry = false // Telemetry background worker disabled for deterministic client test
        });

        _store = new InMemoryFlagStore(options);
        _evaluator = new RuleEvaluator();
        _telemetry = new RedisTelemetryReporter(options);
        _client = new BlastRadiusClient(_store, _evaluator, _telemetry);
    }

    [Fact]
    public void IsEnabled_ReturnsDefaultValue_WhenFlagDoesNotExist()
    {
        bool result = _client.IsEnabled("non-existent-flag", EvaluationContext.ForUser("user_1"), defaultValue: false);
        Assert.False(result);

        bool resultWithTrueDefault = _client.IsEnabled("non-existent-flag", EvaluationContext.ForUser("user_1"), defaultValue: true);
        Assert.True(resultWithTrueDefault);
    }

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenTargetingRuleMatches()
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "beta-dashboard",
            IsEnabled = true,
            RolloutPercentage = 0,
            Version = 1,
            Rules = new List<TargetingRule>
            {
                new()
                {
                    Id = "rule-beta-users",
                    Priority = 1,
                    AttributeName = "role",
                    Operator = RuleOperator.Equals,
                    Values = new[] { "BetaTester" },
                    ServeValue = true
                }
            }
        };

        _store.UpdateOrAddFlag(flag);

        var betaUser = new EvaluationContext("user_beta").SetAttribute("role", "BetaTester");
        var regularUser = new EvaluationContext("user_regular").SetAttribute("role", "Standard");

        Assert.True(_client.IsEnabled("beta-dashboard", betaUser));
        Assert.False(_client.IsEnabled("beta-dashboard", regularUser));
    }

    [Fact]
    public void Evaluate_ReturnsSubMillisecondExecutionTime()
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "fast-feature",
            IsEnabled = true,
            RolloutPercentage = 50,
            Version = 1
        };

        _store.UpdateOrAddFlag(flag);

        var context = EvaluationContext.ForUser("perf_user_1");

        // Warm up JIT
        _client.Evaluate("fast-feature", context);

        var result = _client.Evaluate("fast-feature", context);

        // Verification of sub-millisecond evaluation (< 1000 microseconds)
        Assert.True(result.ExecutionTimeMicroseconds < 1000,
            $"Expected sub-millisecond evaluation, but was {result.ExecutionTimeMicroseconds} us");
    }
}
