using BlastRadius.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlastRadius.Api.Data;

/// <summary>
/// Entity Framework Core database context for the BlastRadius Control Plane.
/// Manages schema persistence in PostgreSQL (Neon) or in-memory fallback.
/// </summary>
public sealed class BlastRadiusDbContext : DbContext
{
    public BlastRadiusDbContext(DbContextOptions<BlastRadiusDbContext> options)
        : base(options)
    {
    }

    public DbSet<EnvironmentEntity> Environments => Set<EnvironmentEntity>();
    public DbSet<FeatureFlagEntity> FeatureFlags => Set<FeatureFlagEntity>();
    public DbSet<TargetingRuleEntity> TargetingRules => Set<TargetingRuleEntity>();
    public DbSet<BlastRadiusPolicyEntity> BlastRadiusPolicies => Set<BlastRadiusPolicyEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Environment configuration
        modelBuilder.Entity<EnvironmentEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });

        // Feature Flag configuration
        modelBuilder.Entity<FeatureFlagEntity>(entity =>
        {
            entity.HasKey(f => f.Id);
            
            // Unique index: Key must be unique per environment
            entity.HasIndex(f => new { f.EnvironmentId, f.Key }).IsUnique();

            entity.Property(f => f.Key).IsRequired().HasMaxLength(150);
            entity.Property(f => f.Name).IsRequired().HasMaxLength(200);
            entity.Property(f => f.EnvironmentId).IsRequired().HasMaxLength(50);
            
            // Optimistic concurrency token
            entity.Property(f => f.Version).IsConcurrencyToken();

            entity.HasOne(f => f.Environment)
                  .WithMany(e => e.FeatureFlags)
                  .HasForeignKey(f => f.EnvironmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.BlastRadiusPolicy)
                  .WithOne(p => p.FeatureFlag)
                  .HasForeignKey<BlastRadiusPolicyEntity>(p => p.FeatureFlagId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(f => f.TargetingRules)
                  .WithOne(r => r.FeatureFlag)
                  .HasForeignKey(r => r.FeatureFlagId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Targeting Rules configuration
        modelBuilder.Entity<TargetingRuleEntity>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.AttributeName).IsRequired().HasMaxLength(100);
            entity.Property(r => r.ValuesJson).IsRequired();
            entity.HasIndex(r => new { r.FeatureFlagId, r.Priority });
        });

        // Audit Log configuration
        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.FlagKey, a.TimestampUtc });
            entity.Property(a => a.FlagKey).IsRequired().HasMaxLength(150);
            entity.Property(a => a.Action).IsRequired().HasMaxLength(100);
            entity.Property(a => a.TriggeredBy).IsRequired().HasMaxLength(100);
        });

        // Seed default environments
        modelBuilder.Entity<EnvironmentEntity>().HasData(
            new EnvironmentEntity
            {
                Id = "production",
                Name = "Production",
                Description = "Primary production customer-facing environment",
                CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new EnvironmentEntity
            {
                Id = "staging",
                Name = "Staging",
                Description = "Pre-release staging environment for verification",
                CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new EnvironmentEntity
            {
                Id = "development",
                Name = "Development",
                Description = "Local and QA sandbox development environment",
                CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            }
        );
    }
}
