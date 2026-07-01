using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Bootstrap behavior: Resource and Compute processors must fail at point of use with a clear
/// message when no profile is available, and resolve the subscription from the profile otherwise.
/// </summary>
public class ProfileSubscriptionResolutionTests
{
    private sealed class TestResourceProcessor : ResourcesProcessorBase<object>
    {
        public void SetProfile(Profile? profile) => ResolvedProfile = profile;
        public void Resolve() => ResolveSubscriptionId();
    }

    private sealed class TestComputeProcessor : ComputeProcessorBase<object>
    {
        public void SetProfile(Profile? profile) => ResolvedProfile = profile;
        public void Resolve() => ResolveSubscriptionId();
    }

    private static Profile ProfileWithSub(string sub) => new()
    {
        Name = "p",
        TenantId = "t",
        SubscriptionId = sub,
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Browser,
    };

    [Fact]
    public void Resources_NullProfile_ShouldThrowClearError()
    {
        var processor = new TestResourceProcessor();
        processor.SetProfile(null);

        var ex = Assert.Throws<InvalidOperationException>(processor.Resolve);
        Assert.Contains("profile", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resources_WithProfile_ShouldResolveSubscriptionFromProfile()
    {
        var processor = new TestResourceProcessor();
        processor.SetProfile(ProfileWithSub("sub-123"));

        processor.Resolve();

        Assert.Equal("sub-123", processor.SubscriptionId);
    }

    [Fact]
    public void Resources_ExplicitSubscription_ShouldNotRequireProfile()
    {
        var processor = new TestResourceProcessor { SubscriptionId = "explicit-sub" };
        processor.SetProfile(null);

        processor.Resolve();

        Assert.Equal("explicit-sub", processor.SubscriptionId);
    }

    [Fact]
    public void Compute_NullProfile_ShouldThrowClearError()
    {
        var processor = new TestComputeProcessor();
        processor.SetProfile(null);

        Assert.Throws<InvalidOperationException>(processor.Resolve);
    }

    [Fact]
    public void Compute_WithProfile_ShouldResolveSubscriptionFromProfile()
    {
        var processor = new TestComputeProcessor();
        processor.SetProfile(ProfileWithSub("sub-456"));

        processor.Resolve();

        Assert.Equal("sub-456", processor.SubscriptionId);
    }
}
