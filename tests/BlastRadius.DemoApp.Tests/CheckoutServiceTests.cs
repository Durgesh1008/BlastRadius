using BlastRadius.DemoApp.Models;
using BlastRadius.DemoApp.Services;
using BlastRadius.Sdk.Client;
using BlastRadius.Sdk.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlastRadius.DemoApp.Tests;

public class CheckoutServiceTests
{
    private sealed class FakeBlastRadiusClient : IBlastRadiusClient
    {
        public bool EnabledResult { get; set; } = false;
        public int BucketResult { get; set; } = 7;
        public List<(string flagKey, bool isError)> RecordedTelemetry { get; } = new();

        public bool IsEnabled(string flagKey, EvaluationContext? context = null, bool defaultValue = false)
        {
            return EnabledResult;
        }

        public EvaluationResult Evaluate(string flagKey, EvaluationContext? context = null, bool defaultValue = false)
        {
            return EnabledResult
                ? EvaluationResult.PercentageRolloutMatch(flagKey, BucketResult, 1, 10)
                : EvaluationResult.Disabled(flagKey, 1, 10);
        }

        public void RecordTelemetry(string flagKey, bool isError)
        {
            RecordedTelemetry.Add((flagKey, isError));
        }

        public IReadOnlyCollection<FeatureFlagConfig> GetAllFlags() => Array.Empty<FeatureFlagConfig>();
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }

    [Fact]
    public async Task ProcessCheckout_WhenFlagDisabled_ServesLegacyCardWith100PercentSuccess()
    {
        var client = new FakeBlastRadiusClient { EnabledResult = false };
        var chaosManager = new ChaosManager();
        var service = new CheckoutService(client, chaosManager, NullLogger<CheckoutService>.Instance);

        var request = new CheckoutRequest
        {
            UserId = "user_stable",
            Amount = 199.99m
        };

        var response = await service.ProcessCheckoutAsync(request);

        Assert.True(response.Success);
        Assert.Equal("LegacyCard", response.GatewayUsed);
        Assert.Contains("Standard Card Gateway", response.Message);
        Assert.Empty(client.RecordedTelemetry); // Legacy gateway doesn't feed the new feature's breaker
    }

    [Fact]
    public async Task ProcessCheckout_WhenFlagEnabledAndZeroChaos_ServesUPIWithSuccess()
    {
        var client = new FakeBlastRadiusClient { EnabledResult = true, BucketResult = 5 };
        var chaosManager = new ChaosManager();
        chaosManager.UpdateConfig(new ChaosConfig { FailureRatePercent = 0.0, SimulateLatencyMs = 0 });

        var service = new CheckoutService(client, chaosManager, NullLogger<CheckoutService>.Instance);

        var request = new CheckoutRequest
        {
            UserId = "user_canary_vip",
            Amount = 499.00m
        };

        var response = await service.ProcessCheckoutAsync(request);

        Assert.True(response.Success);
        Assert.Equal("NewInstantUPI", response.GatewayUsed);
        Assert.Contains("Instant UPI", response.Message);
        
        // Success telemetry recorded
        Assert.Single(client.RecordedTelemetry);
        Assert.False(client.RecordedTelemetry[0].isError);
    }

    [Fact]
    public async Task ProcessCheckout_WhenFlagEnabledAnd100PercentChaos_FailsAndReportsTelemetry()
    {
        var client = new FakeBlastRadiusClient { EnabledResult = true, BucketResult = 5 };
        var chaosManager = new ChaosManager();
        chaosManager.UpdateConfig(new ChaosConfig { FailureRatePercent = 100.0, SimulateLatencyMs = 0 });

        var service = new CheckoutService(client, chaosManager, NullLogger<CheckoutService>.Instance);

        var request = new CheckoutRequest
        {
            UserId = "user_canary_fail",
            Amount = 799.00m
        };

        var response = await service.ProcessCheckoutAsync(request);

        Assert.False(response.Success);
        Assert.Equal("NewInstantUPI", response.GatewayUsed);
        Assert.Contains("504 Gateway Timeout", response.Message);

        // Error telemetry recorded
        Assert.Single(client.RecordedTelemetry);
        Assert.True(client.RecordedTelemetry[0].isError);
    }

    [Fact]
    public async Task RunBatchSimulation_ProcessesBatchOrdersCorrectly()
    {
        var client = new FakeBlastRadiusClient { EnabledResult = true, BucketResult = 12 };
        var chaosManager = new ChaosManager();
        chaosManager.UpdateConfig(new ChaosConfig { FailureRatePercent = 20.0, SimulateLatencyMs = 0 });

        var service = new CheckoutService(client, chaosManager, NullLogger<CheckoutService>.Instance);

        var response = await service.RunBatchSimulationAsync(new BatchSimulationRequest
        {
            TotalOrders = 20,
            Concurrency = 4
        });

        Assert.Equal(20, response.TotalOrders);
        Assert.Equal(20, response.SuccessOrders + response.FailedOrders);
        Assert.Equal(20, response.NewUpiOrders);
    }

    [Fact]
    public void ChaosManager_MaintainsAccurateCountsAndTransactionHistory()
    {
        var manager = new ChaosManager();

        manager.RecordTransaction(new CheckoutResponse { Success = true, GatewayUsed = "NewInstantUPI" });
        manager.RecordTransaction(new CheckoutResponse { Success = false, GatewayUsed = "NewInstantUPI" });
        manager.RecordTransaction(new CheckoutResponse { Success = true, GatewayUsed = "LegacyCard" });

        var stats = manager.GetStats(isFlagEnabled: true, rolloutPercentage: 25, flagVersion: 3, killReason: null);

        Assert.Equal(3, stats.TotalTransactions);
        Assert.Equal(2, stats.SuccessfulTransactions);
        Assert.Equal(1, stats.FailedTransactions);
        Assert.Equal(2, stats.NewUpiCount);
        Assert.Equal(1, stats.LegacyCardCount);
        Assert.Equal(33.3, stats.ErrorRatePercent);
    }
}
