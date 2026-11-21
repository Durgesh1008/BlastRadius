using System.Collections.Concurrent;
using BlastRadius.DemoApp.Models;

namespace BlastRadius.DemoApp.Services;

public interface IChaosManager
{
    ChaosConfig CurrentConfig { get; }
    void UpdateConfig(ChaosConfig config);
    void RecordTransaction(CheckoutResponse response);
    IReadOnlyList<CheckoutResponse> GetRecentTransactions(int limit = 50);
    LiveStatsResponse GetStats(bool isFlagEnabled, int rolloutPercentage, long flagVersion, string? killReason);
    void ResetStats();
}

public sealed class ChaosManager : IChaosManager
{
    private readonly ConcurrentQueue<CheckoutResponse> _recentTransactions = new();
    private ChaosConfig _config = new();
    private long _totalTransactions;
    private long _successfulTransactions;
    private long _failedTransactions;
    private long _newUpiCount;
    private long _legacyCardCount;

    public ChaosConfig CurrentConfig => _config;

    public void UpdateConfig(ChaosConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public void RecordTransaction(CheckoutResponse response)
    {
        Interlocked.Increment(ref _totalTransactions);
        if (response.Success)
        {
            Interlocked.Increment(ref _successfulTransactions);
        }
        else
        {
            Interlocked.Increment(ref _failedTransactions);
        }

        if (response.GatewayUsed == "NewInstantUPI")
        {
            Interlocked.Increment(ref _newUpiCount);
        }
        else
        {
            Interlocked.Increment(ref _legacyCardCount);
        }

        _recentTransactions.Enqueue(response);
        while (_recentTransactions.Count > 100)
        {
            _recentTransactions.TryDequeue(out _);
        }
    }

    public IReadOnlyList<CheckoutResponse> GetRecentTransactions(int limit = 50)
    {
        return _recentTransactions.Reverse().Take(limit).ToList();
    }

    public LiveStatsResponse GetStats(bool isFlagEnabled, int rolloutPercentage, long flagVersion, string? killReason)
    {
        long total = Interlocked.Read(ref _totalTransactions);
        long failed = Interlocked.Read(ref _failedTransactions);
        double rate = total > 0 ? Math.Round(((double)failed / total) * 100.0, 1) : 0.0;

        return new LiveStatsResponse
        {
            TotalTransactions = total,
            SuccessfulTransactions = Interlocked.Read(ref _successfulTransactions),
            FailedTransactions = failed,
            NewUpiCount = Interlocked.Read(ref _newUpiCount),
            LegacyCardCount = Interlocked.Read(ref _legacyCardCount),
            ErrorRatePercent = rate,
            IsNewUpiFlagEnabled = isFlagEnabled,
            RolloutPercentage = rolloutPercentage,
            FlagVersion = flagVersion,
            KillReason = killReason,
            Chaos = _config,
            RecentTransactions = GetRecentTransactions(15).ToList()
        };
    }

    public void ResetStats()
    {
        Interlocked.Exchange(ref _totalTransactions, 0);
        Interlocked.Exchange(ref _successfulTransactions, 0);
        Interlocked.Exchange(ref _failedTransactions, 0);
        Interlocked.Exchange(ref _newUpiCount, 0);
        Interlocked.Exchange(ref _legacyCardCount, 0);
        _recentTransactions.Clear();
    }
}
