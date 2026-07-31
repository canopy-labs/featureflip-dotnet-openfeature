# Featureflip.OpenFeature

[OpenFeature](https://openfeature.dev/) provider for
[Featureflip](https://featureflip.io), backed by the `Featureflip.Client`
.NET server SDK.

## Installation

```bash
dotnet add package OpenFeature
dotnet add package Featureflip.Client
dotnet add package Featureflip.OpenFeature
```

## Usage

```csharp
using OpenFeature;
using Featureflip.OpenFeature;

await Api.Instance.SetProviderAsync(new FeatureflipProvider("your-server-sdk-key"));

var client = Api.Instance.GetClient();
var enabled = await client.GetBooleanValueAsync("new-checkout", false,
    EvaluationContext.Builder().SetTargetingKey("user-42").Build());
```

The constructor accepts either a server SDK key (with optional
`FeatureFlagOptions`) or an existing `IFeatureflipClient`. When given a key, the
provider obtains a client via the SDK's singleton-by-construction factory, so it
shares one underlying client core with any direct SDK usage of the same key.

**Lifecycle:** the provider disposes its `IFeatureflipClient` on shutdown
(`Api.Instance.ShutdownAsync()` / provider replacement) only when it created
that client itself — i.e. when constructed from an SDK key. If you construct
the provider from your own `IFeatureflipClient` instance, the provider leaves
it running; disposing it (or not) is up to you.

- `targetingKey` maps to Featureflip's `user_id` (used for rollout bucketing);
  an explicit `user_id`/`userId` attribute takes precedence.
- The matched variation key surfaces as `Variant`; `ruleId` / `prerequisiteKey`
  surface in `FlagMetadata`.

### Reasons

| Featureflip outcome | OpenFeature reason | ErrorType |
|---|---|---|
| Targeting rule matched | `TARGETING_MATCH` | — |
| Fallthrough serve | `DEFAULT` | — |
| Flag disabled | `DISABLED` | — |
| Prerequisite not met | `PREREQUISITE_FAILED` | — |
| Flag not found | `ERROR` | `FLAG_NOT_FOUND` |
| Wrong value type | `ERROR` | `TYPE_MISMATCH` |
| Evaluation error | `ERROR` | `GENERAL` |

### Reacting to flag changes

The provider emits `PROVIDER_CONFIGURATION_CHANGED` whenever flag configuration
changes after startup, carrying the affected flag keys:

```csharp
using OpenFeature;
using OpenFeature.Constant;

Api.Instance.AddHandler(ProviderEventTypes.ProviderConfigurationChanged, details =>
{
    Console.WriteLine($"flags changed: {string.Join(", ", details.FlagsChanged ?? [])}");
});
```

A flag is reported when it is created, deleted, redefined, when a segment its
targeting rules reference changes, or when a flag it lists as a prerequisite
changes. The initial flag load does not fire the event — OpenFeature signals that
with `PROVIDER_READY`.

### Limitations

- **No tracking:** the .NET SDK has no custom-event API, so `Track()` is a no-op.
- **Object flags accept objects and arrays only:** a Json flag holding a bare
  primitive resolves as `TYPE_MISMATCH`.

Full documentation: https://featureflip.io/docs/integrations/openfeature/

## License

Apache-2.0
