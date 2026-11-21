using BlastRadius.Sdk.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlastRadius.Sdk.Extensions;

/// <summary>
/// Hosted service that pre-warms the BlastRadius flag store and starts Redis Pub/Sub listeners
/// during application startup, prior to serving incoming HTTP traffic.
/// </summary>
public sealed class BlastRadiusStartupInitializer : IHostedService
{
    private readonly IBlastRadiusClient _client;
    private readonly ILogger<BlastRadiusStartupInitializer> _logger;

    public BlastRadiusStartupInitializer(
        IBlastRadiusClient client,
        ILogger<BlastRadiusStartupInitializer> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BlastRadiusStartupInitializer: Pre-warming cache and starting Pub/Sub subscriptions...");
        try
        {
            await _client.InitializeAsync(cancellationToken);
            _logger.LogInformation("BlastRadiusStartupInitializer: Cache successfully initialized.");
        }
        catch (Exception ex)
        {
            // Defensive Startup: Log and continue so the host application does not fail to boot
            _logger.LogWarning(ex, "BlastRadiusStartupInitializer: Failed to pre-warm cache during startup. SDK will operate with local defaults.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
