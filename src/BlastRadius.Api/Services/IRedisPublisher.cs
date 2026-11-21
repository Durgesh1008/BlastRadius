using BlastRadius.Sdk.Models;

namespace BlastRadius.Api.Services;

/// <summary>
/// Publishes real-time feature flag mutations and kill signals to Redis Pub/Sub.
/// </summary>
public interface IRedisPublisher
{
    /// <summary>
    /// Broadcasts a flag update notification to the fleet of connected SDK instances.
    /// </summary>
    Task PublishUpdateAsync(FlagUpdateNotification notification, CancellationToken cancellationToken = default);
}
