using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Featureflip.Client;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
// Featureflip.Client and OpenFeature.Model both declare an EvaluationContext type.
// The FeatureProvider overrides below must bind to OpenFeature's; alias the bare
// name to it so it wins over the Featureflip.Client one brought in for
// IFeatureflipClient/EvaluationDetail/EvaluationReason (CS0104 otherwise).
using EvaluationContext = OpenFeature.Model.EvaluationContext;

namespace Featureflip.OpenFeature;

/// <summary>
/// OpenFeature provider backed by the Featureflip .NET server SDK.
/// Accepts either an existing <see cref="IFeatureflipClient"/> or an SDK key +
/// options (the provider then obtains a client via the singleton factory).
/// </summary>
public sealed class FeatureflipProvider : FeatureProvider
{
    private readonly IFeatureflipClient _client;
    private readonly bool _ownsClient;
    private readonly TimeSpan _initTimeout;

    // Guards _subscribed so concurrent Initialize/Shutdown can't double-subscribe or
    // leave a handler attached after shutdown.
    private readonly object _subscriptionLock = new();
    private bool _subscribed;

    public FeatureflipProvider(IFeatureflipClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = false;
        _initTimeout = new FeatureFlagOptions().InitTimeout;
    }

    public FeatureflipProvider(string sdkKey, FeatureFlagOptions? options = null)
    {
        var resolvedOptions = options ?? new FeatureFlagOptions();
        _client = FeatureflipClient.Get(sdkKey, resolvedOptions);
        _ownsClient = true;
        _initTimeout = resolvedOptions.InitTimeout;
    }

