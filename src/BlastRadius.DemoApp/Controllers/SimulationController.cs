using BlastRadius.DemoApp.Models;
using BlastRadius.DemoApp.Services;
using BlastRadius.Sdk.Client;
using BlastRadius.Sdk.Hashing;
using BlastRadius.Sdk.Models;
using Microsoft.AspNetCore.Mvc;

namespace BlastRadius.DemoApp.Controllers;

[ApiController]
[Route("api/simulation")]
[Produces("application/json")]
public sealed class SimulationController : ControllerBase
{
    private const string DefaultTargetFlagKey = "new-upi-checkout";

    private readonly ICheckoutService _checkoutService;
    private readonly IChaosManager _chaosManager;
    private readonly IBlastRadiusClient _blastRadius;

    public SimulationController(
        ICheckoutService checkoutService,
        IChaosManager chaosManager,
        IBlastRadiusClient blastRadius)
    {
        _checkoutService = checkoutService;
        _chaosManager = chaosManager;
        _blastRadius = blastRadius;
    }

    /// <summary>
    /// Returns live telemetry health, recent transactions, and current feature flag state for the dashboard.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(LiveStatsResponse), StatusCodes.Status200OK)]
    public IActionResult GetLiveStats([FromQuery] string flagKey = DefaultTargetFlagKey)
    {
        var flag = _blastRadius.GetAllFlags().FirstOrDefault(f => f.FlagKey == flagKey)
                   ?? _blastRadius.GetAllFlags().FirstOrDefault();

        bool isEnabled = flag?.IsEnabled ?? false;
        int rollout = flag?.RolloutPercentage ?? 0;
        long version = flag?.Version ?? 0;
        string? killReason = flag?.KillReason;

        var stats = _chaosManager.GetStats(isEnabled, rollout, version, killReason);
        return Ok(stats);
    }

    /// <summary>
    /// Returns all active feature flags currently loaded into the SDK in-memory cache.
    /// </summary>
    [HttpGet("flags")]
    [ProducesResponseType(typeof(IReadOnlyCollection<FeatureFlagConfig>), StatusCodes.Status200OK)]
    public IActionResult GetLoadedFlags()
    {
        var flags = _blastRadius.GetAllFlags();
        return Ok(flags);
    }

    /// <summary>
    /// Evaluates any feature flag in real-time against arbitrary user attributes,
    /// returning the exact MurmurHash3 mathematical derivation and microsecond latency.
    /// </summary>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(FlagEvaluationResponse), StatusCodes.Status200OK)]
    public IActionResult EvaluateFlag([FromBody] FlagEvaluationRequest request)
    {
        string flagKey = string.IsNullOrWhiteSpace(request.FlagKey) ? DefaultTargetFlagKey : request.FlagKey;
        string userId = string.IsNullOrWhiteSpace(request.UserId) ? "anonymous" : request.UserId;

        var context = EvaluationContext.ForUser(userId);
        if (!string.IsNullOrWhiteSpace(request.UserTier))
        {
            context.SetAttribute("userTier", request.UserTier);
        }
        if (!string.IsNullOrWhiteSpace(request.City))
        {
            context.SetAttribute("city", request.City);
        }
        if (request.CustomAttributes != null)
        {
            foreach (var (k, v) in request.CustomAttributes)
            {
                if (!string.IsNullOrWhiteSpace(k))
                {
                    context.SetAttribute(k, v);
                }
            }
        }

        var result = _blastRadius.Evaluate(flagKey, context, defaultValue: false);
        var flag = _blastRadius.GetAllFlags().FirstOrDefault(f => f.FlagKey == flagKey);

        string composite = $"{flagKey}:{userId}";
        uint rawHash = MurmurHash3.Hash32(composite);

        return Ok(new FlagEvaluationResponse
        {
            FlagKey = flagKey,
            UserId = userId,
            IsEnabled = flag?.IsEnabled ?? false,
            RolloutPercentage = flag?.RolloutPercentage ?? 0,
            Value = result.Value,
            Reason = result.Reason.ToString(),
            MatchedRuleId = result.MatchedRuleId,
            Bucket = result.Bucket,
            Version = result.Version,
            ExecutionTimeMicroseconds = result.ExecutionTimeMicroseconds,
            RawHash = rawHash,
            CompositeKey = composite,
            KillReason = flag?.KillReason
        });
    }

    /// <summary>
    /// Fires a batch of simulated parallel checkouts to test canary rollouts and automated circuit-breaking.
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(BatchSimulationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunBatch(
        [FromBody] BatchSimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OverrideFailureRate.HasValue)
        {
            var config = _chaosManager.CurrentConfig;
            config.FailureRatePercent = Math.Clamp(request.OverrideFailureRate.Value, 0.0, 100.0);
            _chaosManager.UpdateConfig(config);
        }

        var result = await _checkoutService.RunBatchSimulationAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Dynamically adjusts runtime chaos injection parameters (failure rate and simulated network delay).
    /// </summary>
    [HttpPost("chaos")]
    [ProducesResponseType(typeof(ChaosConfig), StatusCodes.Status200OK)]
    public IActionResult UpdateChaos([FromBody] ChaosConfig config)
    {
        _chaosManager.UpdateConfig(config);
        return Ok(_chaosManager.CurrentConfig);
    }

    /// <summary>
    /// Resets runtime transaction counters so new experiments start from a clean baseline.
    /// </summary>
    [HttpPost("reset")]
    public IActionResult ResetTelemetry()
    {
        _chaosManager.ResetStats();
        return Ok(new { message = "Telemetry baseline reset successfully." });
    }
}
