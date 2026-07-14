using Featureflip.OpenFeature;
using OpenFeature.Model;
using Xunit;

namespace Featureflip.OpenFeature.Tests;

public class ContextMappingTests
{
    [Fact]
    public void TargetingKey_MapsToUserId()
    {
        var ctx = EvaluationContext.Builder().SetTargetingKey("user-42").Build();

        var ff = ContextMapping.ToFeatureflipContext(ctx);

        Assert.Equal("user-42", ff.UserId);
    }

    [Fact]
    public void ExplicitUserIdAttribute_WinsOverTargetingKey()
    {
        var ctx = EvaluationContext.Builder()
            .SetTargetingKey("targeting-key")
            .Set("user_id", "explicit-id")
            .Build();

        var ff = ContextMapping.ToFeatureflipContext(ctx);

        Assert.Equal("explicit-id", ff.UserId);
    }

    [Fact]
    public void UserIdCamelCaseAttribute_WinsOverTargetingKey()
    {
        var ctx = EvaluationContext.Builder()
            .SetTargetingKey("targeting-key")
            .Set("userId", "camel-id")
            .Build();

        var ff = ContextMapping.ToFeatureflipContext(ctx);

        Assert.Equal("camel-id", ff.UserId);
    }

    [Fact]
    public void OtherAttributes_ArePassedThrough()
    {
        var ctx = EvaluationContext.Builder()
            .SetTargetingKey("u")
            .Set("country", "US")
            .Set("age", 30)
            .Build();

        var ff = ContextMapping.ToFeatureflipContext(ctx);

        Assert.Equal("US", ff.GetAttribute("country"));
        Assert.Equal(30d, ff.GetAttribute("age"));
    }

    [Fact]
    public void NullContext_ReturnsEmptyContext()
    {
        var ff = ContextMapping.ToFeatureflipContext(null);
        Assert.Null(ff.UserId);
    }

    [Fact]
    public void TargetingKey_IsNotLeakedAsCustomAttribute()
    {
        var ctx = EvaluationContext.Builder().SetTargetingKey("user-42").Build();

        var ff = ContextMapping.ToFeatureflipContext(ctx);

        Assert.Null(ff.GetAttribute("targetingKey"));
        Assert.Equal("user-42", ff.UserId);
    }

    [Fact]
    public void UserIdAttribute_WinsOverUserIdCamelCaseAndTargetingKey()
    {
        var ctx = EvaluationContext.Builder()
            .SetTargetingKey("targeting-key")
            .Set("user_id", "snake-id")
            .Set("userId", "camel-id")
            .Build();

        var ff = ContextMapping.ToFeatureflipContext(ctx);

        Assert.Equal("snake-id", ff.UserId);
    }

    [Fact]
    public void NumericUserId_IsNotOverriddenByTargetingKey()
    {
        var ctx = EvaluationContext.Builder()
            .SetTargetingKey("tk")
            .Set("user_id", 42)
            .Build();

        var ff = ContextMapping.ToFeatureflipContext(ctx);

        Assert.Null(ff.UserId);                       // targetingKey did NOT take over
        Assert.Equal(42d, ff.GetAttribute("user_id")); // numeric user_id preserved for bucketing
    }

    [Fact]
    public void NumericUserIdCamelCase_IsNotOverriddenByTargetingKey()
    {
        var ctx = EvaluationContext.Builder()
            .SetTargetingKey("tk")
            .Set("userId", 42)
            .Build();

        var ff = ContextMapping.ToFeatureflipContext(ctx);

        Assert.Null(ff.UserId);
        Assert.Equal(42d, ff.GetAttribute("userId"));
    }
}
