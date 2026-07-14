using System.Text.Json;
using Featureflip.Client;
using Featureflip.OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Featureflip.OpenFeature.Tests;

public class ResolutionMappingTests
{
    [Theory]
    [InlineData(EvaluationReason.RuleMatch, Reason.TargetingMatch)]
    [InlineData(EvaluationReason.Fallthrough, Reason.Default)]
    [InlineData(EvaluationReason.FlagDisabled, Reason.Disabled)]
    [InlineData(EvaluationReason.PrerequisiteFailed, "PREREQUISITE_FAILED")]
    [InlineData(EvaluationReason.FlagNotFound, Reason.Unknown)]
    [InlineData(EvaluationReason.Error, Reason.Unknown)]
    public void MapReason_MapsSuccessReasons(EvaluationReason reason, string expected)
    {
        Assert.Equal(expected, ResolutionMapping.MapReason(reason));
    }

    [Fact]
    public void JsonElementToValue_Object_BecomesStructure()
    {
        var element = JsonSerializer.SerializeToElement(new { a = 1, b = "x" });

        var value = ResolutionMapping.JsonElementToValue(element);

        Assert.True(value.IsStructure);
        Assert.Equal(1d, value.AsStructure!.GetValue("a").AsDouble);
        Assert.Equal("x", value.AsStructure!.GetValue("b").AsString);
    }

    [Fact]
    public void JsonElementToValue_Array_BecomesList()
    {
        var element = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 });

        var value = ResolutionMapping.JsonElementToValue(element);

        Assert.True(value.IsList);
        Assert.Equal(3, value.AsList!.Count);
    }

    [Fact]
    public void JsonElementToValue_String_BecomesStringValue()
    {
        var element = JsonSerializer.SerializeToElement("hello");

        var value = ResolutionMapping.JsonElementToValue(element);

        Assert.True(value.IsString);
        Assert.Equal("hello", value.AsString);
    }

    [Fact]
    public void JsonElementToValue_Number_BecomesNumberValue()
    {
        var element = JsonSerializer.SerializeToElement(42.5);

        var value = ResolutionMapping.JsonElementToValue(element);

        Assert.True(value.IsNumber);
        Assert.Equal(42.5, value.AsDouble);
    }

    [Fact]
    public void JsonElementToValue_True_BecomesBooleanValue()
    {
        var element = JsonSerializer.SerializeToElement(true);

        var value = ResolutionMapping.JsonElementToValue(element);

        Assert.True(value.IsBoolean);
        Assert.True(value.AsBoolean);
    }

    [Fact]
    public void JsonElementToValue_False_BecomesBooleanValue()
    {
        var element = JsonSerializer.SerializeToElement(false);

        var value = ResolutionMapping.JsonElementToValue(element);

        Assert.True(value.IsBoolean);
        Assert.False(value.AsBoolean);
    }

    [Fact]
    public void JsonElementToValue_Null_BecomesNullValue()
    {
        var element = JsonDocument.Parse("null").RootElement;

        var value = ResolutionMapping.JsonElementToValue(element);

        Assert.True(value.IsNull);
    }

    [Fact]
    public void BuildMetadata_NullWhenNoFields()
    {
        Assert.Null(ResolutionMapping.BuildMetadata(null, null));
    }

    [Fact]
    public void BuildMetadata_CarriesRuleIdAndPrerequisiteKey()
    {
        var meta = ResolutionMapping.BuildMetadata("rule-1", "prereq-flag");

        Assert.NotNull(meta);
        Assert.Equal("rule-1", meta!.GetString("ruleId"));
        Assert.Equal("prereq-flag", meta.GetString("prerequisiteKey"));
    }

    [Fact]
    public void BuildMetadata_RuleIdOnly_HasNoPrerequisiteKey()
    {
        var meta = ResolutionMapping.BuildMetadata("rule-1", null);

        Assert.NotNull(meta);
        Assert.Equal("rule-1", meta!.GetString("ruleId"));
        Assert.Null(meta.GetString("prerequisiteKey"));
    }

    [Fact]
    public void BuildMetadata_PrerequisiteKeyOnly_HasNoRuleId()
    {
        var meta = ResolutionMapping.BuildMetadata(null, "parent");

        Assert.NotNull(meta);
        Assert.Equal("parent", meta!.GetString("prerequisiteKey"));
        Assert.Null(meta.GetString("ruleId"));
    }
}
