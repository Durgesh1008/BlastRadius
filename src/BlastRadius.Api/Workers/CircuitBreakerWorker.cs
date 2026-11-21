using BlastRadius.Api.Data;
using BlastRadius.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BlastRadius.Api.Workers;

/// <summary>
/// Autonomous circuit-breaker background worker.
/// Continuously monitors Redis sliding-window error rates and trips the emergency kill switch
/// within milliseconds of an error rate spike, isolating faulty code without redeployment.
/// </summary>
public sealed class CircuitBreakerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CircuitBreakerWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public CircuitBreakerWorker(
        IServiceProvider serviceProvider,
        ILogger<CircuitBreakerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BlastRadius CircuitBreakerWorker started. Polling interval: {Interval}s.", _pollInterval.TotalSeconds);

        // Warm-up delay before starting first evaluation cycle
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateActiveFlagsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CircuitBreakerWorker polling cycle.");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("BlastRadius CircuitBreakerWorker stopped.");
    }

    private async Task EvaluateActiveFlagsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlastRadiusDbContext>();
        var telemetryReader = scope.ServiceProvider.GetRequiredService<ITelemetryReader>();
        var flagService = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();

        // Query all enabled flags with active auto-kill policies
        var activeFlags = await db.FeatureFlags
            .AsNoTracking()
            .Include(f => f.BlastRadiusPolicy)
            .Where(f => f.IsEnabled && f.BlastRadiusPolicy != null && f.BlastRadiusPolicy.AutoKillEnabled)
            .ToListAsync(cancellationToken);

        if (activeFlags.Count == 0)
        {
            return;
        }

        foreach (var flag in activeFlags)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var policy = flag.BlastRadiusPolicy!;
            var (total, errors, errorRatePercent) = await telemetryReader.GetMetricsInSlidingWindowAsync(
                flag.Key,
                flag.EnvironmentId,
                policy.SlidingWindowSeconds);

            // Check if criteria for autonomous tripping are met:
            // 1. Total request count must meet the sample size threshold (guards against 1 error / 1 request = 100%)
            // 2. Error rate percentage must exceed the configured maximum threshold
            if (total >= policy.MinRequestsThreshold && errorRatePercent > policy.MaxErrorRatePercent)
            {
                string killReason = $"Automated Circuit Breaker: Error rate spiked to {errorRatePercent:F1}% ({errors} errors out of {total} requests), breaching maximum allowable threshold of {policy.MaxErrorRatePercent:F1}% over {policy.SlidingWindowSeconds}s sliding window.";

                _logger.LogCritical(
                    "ANOMALY DETECTED for '{FlagKey}' in '{Env}'! Errors={Errors}, Total={Total}, Rate={Rate}%, Threshold={Threshold}%. Auto-killing flag...",
                    flag.Key, flag.EnvironmentId, errors, total, errorRatePercent, policy.MaxErrorRatePercent);

                await flagService.AutoKillFlagAsync(flag.Key, flag.EnvironmentId, killReason, cancellationToken);
            }
            else
            {
                _logger.LogTrace(
                    "Health Check: Flag '{FlagKey}' ({Env}) healthy. Total: {Total}, Errors: {Errors}, ErrorRate: {Rate}%.",
                    flag.Key, flag.EnvironmentId, total, errors, errorRatePercent);
            }
        }
    }
}
