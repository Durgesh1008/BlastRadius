using System.ComponentModel.DataAnnotations;

namespace BlastRadius.Api.Data.Entities;

/// <summary>
/// Immutable audit log trail capturing every configuration change, rollout percentage shift,
/// and automated circuit-breaker trip.
/// </summary>
public sealed class AuditLogEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public string FlagKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Environment { get; set; } = "production";

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty; // e.g., "Created", "Updated", "RolloutChanged", "CircuitBreakerKilled", "Reinstated"

    [Required]
    [MaxLength(100)]
    public string TriggeredBy { get; set; } = string.Empty; // e.g., "AdminUser:alice", "Automated:CircuitBreakerWorker"

    [MaxLength(2000)]
    public string? Details { get; set; }

    public long Version { get; set; }

    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
