using BlastRadius.Sdk.Evaluation;
using BlastRadius.Sdk.Models;
using BlastRadius.Sdk.Store;
using BlastRadius.Sdk.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlastRadius.Sdk.Client;

/// <summary>
/// Thread-safe, production-ready implementation of <see cref="IBlastRadiusClient"/>.
/// Coordinates zero-latency in-memory store lookups, MurmurHash3 rule evaluations, and async telemetry.
/// </summary>
public sealed class BlastRadiusClient : IBlastRadiusClient
{
    private readonly IInMemoryFlagStore _store;
    private readonly IRuleEvaluator _evaluator;
    private readonly ITelemetryReporter _telemetry;
    private readonly ILogger<BlastRadiusClient> _logger;

    public BlastRadiusClient(
        IInMemoryFlagStore store,
        IRuleEvaluator evaluator,
        ITelemetryReporter telemetry,
        ILogger<BlastRadiusClient>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _logger = logger ?? NullLogger<BlastRadiusClient>.Instance;
    }

    /// <inheritdoc />
    public bool IsEnabled(string flagKey, EvaluationContext? context = null, bool defaultValue = false)
    {
        return Evaluate(flagKey, context, defaultValue).Value;
    }

    /// <inheritdoc />
    public EvaluationResult Evaluate(string flagKey, EvaluationContext? context = null, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
        {
            return EvaluationResult.DefaultFallback("empty", defaultValue, EvaluationReason.DefaultFallback, 0);
        }

        try
        {
            var flagConfig = _store.GetFlag(flagKey);
            var result = _evaluator.Evaluate(flagConfig, context, defaultValue);

            // Record successful evaluation telemetry point if the feature was evaluated as active
            if (result.Value)
            {
                _telemetry.Record(flagKey, isError: false);
            }

            return result;
        }
        catch (Exception ex)
        {
            // STRICT FAIL-SAFE:
            // Ensure no host application flow (e.g. checkout, payment, auth) ever crashes
            // due to a feature flag evaluation issue.
            _logger.LogError(ex, "BlastRadiusClient encountered an unexpected error evaluating '{FlagKey}'. Serving default value ({DefaultVal}).",
                flagKey, defaultValue);

            return EvaluationResult.ErrorFallback(flagKey, defaultValue, 0);
        }
    }

    /// <inheritdoc />
    public void RecordTelemetry(string flagKey, bool isError)
    {
        try
        {
            _telemetry.Record(flagKey, isError);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record telemetry for '{FlagKey}'.", flagKey);
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<FeatureFlagConfig> GetAllFlags()
    {
        return _store.GetAllFlags();
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting BlastRadius SDK client initialization...");
        await _store.InitializeAsync(cancellationToken);
        await _telemetry.StartAsync(cancellationToken);
        _logger.LogInformation("BlastRadius SDK client initialization complete.");
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        await _telemetry.DisposeAsync();
    }

    public void Dispose()
    {
        _store.Dispose();
        _telemetry.Dispose();
    }
}
