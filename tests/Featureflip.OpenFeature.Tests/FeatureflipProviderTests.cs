using System.Text.Json;
using Featureflip.Client;
using Featureflip.OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;
// Featureflip.Client and OpenFeature.Model both declare an EvaluationContext type;
// this file only ever needs OpenFeature's (the provider's public surface), so alias
// the bare name to disambiguate (CS0104) rather than fully-qualifying every call site.
using EvaluationContext = OpenFeature.Model.EvaluationContext;

namespace Featureflip.OpenFeature.Tests;

public class FeatureflipProviderTests
{
    private static EvaluationDetail<JsonElement> Detail(
        object value,
        EvaluationReason reason,
        string? variationKey = null,
        string? ruleId = null,
        string? prerequisiteKey = null)
        => new(FakeFeatureflipClient.Json(value), reason, ruleId, errorMessage: null, variationKey, prerequisiteKey);

    [Fact]
    public async Task Metadata_IsFeatureflipDotnet()
    {
        var provider = new FeatureflipProvider(new FakeFeatureflipClient(Detail(true, EvaluationReason.Fallthrough)));
        Assert.Equal("featureflip-dotnet", provider.GetMetadata().Name);
    }

    [Fact]
    public async Task ResolveBoolean_Fallthrough_MapsToDefault()
    {
        var client = new FakeFeatureflipClient(Detail(true, EvaluationReason.Fallthrough, variationKey: "true"));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveBooleanValueAsync("flag", false, EvaluationContext.Empty);

        Assert.True(result.Value);
        Assert.Equal(Reason.Default, result.Reason);
        Assert.Equal("true", result.Variant);
        Assert.Equal(ErrorType.None, result.ErrorType);
    }

    [Fact]
    public async Task ResolveBoolean_RuleMatch_CarriesRuleIdMetadata()
    {
        var client = new FakeFeatureflipClient(
            Detail(false, EvaluationReason.RuleMatch, variationKey: "false", ruleId: "rule-1"));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveBooleanValueAsync("flag", true, EvaluationContext.Empty);

        Assert.False(result.Value);
        Assert.Equal(Reason.TargetingMatch, result.Reason);
        Assert.Equal("rule-1", result.FlagMetadata!.GetString("ruleId"));
    }

    [Fact]
    public async Task ResolvePrerequisiteFailed_MapsToCustomReasonWithMetadata()
    {
        var client = new FakeFeatureflipClient(
            Detail(false, EvaluationReason.PrerequisiteFailed, variationKey: "false", prerequisiteKey: "parent"));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveBooleanValueAsync("flag", true, EvaluationContext.Empty);

        Assert.Equal("PREREQUISITE_FAILED", result.Reason);
        Assert.Equal("parent", result.FlagMetadata!.GetString("prerequisiteKey"));
    }

    [Fact]
    public async Task FlagNotFound_ReturnsDefaultWithFlagNotFoundError()
    {
        var client = new FakeFeatureflipClient(
            new EvaluationDetail<JsonElement>(default, EvaluationReason.FlagNotFound, null, "missing"));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveBooleanValueAsync("flag", true, EvaluationContext.Empty);

        Assert.True(result.Value); // caller default
        Assert.Equal(Reason.Error, result.Reason);
        Assert.Equal(ErrorType.FlagNotFound, result.ErrorType);
    }

    [Fact]
    public async Task ErrorReason_ReturnsDefaultWithGeneralError_NoThrow()
    {
        var client = new FakeFeatureflipClient(
            new EvaluationDetail<JsonElement>(default, EvaluationReason.Error, null, "boom"));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveStringValueAsync("flag", "fallback", EvaluationContext.Empty);

        Assert.Equal("fallback", result.Value);
        Assert.Equal(ErrorType.General, result.ErrorType);
    }

    [Fact]
    public async Task WrongType_ReturnsDefaultWithTypeMismatch()
    {
        // Flag resolves to a string, but caller requests a boolean.
        var client = new FakeFeatureflipClient(Detail("hello", EvaluationReason.Fallthrough));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveBooleanValueAsync("flag", false, EvaluationContext.Empty);

        Assert.False(result.Value);
        Assert.Equal(ErrorType.TypeMismatch, result.ErrorType);
    }

    [Fact]
    public async Task IntegerRequest_OnNonIntegralNumber_IsTypeMismatch()
    {
        var client = new FakeFeatureflipClient(Detail(5.5, EvaluationReason.Fallthrough));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveIntegerValueAsync("flag", 0, EvaluationContext.Empty);

        Assert.Equal(0, result.Value);
        Assert.Equal(ErrorType.TypeMismatch, result.ErrorType);
    }