    // Test-only: inject a client and an explicit init-wait bound.
    internal FeatureflipProvider(IFeatureflipClient client, TimeSpan initTimeout)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = false;
        _initTimeout = initTimeout;
    }

    public override Metadata GetMetadata() => new("featureflip-dotnet");

    public override async Task InitializeAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        // Block until the client's initial flag load finishes, but bound the wait.
        // Under the SDK's default WaitForInitialization=false, a failed initial load
        // leaves the client's init task pending until a later background retry, so an
        // unbounded await would hang OpenFeature's SetProviderAsync. On timeout we
        // return ready: the client keeps loading in the background and evaluations
        // fail safe to the caller default until flags arrive.
        var initTask = _client.WaitForInitializationAsync(cancellationToken);
        var completed = await Task.WhenAny(initTask, Task.Delay(_initTimeout, cancellationToken)).ConfigureAwait(false);
        if (completed == initTask)
        {
            await initTask.ConfigureAwait(false);
        }
        else
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        // Subscribe only after the init wait, so the initial flag load is never reported
        // as a configuration change — OpenFeature signals that with PROVIDER_READY.
        // Subscribing is still correct on the timeout path above: the SDK's store
        // establishes its baseline silently on the first snapshot whenever that lands,
        // so a load still in flight cannot raise FlagsChanged either.
        Subscribe();
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        // Unsubscribe regardless of ownership — the handler writes to this provider's
        // event channel, so leaving it attached to a caller-owned client would keep
        // publishing after OpenFeature has torn the provider down.
        Unsubscribe();

        // Dispose only a client the provider created (the sdkKey constructor); a
        // caller-injected IFeatureflipClient is owned by the caller.
        if (_ownsClient)
        {
            _client.Dispose();
        }
        return Task.CompletedTask;
    }

    private void Subscribe()
    {
        lock (_subscriptionLock)
        {
            // Guarded: OpenFeature may re-initialize an already-initialized provider.
            if (_subscribed)
            {
                return;
            }
            _client.FlagsChanged += OnFlagsChanged;
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        lock (_subscriptionLock)
        {
            if (!_subscribed)
            {
                return;
            }
            _client.FlagsChanged -= OnFlagsChanged;
            _subscribed = false;
        }
    }

    private void OnFlagsChanged(object? sender, FlagsChangedEventArgs e)
    {
        var payload = new ProviderEventPayload
        {
            Type = ProviderEventTypes.ProviderConfigurationChanged,
            ProviderName = GetMetadata().Name,
            FlagsChanged = e.ChangedKeys.ToList(),
        };

        // Never block in this handler. The SDK raises FlagsChanged synchronously on its
        // polling/streaming thread, so a slow handler delays flag delivery and, on the
        // SSE path, can stall long enough to trip the idle-connection watchdog into a
        // reconnect.
        //
        // OpenFeature's event channel is bounded at a single slot
        // (Channel.CreateBounded<object>(1), FullMode.Wait), so TryWrite fails whenever a
        // previous payload is still waiting for the event executor to drain it. Dropping
        // on a full channel is not an option — a consumer reading payload.FlagsChanged
        // would silently miss those keys — so finish the write on the thread pool, where
        // waiting for a slot costs nothing. Concurrent fallback writes may be delivered
        // out of order; that is fine here, because each payload is an independent "these
        // keys changed" signal rather than a delta that has to be applied in sequence.
        if (!GetEventChannel().Writer.TryWrite(payload))
        {
            _ = WriteWhenSlotFreeAsync(payload);
        }
    }

    private async Task WriteWhenSlotFreeAsync(ProviderEventPayload payload)
    {
        try
        {
            await GetEventChannel().Writer.WriteAsync(payload).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The channel was completed while this write was queued (provider shutdown),
            // or the write otherwise cannot land. Swallow rather than let a
            // fire-and-forget task fault: an unobserved faulted Task raises
            // TaskScheduler.UnobservedTaskException when it is finalized, which would
            // surface far from here and in an unrelated part of the host.
        }
    }

    public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(
        string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        => Resolve(flagKey, defaultValue, context, static e =>
            e.ValueKind is JsonValueKind.True or JsonValueKind.False ? (true, e.GetBoolean()) : (false, false));

    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(
        string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        => Resolve(flagKey, defaultValue, context, static e =>
            e.ValueKind == JsonValueKind.String ? (true, e.GetString()!) : (false, string.Empty));

    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(
        string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        => Resolve(flagKey, defaultValue, context, static e =>
            TryGetInt32(e, out var i) ? (true, i) : (false, 0));

    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(
        string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        => Resolve(flagKey, defaultValue, context, static e =>
            e.ValueKind == JsonValueKind.Number ? (true, e.GetDouble()) : (false, 0d));

    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(
        string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        => Resolve(flagKey, defaultValue, context, static e =>
            e.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                ? (true, ResolutionMapping.JsonElementToValue(e))
                : (false, new Value()));

    private Task<ResolutionDetails<T>> Resolve<T>(
        string flagKey,
        T defaultValue,
        EvaluationContext? context,
        Func<JsonElement, (bool ok, T value)> convert)
    {
        EvaluationDetail<JsonElement> detail;
        try
        {
            var ffContext = ContextMapping.ToFeatureflipContext(context);
            detail = _client.VariationDetail<JsonElement>(flagKey, ffContext, default);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ResolutionDetails<T>(
                flagKey, defaultValue, ErrorType.General, Reason.Error, errorMessage: ex.Message));
        }

        // Error reasons: substitute the caller's correctly-typed default. The
        // SDK's error detail can carry a wrong-typed off-variation value, so skip
        // the type guard here.
        if (detail.Reason == EvaluationReason.FlagNotFound)
        {
            return Task.FromResult(new ResolutionDetails<T>(
                flagKey, defaultValue, ErrorType.FlagNotFound, Reason.Error, errorMessage: detail.ErrorMessage));
        }
        if (detail.Reason == EvaluationReason.Error)
        {
            return Task.FromResult(new ResolutionDetails<T>(
                flagKey, defaultValue, ErrorType.General, Reason.Error, errorMessage: detail.ErrorMessage));
        }

        var (ok, value) = convert(detail.Value);
        if (!ok)
        {
            return Task.FromResult(new ResolutionDetails<T>(
                flagKey, defaultValue, ErrorType.TypeMismatch, Reason.Error,
                errorMessage: $"Flag '{flagKey}' did not resolve to the expected type"));
        }

        return Task.FromResult(new ResolutionDetails<T>(
            flagKey,
            value,
            ErrorType.None,
            ResolutionMapping.MapReason(detail.Reason),
            variant: detail.VariationKey,
            flagMetadata: ResolutionMapping.BuildMetadata(detail.RuleId, detail.PrerequisiteKey)));
    }

    private static bool TryGetInt32(JsonElement element, out int value)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            value = 0;
            return false;
        }
        if (element.TryGetInt32(out value))
        {
            return true;
        }
        // Accept whole-number values written in decimal/exponent JSON form (e.g. 1.0,
        // 1e2) — the engine may serialize whole-number floats that way.
        if (element.TryGetDouble(out var d) && d >= int.MinValue && d <= int.MaxValue && Math.Floor(d) == d)
        {
            value = (int)d;
            return true;
        }
        value = 0;
        return false;
    }
}
