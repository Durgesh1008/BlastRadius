using BlastRadius.Sdk.Client;
using BlastRadius.Sdk.Configuration;
using BlastRadius.Sdk.Evaluation;
using BlastRadius.Sdk.Store;
using BlastRadius.Sdk.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlastRadius.Sdk.Extensions;

/// <summary>
/// Extension methods for registering BlastRadius SDK services into the .NET dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the BlastRadius SDK to the specified <see cref="IServiceCollection"/>.
    /// Registers the in-memory store, MurmurHash3 rule evaluator, Redis Pub/Sub sync, and startup initializer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlastRadiusSdk(
        this IServiceCollection services,
        Action<BlastRadiusOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<BlastRadiusOptions>();
        if (configure != null)
        {
            optionsBuilder.Configure(configure);
        }

        services.AddHttpClient();

        services.TryAddSingleton<IRuleEvaluator, RuleEvaluator>();
        services.TryAddSingleton<IInMemoryFlagStore, InMemoryFlagStore>();
        services.TryAddSingleton<ITelemetryReporter, RedisTelemetryReporter>();
        services.TryAddSingleton<IBlastRadiusClient, BlastRadiusClient>();

        // Register startup hosted service to trigger cache bootstrap before handling requests
        services.AddHostedService<BlastRadiusStartupInitializer>();

        return services;
    }
}
