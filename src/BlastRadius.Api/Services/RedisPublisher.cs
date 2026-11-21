using System.Text.Json;
using System.Text.Json.Serialization;
using BlastRadius.Sdk.Models;
using StackExchange.Redis;

namespace BlastRadius.Api.Services;

/// <summary>
/// High-performance Redis Pub/Sub publisher broadcasting invalidation and kill events across the fleet.
/// Gracefully degrades if Redis connection is not configured or temporarily partitioned.
/// </summary>
public sealed class RedisPublisher : IRedisPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<RedisPublisher> _logger;
    private IConnectionMultiplexer? _redis;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public RedisPublisher(IConfiguration configuration, ILogger<RedisPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task PublishUpdateAsync(FlagUpdateNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            var multiplexer = await GetMultiplexerAsync();
            if (multiplexer == null || !multiplexer.IsConnected)
            {
                _logger.LogWarning("Redis is not connected. Broadcast notification for flag '{FlagKey}' was skipped.", notification.FlagKey);
                return;
            }

            string channelName = _configuration["BlastRadius:PubSubChannel"] ?? "flag-updates";
            string payload = JsonSerializer.Serialize(notification, JsonOptions);

            var subscriber = multiplexer.GetSubscriber();
            long subscriberCount = await subscriber.PublishAsync(RedisChannel.Literal(channelName), payload);

            _logger.LogInformation(
                "Broadcasted '{Action}' event for flag '{FlagKey}' (Version {Version}) to Redis channel '{Channel}' ({Subscribers} receivers).",
                notification.Action, notification.FlagKey, notification.Version, channelName, subscriberCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast Redis Pub/Sub notification for flag '{FlagKey}'.", notification.FlagKey);
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
            _logger.LogWarning(ex, "Could not establish connection to Redis for publishing.");
            return null;
        }
        finally
        {
            _connectLock.Release();
        }
    }
}
