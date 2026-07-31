using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Featureflip.Client;
using Featureflip.OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;
// Featureflip.Client and OpenFeature.Model both declare an EvaluationContext type;
// this file only needs OpenFeature's, so alias the bare name (CS0104).
using EvaluationContext = OpenFeature.Model.EvaluationContext;

namespace Featureflip.OpenFeature.Tests;

/// <summary>
/// Covers the provider's PROVIDER_CONFIGURATION_CHANGED wiring: when it subscribes to the
/// SDK's FlagsChanged event, when it stops, and how it copes with OpenFeature's
/// single-slot event channel.
/// </summary>
public class FeatureflipProviderEventsTests
{
    private static FakeFeatureflipClient Client() => new(
        new EvaluationDetail<JsonElement>(
            FakeFeatureflipClient.Json(true), EvaluationReason.Fallthrough, null, null, "v"));

    /// <summary>Reads one payload, bounded so a missing event fails instead of hanging.</summary>
    private static async Task<ProviderEventPayload> ReadAsync(FeatureflipProvider provider)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var item = await provider.GetEventChannel().Reader.ReadAsync(cts.Token);
        return Assert.IsType<ProviderEventPayload>(item);
    }

    [Fact]
    public async Task FlagsChanged_AfterInitialize_EmitsProviderConfigurationChanged()
    {
        var client = Client();
        var provider = new FeatureflipProvider(client);
        await provider.InitializeAsync(EvaluationContext.Empty);

        client.RaiseFlagsChanged("alpha", "beta");

        var payload = await ReadAsync(provider);
        Assert.Equal(ProviderEventTypes.ProviderConfigurationChanged, payload.Type);
        Assert.Equal("featureflip-dotnet", payload.ProviderName);
        Assert.Equal(new[] { "alpha", "beta" }, payload.FlagsChanged!.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public async Task FlagsChanged_BeforeInitialize_EmitsNothing()
    {
        // The initial flag load is not a configuration change — OpenFeature signals that
        // with PROVIDER_READY — so nothing may be emitted until initialization finishes.
        var client = Client();
        var provider = new FeatureflipProvider(client);

        client.RaiseFlagsChanged("alpha");

        Assert.False(provider.GetEventChannel().Reader.TryRead(out _));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task InitializeAsync_OnTimeout_StillSubscribes()
    {
        // The init wait can bound out while the client is still loading in the background.
        // Subscribing anyway is safe: the SDK's store establishes its baseline silently on
        // whichever snapshot arrives first, so a load in flight cannot raise FlagsChanged.
        var client = Client();
        client.InitializationTask = new TaskCompletionSource<bool>().Task; // never completes
        var provider = new FeatureflipProvider(client, TimeSpan.FromMilliseconds(50));
        await provider.InitializeAsync(EvaluationContext.Empty);

        client.RaiseFlagsChanged("alpha");

        var payload = await ReadAsync(provider);
        Assert.Equal(ProviderEventTypes.ProviderConfigurationChanged, payload.Type);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_SubscribesOnce()
    {
        // OpenFeature may re-initialize an already-initialized provider; a second
        // subscription would emit every change twice.
        var client = Client();
        var provider = new FeatureflipProvider(client);
        await provider.InitializeAsync(EvaluationContext.Empty);
        await provider.InitializeAsync(EvaluationContext.Empty);

        client.RaiseFlagsChanged("alpha");

        await ReadAsync(provider);
        Assert.False(provider.GetEventChannel().Reader.TryRead(out _));
    }

    [Fact]
    public async Task ShutdownAsync_Unsubscribes_EvenForACallerInjectedClient()
    {
        // The handler writes to this provider's channel, so a caller-owned client that
        // outlives the provider must not keep it attached.
        var client = Client();
        var provider = new FeatureflipProvider(client);
        await provider.InitializeAsync(EvaluationContext.Empty);
        Assert.True(client.HasFlagsChangedSubscriber);

        await provider.ShutdownAsync();

        Assert.False(client.HasFlagsChangedSubscriber);
        client.RaiseFlagsChanged("alpha");
        Assert.False(provider.GetEventChannel().Reader.TryRead(out _));
    }

    [Fact]
    public async Task FlagsChanged_WhenChannelIsFull_StillDeliversEveryUpdate()
    {
        // OpenFeature's channel holds ONE payload, so a second update raised before the
        // event executor drains the first cannot be written inline. It must still be
        // delivered — a consumer reading FlagsChanged would otherwise silently miss keys.
        var client = Client();
        var provider = new FeatureflipProvider(client);
        await provider.InitializeAsync(EvaluationContext.Empty);

        client.RaiseFlagsChanged("first");   // fills the single slot
        client.RaiseFlagsChanged("second");  // slot busy -> completed off the SDK's thread

        var one = await ReadAsync(provider);
        var two = await ReadAsync(provider);

        var delivered = new[] { one, two }.SelectMany(p => p.FlagsChanged!).OrderBy(k => k, StringComparer.Ordinal);
        Assert.Equal(new[] { "first", "second" }, delivered);
    }

    [Fact]
    public async Task FlagsChanged_DoesNotBlockTheRaisingThread_WhenChannelIsFull()
    {
        // The SDK raises FlagsChanged synchronously on its polling/streaming thread. A
        // blocking write would delay flag delivery and can stall the SSE path long enough
        // to trip its idle-connection watchdog, so the full-channel path must return
        // promptly rather than wait for a free slot.
        var client = Client();
        var provider = new FeatureflipProvider(client);
        await provider.InitializeAsync(EvaluationContext.Empty);

        client.RaiseFlagsChanged("first"); // fills the single slot; nothing drains it

        var raise = Task.Run(() => client.RaiseFlagsChanged("second"));
        var finished = await Task.WhenAny(raise, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(raise, finished);
        await raise;
    }
}
