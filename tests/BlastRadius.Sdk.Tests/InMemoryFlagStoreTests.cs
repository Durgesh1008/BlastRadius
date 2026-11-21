using BlastRadius.Sdk.Configuration;
using BlastRadius.Sdk.Models;
using BlastRadius.Sdk.Store;
using Microsoft.Extensions.Options;

namespace BlastRadius.Sdk.Tests;

public class InMemoryFlagStoreTests
{
    private readonly InMemoryFlagStore _store;

    public InMemoryFlagStoreTests()
    {
        var options = Options.Create(new BlastRadiusOptions
        {
            Environment = "test",
            SyncIntervalSeconds = 0 // Disable background timer for unit tests
        });
        _store = new InMemoryFlagStore(options);
    }

    [Fact]
    public void UpdateOrAddFlag_StoresFlagSuccessfully()
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "checkout-v2",
            IsEnabled = true,
            RolloutPercentage = 25,
            Version = 1
        };

        _store.UpdateOrAddFlag(flag);
        var retrieved = _store.GetFlag("checkout-v2");

        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsEnabled);
        Assert.Equal(25, retrieved.RolloutPercentage);
    }

    [Fact]
    public void UpdateOrAddFlag_RejectsStaleVersion()
    {
        var flagV2 = new FeatureFlagConfig
        {
            FlagKey = "checkout-v2",
            IsEnabled = true,
            RolloutPercentage = 50,
            Version = 2
        };

        var flagV1Stale = new FeatureFlagConfig
        {
            FlagKey = "checkout-v2",
            IsEnabled = false,
            RolloutPercentage = 10,
            Version = 1
        };

        _store.UpdateOrAddFlag(flagV2);
        // Attempt to apply older version (e.g. out-of-order network arrival)
        _store.UpdateOrAddFlag(flagV1Stale);

        var current = _store.GetFlag("checkout-v2");
        Assert.NotNull(current);
        Assert.Equal(2, current.Version);
        Assert.True(current.IsEnabled);
        Assert.Equal(50, current.RolloutPercentage);
    }

    [Fact]
    public void MarkKilled_DisablesFlagImmediately()
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "upi-payment",
            IsEnabled = true,
            RolloutPercentage = 100,
            Version = 3
        };

        _store.UpdateOrAddFlag(flag);

        // Trip the circuit breaker
        _store.MarkKilled("upi-payment", "Error rate exceeded 2.5%", 4);

        var updated = _store.GetFlag("upi-payment");
        Assert.NotNull(updated);
        Assert.False(updated.IsEnabled);
        Assert.Equal("Error rate exceeded 2.5%", updated.KillReason);
        Assert.Equal(4, updated.Version);
    }

    [Fact]
    public void RemoveFlag_EvictsFlagFromCache()
    {
        var flag = new FeatureFlagConfig
        {
            FlagKey = "obsolete-feature",
            IsEnabled = true,
            Version = 1
        };

        _store.UpdateOrAddFlag(flag);
        Assert.NotNull(_store.GetFlag("obsolete-feature"));

        _store.RemoveFlag("obsolete-feature");
        Assert.Null(_store.GetFlag("obsolete-feature"));
    }
}
