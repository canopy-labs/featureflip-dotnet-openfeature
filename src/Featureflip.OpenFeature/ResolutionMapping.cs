using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Featureflip.Client;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Featureflip.OpenFeature;

/// <summary>Maps Featureflip evaluation results onto OpenFeature shapes.</summary>
internal static class ResolutionMapping
{
    /// <summary>Maps a successful Featureflip reason to an OpenFeature reason string.</summary>
    public static string MapReason(EvaluationReason reason) => reason switch
    {
        EvaluationReason.RuleMatch => Reason.TargetingMatch,
        EvaluationReason.Fallthrough => Reason.Default,
        EvaluationReason.FlagDisabled => Reason.Disabled,
        // No standard OpenFeature reason models an unmet prerequisite.
        EvaluationReason.PrerequisiteFailed => "PREREQUISITE_FAILED",
        _ => Reason.Unknown,
    };

    public static Value JsonElementToValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => new Value(BuildStructure(element)),
        JsonValueKind.Array => new Value(element.EnumerateArray().Select(JsonElementToValue).ToList()),
        JsonValueKind.String => new Value(element.GetString()!),
        JsonValueKind.Number => new Value(element.GetDouble()),
        JsonValueKind.True => new Value(true),
        JsonValueKind.False => new Value(false),
        _ => new Value(),
    };

    public static ImmutableMetadata? BuildMetadata(string? ruleId, string? prerequisiteKey)
    {
        if (ruleId is null && prerequisiteKey is null)
        {
            return null;
        }

        var metadata = new Dictionary<string, object>();
        if (ruleId is not null) metadata["ruleId"] = ruleId;
        if (prerequisiteKey is not null) metadata["prerequisiteKey"] = prerequisiteKey;
        return new ImmutableMetadata(metadata);
    }

    private static Structure BuildStructure(JsonElement obj)
    {
        var builder = Structure.Builder();
        foreach (var property in obj.EnumerateObject())
        {
            builder.Set(property.Name, JsonElementToValue(property.Value));
        }
        return builder.Build();
    }
}
