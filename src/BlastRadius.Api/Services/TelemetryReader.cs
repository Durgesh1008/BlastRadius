using StackExchange.Redis;

namespace BlastRadius.Api.Services;

/// <summary>
/// Reads real-time time-bucketed metrics from Redis to evaluate sliding-window error rates.
/// Safe against Redis disconnections or missing keys.
/// </summary>
public sealed class TelemetryReader : ITelemetryReader
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelemetryReader> _logger;
    private IConnectionMultiplexer? _redis;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public TelemetryReader(IConfiguration configuration, ILogger<TelemetryReader> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(long TotalRequests, long ErrorRequests, double ErrorRatePercent)> GetMetricsInSlidingWindowAsync(
        string flagKey,
        string environment,
        int slidingWindowSeconds)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
        {
            return (0, 0, 0.0);
        }

        try
        {
            var multiplexer = await GetMultiplexerAsync();
            if (multiplexer == null || !multiplexer.IsConnected)
            {
                return (0, 0, 0.0);
            }

            var db = multiplexer.GetDatabase();

            // Calculate which minute buckets fall inside the sliding window
            long currentMinute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            int minutesToInspect = Math.Max(1, (int)Math.Ceiling(slidingWindowSeconds / 60.0));

            long total = 0;
            long errors = 0;

            for (int i = 0; i < minutesToInspect; i++)
            {
                long bucketMinute = currentMinute - i;
                string redisKey = $"blastradius:telemetry:{environment}:{flagKey}:{bucketMinute}";

                var hashValues = await db.HashGetAsync(redisKey, new RedisValue[] { "total", "errors" });

                if (hashValues[0].HasValue && long.TryParse(hashValues[0].ToString(), out long bucketTotal))
                {
                    total += bucketTotal;
                }

                if (hashValues[1].HasValue && long.TryParse(hashValues[1].ToString(), out long bucketErrors))
                {
                    errors += bucketErrors;
                }
            }

            double errorRate = total > 0 ? ((double)errors / total) * 100.0 : 0.0;
            return (total, errors, Math.Round(errorRate, 2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read sliding window telemetry from Redis for flag '{FlagKey}'.", flagKey);
            return (0, 0, 0.0);
        }
    }

    private async Task<IConnectionMultiplexer?> GetMultiplexerAsync()
    {
        if (_redis != null && _redis.IsConnected)
        {
            return _redis;
        }

        string? connectionString = _configuration.GetConnectionString("Redis") ?? _configuration["BlastRadius:RedisConnectionString"];
        var options = RedisConnectionHelper.Parse(connectionString);
        if (options == null)
        {
            return null;
        }

        await _connectLock.WaitAsync();
        try
        {
            if (_redis != null && _redis.IsConnected)
            {
                return _redis;
            }

            _redis = await ConnectionMultiplexer.ConnectAsync(options);
            return _redis;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not establish connection to Redis for reading telemetry.");
            return null;
        }
        finally
        {
            _connectLock.Release();
        }
    }
}
