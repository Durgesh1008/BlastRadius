using BlastRadius.Api.DTOs;
using BlastRadius.Sdk.Models;

namespace BlastRadius.Api.Services;

/// <summary>
/// Business logic contract for managing feature flags, canary rollouts, audit trails, and kill switches.
/// </summary>
public interface IFeatureFlagService
{
    Task<IReadOnlyList<FeatureFlagResponse>> GetAllFlagsAsync(string environment, CancellationToken cancellationToken = default);
    
    Task<FeatureFlagResponse?> GetFlagByKeyAsync(string key, string environment, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Returns the pre-warm flag configuration snapshot consumed by SDK fleet instances upon startup.
    /// </summary>
    Task<IReadOnlyList<FeatureFlagConfig>> GetSnapshotAsync(string environment, CancellationToken cancellationToken = default);
    
    Task<FeatureFlagResponse> CreateFlagAsync(CreateFeatureFlagRequest request, string triggeredBy, CancellationToken cancellationToken = default);
    
    Task<FeatureFlagResponse> UpdateFlagAsync(string key, string environment, UpdateFeatureFlagRequest request, string triggeredBy, CancellationToken cancellationToken = default);
    
    Task<FeatureFlagResponse> UpdateRolloutAsync(string key, string environment, UpdateRolloutRequest request, string triggeredBy, CancellationToken cancellationToken = default);
    
    Task<FeatureFlagResponse> KillFlagAsync(string key, string environment, KillFeatureFlagRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Autonomous kill switch invoked directly by the background circuit breaker worker upon detecting an anomaly.
    /// </summary>
    Task<FeatureFlagResponse?> AutoKillFlagAsync(string key, string environment, string killReason, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<AuditLogResponse>> GetAuditLogsAsync(string? flagKey, string environment, int limit = 100, CancellationToken cancellationToken = default);
}
