using BlastRadius.Api.DTOs;
using BlastRadius.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlastRadius.Api.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
[Produces("application/json")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IFeatureFlagService _flagService;

    public AuditLogsController(IFeatureFlagService flagService)
    {
        _flagService = flagService;
    }

    /// <summary>
    /// Returns the audit history trail for feature flag mutations and automated circuit-breaker events.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? flagKey = null,
        [FromQuery] string environment = "production",
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var logs = await _flagService.GetAuditLogsAsync(flagKey, environment, limit, cancellationToken);
        return Ok(logs);
    }
}

[ApiController]
[Route("api/v1/telemetry")]
[Produces("application/json")]
public sealed class TelemetryController : ControllerBase
{
    private readonly ITelemetryReader _telemetryReader;
    private readonly IFeatureFlagService _flagService;

    public TelemetryController(ITelemetryReader telemetryReader, IFeatureFlagService flagService)
    {
        _telemetryReader = telemetryReader;
        _flagService = flagService;
    }

    /// <summary>
    /// Inspects the live sliding-window error rate and request volume for a specific feature flag.
    /// </summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(TelemetryStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTelemetryStatus(
        [FromRoute] string key,
        [FromQuery] string environment = "production",
        [FromQuery] int slidingWindowSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        var flag = await _flagService.GetFlagByKeyAsync(key, environment, cancellationToken);
        if (flag == null)
        {
            return NotFound(new { error = $"Flag '{key}' was not found in environment '{environment}'." });
        }

        var (total, errors, rate) = await _telemetryReader.GetMetricsInSlidingWindowAsync(key, environment, slidingWindowSeconds);

        double maxThreshold = flag.BlastRadiusPolicy?.MaxErrorRatePercent ?? 2.0;
        int minRequests = flag.BlastRadiusPolicy?.MinRequestsThreshold ?? 50;
        bool breached = total >= minRequests && rate > maxThreshold;

        var response = new TelemetryStatusResponse
        {
            FlagKey = key,
            Environment = environment,
            TotalRequestsInWindow = total,
            ErrorRequestsInWindow = errors,
            ErrorRatePercent = rate,
            SlidingWindowSeconds = slidingWindowSeconds,
            BreachedThreshold = breached
        };

        return Ok(response);
    }
}
