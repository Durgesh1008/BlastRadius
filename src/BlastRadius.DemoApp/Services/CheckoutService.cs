using System.Diagnostics;
using BlastRadius.DemoApp.Models;
using BlastRadius.Sdk.Client;
using BlastRadius.Sdk.Models;

namespace BlastRadius.DemoApp.Services;

public interface ICheckoutService
{
    Task<CheckoutResponse> ProcessCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
    Task<BatchSimulationResponse> RunBatchSimulationAsync(BatchSimulationRequest request, CancellationToken cancellationToken = default);
}

public sealed class CheckoutService : ICheckoutService
{
    private const string DefaultTargetFlagKey = "new-upi-checkout";

    private readonly IBlastRadiusClient _blastRadius;
    private readonly IChaosManager _chaosManager;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        IBlastRadiusClient blastRadius,
        IChaosManager chaosManager,
        ILogger<CheckoutService> logger)
    {
        _blastRadius = blastRadius;
        _chaosManager = chaosManager;
        _logger = logger;
    }

    public async Task<CheckoutResponse> ProcessCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        long startTimestamp = Stopwatch.GetTimestamp();
        string flagKey = string.IsNullOrWhiteSpace(request.FlagKey) ? DefaultTargetFlagKey : request.FlagKey;

        // 1. Build evaluation context from customer attributes
        var context = EvaluationContext.ForUser(request.UserId)
            .SetAttribute("userTier", request.UserTier)
            .SetAttribute("city", request.City);

        // 2. Sub-microsecond local in-memory evaluation via BlastRadius SDK
        var evalResult = _blastRadius.Evaluate(flagKey, context, defaultValue: false);

        var chaos = _chaosManager.CurrentConfig;

        // Simulate real-world network transport latency
        if (chaos.SimulateLatencyMs > 0)
        {
            await Task.Delay(chaos.SimulateLatencyMs, cancellationToken);
        }

        CheckoutResponse response;

        if (evalResult.Value)
        {
            // === NEW FAST-TRACK UPI GATEWAY (Canary Feature) ===
            bool shouldInjectFailure = chaos.IsFaultInjectionActive &&
                                       (Random.Shared.NextDouble() * 100.0 < chaos.FailureRatePercent);

            if (shouldInjectFailure)
            {
                // CRITICAL TELEMETRY: Feed error count into the sliding window
                _blastRadius.RecordTelemetry(flagKey, isError: true);

                long elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).Milliseconds;
                response = new CheckoutResponse
                {
                    OrderId = request.OrderId,
                    UserId = request.UserId,
                    Success = false,
                    GatewayUsed = "NewInstantUPI",
                    Amount = request.Amount,
                    LatencyMs = elapsedMs,
                    MurmurHashBucket = evalResult.Bucket,
                    EvaluationReason = evalResult.Reason.ToString(),
                    FeatureFlagVersion = evalResult.Version,
                    Message = "Payment Failed: 504 Gateway Timeout on Partner Bank UPI Switch",
                    ErrorDetails = "Simulated upstream partner timeout recorded into BlastRadius telemetry."
                };

                _logger.LogWarning("Checkout {OrderId} [UPI FAIL] for user '{UserId}' (Bucket {Bucket}) on flag '{FlagKey}' - Error recorded in telemetry.",
                    request.OrderId, request.UserId, evalResult.Bucket, flagKey);
            }
            else
            {
                // Success telemetry
                _blastRadius.RecordTelemetry(flagKey, isError: false);

                long elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).Milliseconds;
                response = new CheckoutResponse
                {
                    OrderId = request.OrderId,
                    UserId = request.UserId,
                    Success = true,
                    GatewayUsed = "NewInstantUPI",
                    Amount = request.Amount,
                    LatencyMs = elapsedMs,
                    MurmurHashBucket = evalResult.Bucket,
                    EvaluationReason = evalResult.Reason.ToString(),
                    FeatureFlagVersion = evalResult.Version,
                    Message = "Payment Captured Successfully via 1-Click Instant UPI (Fast Track)"
                };

                _logger.LogInformation("Checkout {OrderId} [UPI SUCCESS] for user '{UserId}' (Bucket {Bucket}) on flag '{FlagKey}'.",
                    request.OrderId, request.UserId, evalResult.Bucket, flagKey);
            }
        }
        else
        {
            // === STABLE LEGACY CARD GATEWAY (Safe Fallback) ===
            long elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).Milliseconds;
            response = new CheckoutResponse
            {
                OrderId = request.OrderId,
                UserId = request.UserId,
                Success = true,
                GatewayUsed = "LegacyCard",
                Amount = request.Amount,
                LatencyMs = elapsedMs,
                MurmurHashBucket = evalResult.Bucket,
                EvaluationReason = evalResult.Reason.ToString(),
                FeatureFlagVersion = evalResult.Version,
                Message = "Payment Captured Successfully via Standard Card Gateway (Stable Path)"
            };

            _logger.LogDebug("Checkout {OrderId} [LEGACY CARD] for user '{UserId}' (Reason: {Reason}) on flag '{FlagKey}'.",
                request.OrderId, request.UserId, evalResult.Reason, flagKey);
        }

        _chaosManager.RecordTransaction(response);
        return response;
    }

    public async Task<BatchSimulationResponse> RunBatchSimulationAsync(BatchSimulationRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        int total = Math.Clamp(request.TotalOrders, 5, 500);
        string flagKey = string.IsNullOrWhiteSpace(request.FlagKey) ? DefaultTargetFlagKey : request.FlagKey;

        var responses = new List<CheckoutResponse>();
        var semaphore = new SemaphoreSlim(Math.Clamp(request.Concurrency, 1, 20));

        var cities = new[] { "Bangalore", "Mumbai", "Delhi", "Hyderabad", "Chennai" };
        var tiers = new[] { "Standard", "Standard", "Standard", "VIP", "BetaTester" };

        var tasks = Enumerable.Range(1, total).Select(async i =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var order = new CheckoutRequest
                {
                    UserId = $"cust_{Random.Shared.Next(1, 2500):D4}",
                    OrderId = $"ORD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                    Amount = Math.Round((decimal)(Random.Shared.Next(50, 2500) + Random.Shared.NextDouble()), 2),
                    City = cities[Random.Shared.Next(cities.Length)],
                    UserTier = tiers[Random.Shared.Next(tiers.Length)],
                    FlagKey = flagKey
                };

                return await ProcessCheckoutAsync(order, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        responses.AddRange(results);

        int successes = responses.Count(r => r.Success);
        int failures = responses.Count(r => !r.Success);
        int upiCount = responses.Count(r => r.GatewayUsed == "NewInstantUPI");
        int legacyCount = responses.Count(r => r.GatewayUsed == "LegacyCard");
        double actualErrorRate = total > 0 ? Math.Round(((double)failures / total) * 100.0, 1) : 0.0;

        // Check if circuit breaker tripped during this batch
        var activeFlag = _blastRadius.GetAllFlags().FirstOrDefault(f => f.FlagKey == flagKey);
        bool breakerTripped = activeFlag != null && !activeFlag.IsEnabled && !string.IsNullOrWhiteSpace(activeFlag.KillReason);

        return new BatchSimulationResponse
        {
            TotalOrders = total,
            SuccessOrders = successes,
            FailedOrders = failures,
            NewUpiOrders = upiCount,
            LegacyOrders = legacyCount,
            ActualErrorRatePercent = actualErrorRate,
            DurationMs = sw.ElapsedMilliseconds,
            BreakerTrippedDuringRun = breakerTripped,
            SampleTransactions = responses.Take(25).ToList()
        };
    }
}
