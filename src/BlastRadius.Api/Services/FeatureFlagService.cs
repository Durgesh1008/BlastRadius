using System.Text.Json;
using BlastRadius.Api.Data;
using BlastRadius.Api.Data.Entities;
using BlastRadius.Api.DTOs;
using BlastRadius.Sdk.Models;
using Microsoft.EntityFrameworkCore;

namespace BlastRadius.Api.Services;

/// <summary>
/// Implements feature flag business operations, optimistic concurrency, and audit trails.
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly BlastRadiusDbContext _db;
    private readonly IRedisPublisher _publisher;
    private readonly ILogger<FeatureFlagService> _logger;

    public FeatureFlagService(
        BlastRadiusDbContext db,
        IRedisPublisher publisher,
        ILogger<FeatureFlagService> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FeatureFlagResponse>> GetAllFlagsAsync(string environment, CancellationToken cancellationToken = default)
    {
        var entities = await _db.FeatureFlags
            .AsNoTracking()
            .Include(f => f.BlastRadiusPolicy)
            .Include(f => f.TargetingRules)
            .Where(f => f.EnvironmentId == environment)
            .OrderBy(f => f.Key)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToResponse).ToList();
    }

    public async Task<FeatureFlagResponse?> GetFlagByKeyAsync(string key, string environment, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FeatureFlags
            .AsNoTracking()
            .Include(f => f.BlastRadiusPolicy)
            .Include(f => f.TargetingRules)
            .FirstOrDefaultAsync(f => f.Key == key && f.EnvironmentId == environment, cancellationToken);

        return entity != null ? MapToResponse(entity) : null;
    }

    public async Task<IReadOnlyList<FeatureFlagConfig>> GetSnapshotAsync(string environment, CancellationToken cancellationToken = default)
    {
        var entities = await _db.FeatureFlags
            .AsNoTracking()
            .Include(f => f.BlastRadiusPolicy)
            .Include(f => f.TargetingRules)
            .Where(f => f.EnvironmentId == environment)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSdkConfig).ToList();
    }

    public async Task<FeatureFlagResponse> CreateFlagAsync(CreateFeatureFlagRequest request, string triggeredBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool exists = await _db.FeatureFlags.AnyAsync(
            f => f.Key == request.Key && f.EnvironmentId == request.Environment,
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"A feature flag with key '{request.Key}' already exists in environment '{request.Environment}'.");
        }

        var entity = new FeatureFlagEntity
        {
            Key = request.Key,
            Name = request.Name,
            Description = request.Description,
            IsEnabled = request.IsEnabled,
            RolloutPercentage = request.RolloutPercentage,
            FallbackValue = request.FallbackValue,
            EnvironmentId = request.Environment,
            Version = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        if (request.BlastRadiusPolicy != null)
        {
            entity.BlastRadiusPolicy = new BlastRadiusPolicyEntity
            {
                FeatureFlagId = entity.Id,
                MaxErrorRatePercent = request.BlastRadiusPolicy.MaxErrorRatePercent,
                MinRequestsThreshold = request.BlastRadiusPolicy.MinRequestsThreshold,
                SlidingWindowSeconds = request.BlastRadiusPolicy.SlidingWindowSeconds,
                AutoKillEnabled = request.BlastRadiusPolicy.AutoKillEnabled,
                CooldownSeconds = request.BlastRadiusPolicy.CooldownSeconds
            };
        }

        if (request.TargetingRules is { Count: > 0 })
        {
            foreach (var ruleDto in request.TargetingRules)
            {
                entity.TargetingRules.Add(new TargetingRuleEntity
                {
                    FeatureFlagId = entity.Id,
                    Priority = ruleDto.Priority,
                    AttributeName = ruleDto.AttributeName,
                    Operator = ruleDto.Operator,
                    ValuesJson = JsonSerializer.Serialize(ruleDto.Values),
                    ServeValue = ruleDto.ServeValue,
                    Description = ruleDto.Description
                });
            }
        }

        _db.FeatureFlags.Add(entity);

        var auditLog = new AuditLogEntity
        {
            FlagKey = entity.Key,
            Environment = entity.EnvironmentId,
            Action = "Created",
            TriggeredBy = triggeredBy,
            Version = entity.Version,
            Details = $"Created flag '{entity.Name}' with initial rollout {entity.RolloutPercentage}% and enabled={entity.IsEnabled}."
        };
        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync(cancellationToken);

        // Broadcast to fleet via Redis Pub/Sub
        await _publisher.PublishUpdateAsync(new FlagUpdateNotification
        {
            FlagKey = entity.Key,
            Action = FlagUpdateNotification.Actions.Updated,
            Version = entity.Version,
            Config = MapToSdkConfig(entity)
        }, cancellationToken);

        return MapToResponse(entity);
    }

    public async Task<FeatureFlagResponse> UpdateFlagAsync(string key, string environment, UpdateFeatureFlagRequest request, string triggeredBy, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FeatureFlags
            .Include(f => f.BlastRadiusPolicy)
            .Include(f => f.TargetingRules)
            .FirstOrDefaultAsync(f => f.Key == key && f.EnvironmentId == environment, cancellationToken);

        if (entity == null)
        {
            throw new KeyNotFoundException($"Flag '{key}' was not found in environment '{environment}'.");
        }

        // Optimistic Concurrency Check
        if (request.ExpectedVersion > 0 && entity.Version != request.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException($"Concurrency conflict: Flag '{key}' is at version {entity.Version}, but expected version was {request.ExpectedVersion}.");
        }

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.IsEnabled = request.IsEnabled;
        entity.RolloutPercentage = request.RolloutPercentage;
        entity.FallbackValue = request.FallbackValue;
        entity.Version++;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        // If manually re-enabled, clear prior kill reason
        if (entity.IsEnabled)
        {
            entity.KillReason = null;
        }

        // Update Policy
        if (request.BlastRadiusPolicy != null)
        {
            if (entity.BlastRadiusPolicy == null)
            {
                entity.BlastRadiusPolicy = new BlastRadiusPolicyEntity { FeatureFlagId = entity.Id };
            }

            entity.BlastRadiusPolicy.MaxErrorRatePercent = request.BlastRadiusPolicy.MaxErrorRatePercent;
            entity.BlastRadiusPolicy.MinRequestsThreshold = request.BlastRadiusPolicy.MinRequestsThreshold;
            entity.BlastRadiusPolicy.SlidingWindowSeconds = request.BlastRadiusPolicy.SlidingWindowSeconds;
            entity.BlastRadiusPolicy.AutoKillEnabled = request.BlastRadiusPolicy.AutoKillEnabled;
            entity.BlastRadiusPolicy.CooldownSeconds = request.BlastRadiusPolicy.CooldownSeconds;
        }

        // Replace rules
        _db.TargetingRules.RemoveRange(entity.TargetingRules);
        entity.TargetingRules.Clear();

        if (request.TargetingRules is { Count: > 0 })
        {
            foreach (var ruleDto in request.TargetingRules)
            {
                entity.TargetingRules.Add(new TargetingRuleEntity
                {
                    FeatureFlagId = entity.Id,
                    Priority = ruleDto.Priority,
                    AttributeName = ruleDto.AttributeName,
                    Operator = ruleDto.Operator,
                    ValuesJson = JsonSerializer.Serialize(ruleDto.Values),
                    ServeValue = ruleDto.ServeValue,
                    Description = ruleDto.Description
                });
            }
        }

        var auditLog = new AuditLogEntity
        {
            FlagKey = entity.Key,
            Environment = entity.EnvironmentId,
            Action = "Updated",
            TriggeredBy = triggeredBy,
            Version = entity.Version,
            Details = $"Updated flag configuration. Rollout: {entity.RolloutPercentage}%, Enabled: {entity.IsEnabled}."
        };
        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync(cancellationToken);

        // Broadcast to fleet via Redis Pub/Sub
        await _publisher.PublishUpdateAsync(new FlagUpdateNotification
        {
            FlagKey = entity.Key,
            Action = FlagUpdateNotification.Actions.Updated,
            Version = entity.Version,
            Config = MapToSdkConfig(entity)
        }, cancellationToken);

        return MapToResponse(entity);
    }

    public async Task<FeatureFlagResponse> UpdateRolloutAsync(string key, string environment, UpdateRolloutRequest request, string triggeredBy, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FeatureFlags
            .Include(f => f.BlastRadiusPolicy)
            .Include(f => f.TargetingRules)
            .FirstOrDefaultAsync(f => f.Key == key && f.EnvironmentId == environment, cancellationToken);

        if (entity == null)
        {
            throw new KeyNotFoundException($"Flag '{key}' was not found in environment '{environment}'.");
        }

        int previousRollout = entity.RolloutPercentage;
        entity.RolloutPercentage = request.RolloutPercentage;
        entity.Version++;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var auditLog = new AuditLogEntity
        {
            FlagKey = entity.Key,
            Environment = entity.EnvironmentId,
            Action = "RolloutChanged",
            TriggeredBy = triggeredBy,
            Version = entity.Version,
            Details = $"Canary rollout scaled from {previousRollout}% to {entity.RolloutPercentage}%."
        };
        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync(cancellationToken);

        // Broadcast to fleet via Redis Pub/Sub
        await _publisher.PublishUpdateAsync(new FlagUpdateNotification
        {
            FlagKey = entity.Key,
            Action = FlagUpdateNotification.Actions.Updated,
            Version = entity.Version,
            Config = MapToSdkConfig(entity)
        }, cancellationToken);

        return MapToResponse(entity);
    }

    public async Task<FeatureFlagResponse> KillFlagAsync(string key, string environment, KillFeatureFlagRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FeatureFlags
            .Include(f => f.BlastRadiusPolicy)
            .Include(f => f.TargetingRules)
            .FirstOrDefaultAsync(f => f.Key == key && f.EnvironmentId == environment, cancellationToken);

        if (entity == null)
        {
            throw new KeyNotFoundException($"Flag '{key}' was not found in environment '{environment}'.");
        }

        entity.IsEnabled = false;
        entity.KillReason = request.Reason;
        entity.Version++;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var auditLog = new AuditLogEntity
        {
            FlagKey = entity.Key,
            Environment = entity.EnvironmentId,
            Action = "ManualKill",
            TriggeredBy = request.TriggeredBy,
            Version = entity.Version,
            Details = $"Operator executed manual kill switch. Reason: {request.Reason}"
        };
        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync(cancellationToken);

        // Instant emergency kill broadcast
        await _publisher.PublishUpdateAsync(new FlagUpdateNotification
        {
            FlagKey = entity.Key,
            Action = FlagUpdateNotification.Actions.CircuitBreakerKilled,
            Version = entity.Version,
            KillReason = request.Reason
        }, cancellationToken);

        return MapToResponse(entity);
    }

    public async Task<FeatureFlagResponse?> AutoKillFlagAsync(string key, string environment, string killReason, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FeatureFlags
            .Include(f => f.BlastRadiusPolicy)
            .Include(f => f.TargetingRules)
            .FirstOrDefaultAsync(f => f.Key == key && f.EnvironmentId == environment, cancellationToken);

        if (entity == null || !entity.IsEnabled)
        {
            return null; // Already disabled or missing
        }

        entity.IsEnabled = false;
        entity.KillReason = killReason;
        entity.Version++;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var auditLog = new AuditLogEntity
        {
            FlagKey = entity.Key,
            Environment = entity.EnvironmentId,
            Action = "CircuitBreakerKilled",
            TriggeredBy = "Automated:CircuitBreakerWorker",
            Version = entity.Version,
            Details = killReason
        };
        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogCritical("CIRCUIT BREAKER ACTIVATED: Flag '{FlagKey}' in '{Env}' auto-killed! Reason: {Reason}",
            key, environment, killReason);

        // Broadcast kill signal via Redis Pub/Sub immediately (< 10ms fanout)
        await _publisher.PublishUpdateAsync(new FlagUpdateNotification
        {
            FlagKey = entity.Key,
            Action = FlagUpdateNotification.Actions.CircuitBreakerKilled,
            Version = entity.Version,
            KillReason = killReason
        }, cancellationToken);

        return MapToResponse(entity);
    }

    public async Task<IReadOnlyList<AuditLogResponse>> GetAuditLogsAsync(string? flagKey, string environment, int limit = 100, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(a => a.Environment == environment);

        if (!string.IsNullOrWhiteSpace(flagKey))
        {
            query = query.Where(a => a.FlagKey == flagKey);
        }

        var logs = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return logs.Select(a => new AuditLogResponse
        {
            Id = a.Id,
            FlagKey = a.FlagKey,
            Environment = a.Environment,
            Action = a.Action,
            TriggeredBy = a.TriggeredBy,
            Details = a.Details,
            Version = a.Version,
            TimestampUtc = a.TimestampUtc
        }).ToList();
    }

    private static FeatureFlagResponse MapToResponse(FeatureFlagEntity entity)
    {
        return new FeatureFlagResponse
        {
            Id = entity.Id,
            Key = entity.Key,
            Name = entity.Name,
            Description = entity.Description,
            IsEnabled = entity.IsEnabled,
            RolloutPercentage = entity.RolloutPercentage,
            Version = entity.Version,
            FallbackValue = entity.FallbackValue,
            KillReason = entity.KillReason,
            Environment = entity.EnvironmentId,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            BlastRadiusPolicy = entity.BlastRadiusPolicy != null
                ? new BlastRadiusPolicyDto
                {
                    MaxErrorRatePercent = entity.BlastRadiusPolicy.MaxErrorRatePercent,
                    MinRequestsThreshold = entity.BlastRadiusPolicy.MinRequestsThreshold,
                    SlidingWindowSeconds = entity.BlastRadiusPolicy.SlidingWindowSeconds,
                    AutoKillEnabled = entity.BlastRadiusPolicy.AutoKillEnabled,
                    CooldownSeconds = entity.BlastRadiusPolicy.CooldownSeconds
                }
                : null,
            TargetingRules = entity.TargetingRules
                .OrderBy(r => r.Priority)
                .Select(r => new TargetingRuleDto
                {
                    Priority = r.Priority,
                    AttributeName = r.AttributeName,
                    Operator = r.Operator,
                    Values = DeserializeValues(r.ValuesJson),
                    ServeValue = r.ServeValue,
                    Description = r.Description
                })
                .ToList()
        };
    }

    private static FeatureFlagConfig MapToSdkConfig(FeatureFlagEntity entity)
    {
        return new FeatureFlagConfig
        {
            FlagKey = entity.Key,
            IsEnabled = entity.IsEnabled,
            RolloutPercentage = entity.RolloutPercentage,
            Version = entity.Version,
            FallbackValue = entity.FallbackValue,
            KillReason = entity.KillReason,
            Environment = entity.EnvironmentId,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            BlastRadiusPolicy = entity.BlastRadiusPolicy != null
                ? new BlastRadiusPolicy
                {
                    MaxErrorRatePercent = entity.BlastRadiusPolicy.MaxErrorRatePercent,
                    MinRequestsThreshold = entity.BlastRadiusPolicy.MinRequestsThreshold,
                    SlidingWindowSeconds = entity.BlastRadiusPolicy.SlidingWindowSeconds,
                    AutoKillEnabled = entity.BlastRadiusPolicy.AutoKillEnabled,
                    CooldownSeconds = entity.BlastRadiusPolicy.CooldownSeconds
                }
                : null,
            Rules = entity.TargetingRules
                .OrderBy(r => r.Priority)
                .Select(r => new TargetingRule
                {
                    Id = r.Id.ToString(),
                    Priority = r.Priority,
                    AttributeName = r.AttributeName,
                    Operator = r.Operator,
                    Values = DeserializeValues(r.ValuesJson),
                    ServeValue = r.ServeValue,
                    Description = r.Description
                })
                .ToList()
        };
    }

    private static List<string> DeserializeValues(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
