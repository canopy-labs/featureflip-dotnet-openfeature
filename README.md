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

### Limitations

- **No tracking:** the .NET SDK has no custom-event API, so `Track()` is a no-op.
- **Object flags accept objects and arrays only:** a Json flag holding a bare
  primitive resolves as `TYPE_MISMATCH`.
- **No change events:** evaluations always reflect the latest streamed snapshot,
  but OpenFeature change handlers do not fire on flag changes.

Full documentation: https://featureflip.io/docs/integrations/openfeature/

## License

Apache-2.0
