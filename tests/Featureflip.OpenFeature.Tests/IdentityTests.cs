using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Featureflip.Client;
using Featureflip.OpenFeature;
using Xunit;

namespace Featureflip.OpenFeature.Tests;

/// <summary>
/// Requests made through a provider that owns its client name it after the SDK
/// (#4148, contract in #4138): <c>featureflip-csharp/&lt;v&gt; featureflip-openfeature-dotnet/&lt;v&gt;</c>,
/// so OpenFeature usage can be counted apart from direct SDK use. Asserted on the wire.
/// </summary>
public class IdentityTests
{
    private static string VersionOf(Type type) =>
        type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

    private static async Task<string?> FirstUserAgentAsync(string sdkKey, Func<string, FeatureFlagOptions> options)
    {
        var port = ((Func<int>)(() => { var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }))();
        var baseUrl = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(baseUrl);
        listener.Start();
        var first = listener.GetContextAsync();

        var provider = new FeatureflipProvider(sdkKey, options(baseUrl));
        var completed = await Task.WhenAny(first, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(first, completed);
        var ctx = await first;
        var agent = ctx.Request.UserAgent;

        var body = Encoding.UTF8.GetBytes("{\"environment\":\"e\",\"version\":1,\"flags\":[],\"segments\":[]}");
        ctx.Response.ContentType = "application/json";
        await ctx.Response.OutputStream.WriteAsync(body, 0, body.Length);
        ctx.Response.Close();
        await provider.ShutdownAsync();
        listener.Close();
        return agent;
    }

    [Fact]
    public async Task OwnedClient_SendsTheProviderTokenAfterTheSdkToken()
    {
        var agent = await FirstUserAgentAsync("identity-key-1", url => new FeatureFlagOptions { BaseUrl = url, Streaming = false });

        Assert.Equal(
            $"featureflip-csharp/{VersionOf(typeof(FeatureflipClient))} featureflip-openfeature-dotnet/{VersionOf(typeof(FeatureflipProvider))}",
            agent);
    }

    [Fact]
    public async Task TheCallersOptionsAreLeftUntouched()
    {
        // The wrapper goes on a copy: options the caller reuses for direct SDK
        // clients must not be relabelled.
        FeatureFlagOptions? passed = null;

        await FirstUserAgentAsync("identity-key-2", url => passed = new FeatureFlagOptions { BaseUrl = url, Streaming = false });

        Assert.Null(passed!.WrapperName);
    }

    [Fact]
    public async Task AWrapperTheCallerAlreadySetIsKept()
    {
        // The SDK carries one wrapper token, and the caller's is the more specific.
        var agent = await FirstUserAgentAsync("identity-key-3", url => new FeatureFlagOptions
        {
            BaseUrl = url,
            Streaming = false,
            WrapperName = "acme-platform",
            WrapperVersion = "3.1",
        });

        Assert.EndsWith(" acme-platform/3.1", agent);
    }
}
