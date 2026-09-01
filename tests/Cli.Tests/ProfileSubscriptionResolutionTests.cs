using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Bootstrap behavior: Resource processors resolve subscriptions at point of use, while Compute
/// commands declare SubscriptionId as optional so argument binding can default it from a profile.
/// </summary>
public class ProfileSubscriptionResolutionTests
{
    private sealed class TestResourceProcessor : ResourcesProcessorBase<object, object>
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

    [Theory]
    [InlineData(ClientType.CLI, "group create", "subscription-id")]
    [InlineData(ClientType.PowerShell, "New-AzResourceGroup", "SubscriptionId")]
    public void CreateResourceGroup_SubscriptionId_ShouldBeOptional(ClientType clientType, string command, string parameterKey)
    {
        var metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata(command, clientType);

        Assert.False(
            metadata.Parameters[parameterKey].IsRequired,
            $"SubscriptionId should be optional for '{command}' so it defaults to the current subscription.");
    }

    [Theory]
    [InlineData(ClientType.CLI, "vm create", "subscription-id")]
    [InlineData(ClientType.CLI, "vm show", "subscription-id")]
    [InlineData(ClientType.CLI, "vm list", "subscription-id")]
    [InlineData(ClientType.CLI, "vm delete", "subscription-id")]
    [InlineData(ClientType.CLI, "disk list", "subscription-id")]
    [InlineData(ClientType.PowerShell, "New-AzVM", "SubscriptionId")]
    [InlineData(ClientType.PowerShell, "Get-AzVM_Get", "SubscriptionId")]
    [InlineData(ClientType.PowerShell, "Get-AzVM_List", "SubscriptionId")]
    [InlineData(ClientType.PowerShell, "Remove-AzVM", "SubscriptionId")]
    public void Compute_SubscriptionId_ShouldBeOptional(ClientType clientType, string command, string parameterKey)
    {
        var metadata = new Microsoft.Azure.Azclips.Extensions.Compute.ExtensionIndex()
            .GetCommandMetadata(command, clientType);

        Assert.False(
            metadata.Parameters[parameterKey].IsRequired,
            $"SubscriptionId should be optional for '{command}' so argument binding can default it from the active profile.");
    }
}