    [Fact]
    public async Task DoubleRequest_OnNumber_Resolves()
    {
        var client = new FakeFeatureflipClient(Detail(3.14, EvaluationReason.Fallthrough));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveDoubleValueAsync("flag", 0d, EvaluationContext.Empty);

        Assert.Equal(3.14, result.Value);
        Assert.Equal(ErrorType.None, result.ErrorType);
    }

    [Fact]
    public async Task StructureRequest_OnObject_Resolves()
    {
        var client = new FakeFeatureflipClient(Detail(new { a = 1 }, EvaluationReason.Fallthrough));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveStructureValueAsync("flag", new Value(), EvaluationContext.Empty);

        Assert.True(result.Value.IsStructure);
        Assert.Equal(ErrorType.None, result.ErrorType);
    }

    [Fact]
    public async Task StructureRequest_OnBarePrimitive_IsTypeMismatch()
    {
        var client = new FakeFeatureflipClient(Detail("not-an-object", EvaluationReason.Fallthrough));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveStructureValueAsync("flag", new Value(), EvaluationContext.Empty);

        Assert.Equal(ErrorType.TypeMismatch, result.ErrorType);
    }

    [Fact]
    public async Task ThrownException_ReturnsDefaultWithGeneralError()
    {
        var client = new FakeFeatureflipClient(new InvalidOperationException("kaboom"));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveBooleanValueAsync("flag", true, EvaluationContext.Empty);

        Assert.True(result.Value);
        Assert.Equal(ErrorType.General, result.ErrorType);
    }

    [Fact]
    public async Task ResolveBoolean_MapsTargetingKeyToUserId_AndReachesTheSdk()
    {
        var client = new FakeFeatureflipClient(Detail(true, EvaluationReason.Fallthrough));
        var provider = new FeatureflipProvider(client);

        var context = EvaluationContext.Builder()
            .SetTargetingKey("u-123")
            .Set("plan", "enterprise")
            .Build();

        await provider.ResolveBooleanValueAsync("flag", false, context);

        Assert.NotNull(client.LastContext);
        Assert.Equal("u-123", client.LastContext!.UserId);
    }

    [Fact]
    public async Task InitializeAsync_ReturnsOnTimeout_WhenClientInitNeverCompletes()
    {
        var client = new FakeFeatureflipClient(Detail(true, EvaluationReason.Fallthrough))
        {
            InitializationTask = new TaskCompletionSource<bool>().Task, // never completes
        };
        var provider = new FeatureflipProvider(client, TimeSpan.FromMilliseconds(50));

        var init = provider.InitializeAsync(EvaluationContext.Empty);
        var finished = await Task.WhenAny(init, Task.Delay(5000));

        Assert.Same(init, finished); // completed via the 50ms bound, did not hang
        await init;
    }

    [Fact]
    public async Task InitializeAsync_PropagatesCancellation()
    {
        var client = new FakeFeatureflipClient(Detail(true, EvaluationReason.Fallthrough))
        {
            InitializationTask = new TaskCompletionSource<bool>().Task, // never completes
        };
        // Long init bound so the cancellation, not the timeout, ends the wait.
        var provider = new FeatureflipProvider(client, TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.InitializeAsync(EvaluationContext.Empty, cts.Token));
    }

    [Theory]
    [InlineData("1.0", 1)]
    [InlineData("1e2", 100)]
    public async Task ResolveInteger_AcceptsWholeNumberInDecimalOrExponentForm(string json, int expected)
    {
        var element = System.Text.Json.JsonDocument.Parse(json).RootElement.Clone();
        var client = new FakeFeatureflipClient(
            new EvaluationDetail<JsonElement>(element, EvaluationReason.Fallthrough, null, null, "v"));
        var provider = new FeatureflipProvider(client);

        var result = await provider.ResolveIntegerValueAsync("flag", 0, EvaluationContext.Empty);

        Assert.Equal(expected, result.Value);
        Assert.Equal(ErrorType.None, result.ErrorType);
    }

    [Fact]
    public async Task ShutdownAsync_DoesNotDisposeCallerInjectedClient()
    {
        var client = new FakeFeatureflipClient(Detail(true, EvaluationReason.Fallthrough));
        var provider = new FeatureflipProvider(client);

        await provider.ShutdownAsync();

        Assert.False(client.Disposed);
    }
}
