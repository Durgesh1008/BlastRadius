using BlastRadius.Api.Data;
using BlastRadius.Api.DTOs;
using BlastRadius.Api.Services;
using BlastRadius.Sdk.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlastRadius.Api.Tests;

public class FeatureFlagServiceTests
{
    private sealed class FakeRedisPublisher : IRedisPublisher
    {
        public List<FlagUpdateNotification> PublishedNotifications { get; } = new();

        public Task PublishUpdateAsync(FlagUpdateNotification notification, CancellationToken cancellationToken = default)
        {
            PublishedNotifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private static BlastRadiusDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BlastRadiusDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new BlastRadiusDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task CreateFlagAsync_PersistsFlagAndEmitsRedisNotification()
    {
        using var db = CreateInMemoryContext();
        var publisher = new FakeRedisPublisher();
        var service = new FeatureFlagService(db, publisher, NullLogger<FeatureFlagService>.Instance);

        var request = new CreateFeatureFlagRequest
        {
            Key = "new-upi-checkout",
            Name = "New UPI Checkout",
            Description = "Enables instant 1-click UPI payments",
            IsEnabled = true,
            RolloutPercentage = 15,
            Environment = "production",
            BlastRadiusPolicy = new BlastRadiusPolicyDto
            {
                MaxErrorRatePercent = 2.5,
                MinRequestsThreshold = 50,
                SlidingWindowSeconds = 60
            }
        };

        var response = await service.CreateFlagAsync(request, "Alice");

        Assert.NotNull(response);
        Assert.Equal("new-upi-checkout", response.Key);
        Assert.True(response.IsEnabled);
        Assert.Equal(15, response.RolloutPercentage);
        Assert.Equal(1, response.Version);

        // Verify audit log persisted
        var auditLogs = await db.AuditLogs.ToListAsync();
        Assert.Single(auditLogs);
        Assert.Equal("Created", auditLogs[0].Action);
        Assert.Equal("Alice", auditLogs[0].TriggeredBy);

        // Verify Redis notification broadcasted
        Assert.Single(publisher.PublishedNotifications);
        Assert.Equal("new-upi-checkout", publisher.PublishedNotifications[0].FlagKey);
        Assert.Equal(FlagUpdateNotification.Actions.Updated, publisher.PublishedNotifications[0].Action);
    }

    [Fact]
    public async Task CreateFlagAsync_ThrowsOnDuplicateKeyInSameEnvironment()
    {
        using var db = CreateInMemoryContext();
        var publisher = new FakeRedisPublisher();
        var service = new FeatureFlagService(db, publisher, NullLogger<FeatureFlagService>.Instance);

        var request = new CreateFeatureFlagRequest
        {
            Key = "duplicate-flag",
            Name = "Duplicate Test",
            Environment = "production"
        };

        await service.CreateFlagAsync(request, "Alice");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateFlagAsync(request, "Bob"));
    }

    [Fact]
    public async Task UpdateRolloutAsync_ScalesPercentageAndIncrementsVersion()
    {
        using var db = CreateInMemoryContext();
        var publisher = new FakeRedisPublisher();
        var service = new FeatureFlagService(db, publisher, NullLogger<FeatureFlagService>.Instance);

        await service.CreateFlagAsync(new CreateFeatureFlagRequest
        {
            Key = "canary-slider",
            Name = "Canary Slider Test",
            RolloutPercentage = 10,
            Environment = "production"
        }, "Admin");

        var updated = await service.UpdateRolloutAsync("canary-slider", "production", new UpdateRolloutRequest
        {
            RolloutPercentage = 25
        }, "Admin");

        Assert.Equal(25, updated.RolloutPercentage);
        Assert.Equal(2, updated.Version); // Version incremented

        var auditLog = await db.AuditLogs.OrderByDescending(a => a.TimestampUtc).FirstOrDefaultAsync();
        Assert.NotNull(auditLog);
        Assert.Equal("RolloutChanged", auditLog.Action);
    }

    [Fact]
    public async Task KillFlagAsync_DisablesFeatureAndBroadcastsEmergencyKill()
    {
        using var db = CreateInMemoryContext();
        var publisher = new FakeRedisPublisher();
        var service = new FeatureFlagService(db, publisher, NullLogger<FeatureFlagService>.Instance);

        await service.CreateFlagAsync(new CreateFeatureFlagRequest
        {
            Key = "buggy-feature",
            Name = "Buggy Feature",
            IsEnabled = true,
            RolloutPercentage = 100,
            Environment = "production"
        }, "Admin");

        var killed = await service.KillFlagAsync("buggy-feature", "production", new KillFeatureFlagRequest
        {
            Reason = "Memory leak detected in production container",
            TriggeredBy = "OnCallEngineer:Charlie"
        });

        Assert.False(killed.IsEnabled);
        Assert.Equal("Memory leak detected in production container", killed.KillReason);

        // Verify kill signal in Redis
        var lastNotification = publisher.PublishedNotifications.Last();
        Assert.Equal(FlagUpdateNotification.Actions.CircuitBreakerKilled, lastNotification.Action);
        Assert.Equal("buggy-feature", lastNotification.FlagKey);
    }

    [Fact]
    public async Task AutoKillFlagAsync_TripsCircuitBreakerAndRecordsAutomatedAuditTrail()
    {
        using var db = CreateInMemoryContext();
        var publisher = new FakeRedisPublisher();
        var service = new FeatureFlagService(db, publisher, NullLogger<FeatureFlagService>.Instance);

        await service.CreateFlagAsync(new CreateFeatureFlagRequest
        {
            Key = "auto-kill-target",
            Name = "Auto Kill Target",
            IsEnabled = true,
            RolloutPercentage = 50,
            Environment = "production"
        }, "Admin");

        string autoKillReason = "Circuit Breaker: Error rate spiked to 14.5% (> 2.0% limit)";
        var result = await service.AutoKillFlagAsync("auto-kill-target", "production", autoKillReason);

        Assert.NotNull(result);
        Assert.False(result.IsEnabled);
        Assert.Equal(autoKillReason, result.KillReason);

        // Verify audit log marked as automated
        var audit = await db.AuditLogs.OrderByDescending(a => a.TimestampUtc).FirstAsync();
        Assert.Equal("CircuitBreakerKilled", audit.Action);
        Assert.Equal("Automated:CircuitBreakerWorker", audit.TriggeredBy);
    }

    [Fact]
    public async Task GetSnapshotAsync_MapsCorrectlyToSdkConfig()
    {
        using var db = CreateInMemoryContext();
        var publisher = new FakeRedisPublisher();
        var service = new FeatureFlagService(db, publisher, NullLogger<FeatureFlagService>.Instance);

        await service.CreateFlagAsync(new CreateFeatureFlagRequest
        {
            Key = "snapshot-flag",
            Name = "Snapshot Flag",
            IsEnabled = true,
            RolloutPercentage = 30,
            Environment = "production",
            TargetingRules = new List<TargetingRuleDto>
            {
                new()
                {
                    Priority = 1,
                    AttributeName = "plan",
                    Operator = RuleOperator.Equals,
                    Values = new List<string> { "Enterprise" },
                    ServeValue = true
                }
            }
        }, "Admin");

        var snapshot = await service.GetSnapshotAsync("production");

        Assert.Single(snapshot);
        var flag = snapshot[0];
        Assert.Equal("snapshot-flag", flag.FlagKey);
        Assert.True(flag.IsEnabled);
        Assert.Equal(30, flag.RolloutPercentage);
        Assert.Single(flag.Rules);
        Assert.Equal("plan", flag.Rules[0].AttributeName);
    }
}
