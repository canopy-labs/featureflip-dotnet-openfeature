using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Featureflip.Client;

namespace Featureflip.OpenFeature.Tests;

/// <summary>
/// Minimal IFeatureflipClient stub. The provider only ever calls
/// VariationDetail&lt;JsonElement&gt;, so that is the only method with real
/// behavior; the rest throw.
/// </summary>
internal sealed class FakeFeatureflipClient : IFeatureflipClient
{
    private readonly EvaluationDetail<JsonElement>? _detail;
    private readonly Exception? _throw;

    public EvaluationContext? LastContext { get; private set; }
    public Task InitializationTask { get; set; } = Task.CompletedTask;
    public bool Disposed { get; private set; }

    public FakeFeatureflipClient(EvaluationDetail<JsonElement> detail) => _detail = detail;
    public FakeFeatureflipClient(Exception toThrow) => _throw = toThrow;

    public EvaluationDetail<T> VariationDetail<T>(string key, EvaluationContext context, T defaultValue)
    {
        LastContext = context;
        if (_throw is not null) throw _throw;
        return (EvaluationDetail<T>)(object)_detail!;
    }

    public bool IsInitialized => true;
    public Task WaitForInitializationAsync(CancellationToken cancellationToken = default) => InitializationTask;

    public T Variation<T>(string key, EvaluationContext context, T defaultValue) => throw new NotSupportedException();
    public bool BoolVariation(string key, EvaluationContext context, bool defaultValue) => throw new NotSupportedException();
    public string StringVariation(string key, EvaluationContext context, string defaultValue) => throw new NotSupportedException();
    public int IntVariation(string key, EvaluationContext context, int defaultValue) => throw new NotSupportedException();
    public double DoubleVariation(string key, EvaluationContext context, double defaultValue) => throw new NotSupportedException();
    public T JsonVariation<T>(string key, EvaluationContext context, T defaultValue) => throw new NotSupportedException();
    public void Flush() => throw new NotSupportedException();
    public Task FlushAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public void Dispose() { Disposed = true; }

    // Helper: build a JsonElement of any shape for canned details.
    public static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);
}
