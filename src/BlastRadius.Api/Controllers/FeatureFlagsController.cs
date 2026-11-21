using BlastRadius.Api.DTOs;
using BlastRadius.Api.Services;
using BlastRadius.Sdk.Models;
using Microsoft.AspNetCore.Mvc;

namespace BlastRadius.Api.Controllers;

[ApiController]
[Route("api/v1/flags")]
[Produces("application/json")]
public sealed class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _flagService;
    private readonly ILogger<FeatureFlagsController> _logger;

    public FeatureFlagsController(IFeatureFlagService flagService, ILogger<FeatureFlagsController> logger)
    {
        _flagService = flagService;
        _logger = logger;
    }

    /// <summary>
    /// Returns all feature flags registered in the given environment.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureFlagResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllFlags(
        [FromQuery] string environment = "production",
        CancellationToken cancellationToken = default)
    {
        var flags = await _flagService.GetAllFlagsAsync(environment, cancellationToken);
        return Ok(flags);
    }

    /// <summary>
    /// Returns the pre-warm configuration snapshot used by SDK fleet instances upon startup.
    /// </summary>
    [HttpGet("snapshot")]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureFlagConfig>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSnapshot(
        [FromQuery] string environment = "production",
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _flagService.GetSnapshotAsync(environment, cancellationToken);
        return Ok(snapshot);
    }

    /// <summary>
    /// Retrieves an individual feature flag configuration by its unique key.
    /// </summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(FeatureFlagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFlagByKey(
        [FromRoute] string key,
        [FromQuery] string environment = "production",
        CancellationToken cancellationToken = default)
    {
        var flag = await _flagService.GetFlagByKeyAsync(key, environment, cancellationToken);
        if (flag == null)
        {
            return NotFound(new { error = $"Feature flag '{key}' not found in environment '{environment}'." });
        }

        return Ok(flag);
    }

    /// <summary>
    /// Registers a new feature flag in the control plane.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(FeatureFlagResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateFlag(
        [FromBody] CreateFeatureFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string operatorName = User.Identity?.Name ?? "AdminUser";
            var created = await _flagService.CreateFlagAsync(request, operatorName, cancellationToken);
            return CreatedAtAction(nameof(GetFlagByKey), new { key = created.Key, environment = created.Environment }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing feature flag configuration with optimistic concurrency enforcement.
    /// </summary>
    [HttpPut("{key}")]
    [ProducesResponseType(typeof(FeatureFlagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateFlag(
        [FromRoute] string key,
        [FromBody] UpdateFeatureFlagRequest request,
        [FromQuery] string environment = "production",
        CancellationToken cancellationToken = default)
    {
        try
        {
            string operatorName = User.Identity?.Name ?? "AdminUser";
            var updated = await _flagService.UpdateFlagAsync(key, environment, request, operatorName, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex) when (ex is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Scales or adjusts the canary rollout percentage (0% to 100%) for a feature flag.
    /// </summary>
    [HttpPatch("{key}/rollout")]
    [ProducesResponseType(typeof(FeatureFlagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRollout(
        [FromRoute] string key,
        [FromBody] UpdateRolloutRequest request,
        [FromQuery] string environment = "production",
        CancellationToken cancellationToken = default)
    {
        try
        {
            string operatorName = User.Identity?.Name ?? "AdminUser";
            var updated = await _flagService.UpdateRolloutAsync(key, environment, request, operatorName, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Manually trips the emergency kill switch for a feature flag, broadcasting an instant kill event to all nodes.
    /// </summary>
    [HttpPost("{key}/kill")]
    [ProducesResponseType(typeof(FeatureFlagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> KillFlag(
        [FromRoute] string key,
        [FromBody] KillFeatureFlagRequest request,
        [FromQuery] string environment = "production",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var killed = await _flagService.KillFlagAsync(key, environment, request, cancellationToken);
            return Ok(killed);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
