using System.Linq;
using OpenFeature.Model;
using FfContext = Featureflip.Client.EvaluationContext;

namespace Featureflip.OpenFeature;

/// <summary>Maps an OpenFeature evaluation context onto a Featureflip context.</summary>
internal static class ContextMapping
{
    public static FfContext ToFeatureflipContext(EvaluationContext? context)
    {
        var ff = new FfContext();
        if (context is null)
        {
            return ff;
        }

        var attributes = context.AsDictionary();
        foreach (var pair in attributes)
        {
            if (pair.Key == "targetingKey") continue; // OpenFeature reserved key; mirrors the Node provider's exclusion (packages/openfeature-node/src/mapping.ts)
            ff.Set(pair.Key, Unwrap(pair.Value));
        }

        // targetingKey -> UserId, unless an explicit user_id/userId attribute wins.
        string? userId = null;
        if (attributes.TryGetValue("user_id", out var u1) && u1.IsString)
        {
            userId = u1.AsString;
        }
        else if (attributes.TryGetValue("userId", out var u2) && u2.IsString)
        {
            userId = u2.AsString;
        }
        else if (!attributes.ContainsKey("user_id") && !attributes.ContainsKey("userId")
                 && !string.IsNullOrEmpty(context.TargetingKey))
        {
            userId = context.TargetingKey;
        }

        if (userId is not null)
        {
            ff.UserId = userId;
        }

        return ff;
    }

    private static object Unwrap(Value value)
    {
        if (value.IsBoolean) return value.AsBoolean!.Value;
        if (value.IsString) return value.AsString!;
        if (value.IsNumber) return value.AsDouble!.Value;
        if (value.IsDateTime) return value.AsDateTime!.Value;
        if (value.IsList) return value.AsList!.Select(Unwrap).ToList();
        if (value.IsStructure)
        {
            return value.AsStructure!.AsDictionary()
                .ToDictionary(kv => kv.Key, kv => Unwrap(kv.Value));
        }
        return value.AsObject!;
    }
}
