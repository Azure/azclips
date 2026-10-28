// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Reflection;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cli.Tests;

public class OptionalSubscriptionTests
{
    private const string SubscriptionId = "11111111-1111-1111-1111-111111111111";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BindingWithoutProfileDefault_LeavesSubscriptionAbsent(string? subscription)
    {
        var metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata("group list", ClientType.CLI);
        var processor = new ListResourceGroupProcessor();

        CreateBinder().BindArgumentsToProcessor(
            processor, new Dictionary<string, object>(), metadata.Parameters, metadata.ParameterAliases,
            new Profile { SubscriptionId = subscription });

        Assert.True(string.IsNullOrEmpty(processor.SubscriptionId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoginBinding_OnlyUsesExplicitSubscription(bool explicitSubscription)
    {
        var metadata = new Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex()
            .GetCommandMetadata("login", ClientType.CLI);
        var processor = new LoginProcessor();
        var args = new Dictionary<string, object>();
        if (explicitSubscription) args["subscription-id"] = Value(SubscriptionId);

        CreateBinder().BindArgumentsToProcessor(
            processor, args, metadata.Parameters, metadata.ParameterAliases,
            new Profile { SubscriptionId = "22222222-2222-2222-2222-222222222222" });

        Assert.Equal(explicitSubscription ? SubscriptionId : null, processor.SubscriptionId);
    }

    [Fact]
    public async Task BindingResourceIdWithoutProfileSubscription_ResolvesBeforeSending()
    {
        var metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata("Remove-AzResourceGroup", ClientType.PowerShell);
        var processor = new DeleteResourceGroupProcessor_Orchestrator();
        string? path = null;
        processor.Init(new ProcessorContext(
            new FakeHttpPipeline((request, _) =>
            {
                path = request.RequestUri!.AbsolutePath;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }), new FakeEventPublisher(), new FakeAuthHandler(null), new FakeOutputHandler(),
            new Profile { TenantId = "tenant" }));

        CreateBinder().BindArgumentsToProcessor(processor,
            new Dictionary<string, object> { ["Id"] = Value($"/subscriptions/{SubscriptionId}/resourceGroups/rg") },
            metadata.Parameters, metadata.ParameterAliases, new Profile());

        await processor.ProcessAsync();

        Assert.Equal($"/subscriptions/{SubscriptionId}/resourcegroups/rg", path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreflightLookup_RejectsMissingSubscriptionBeforeHttp(bool networkLookup)
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        IProcessor processor = networkLookup
            ? new Microsoft.Azure.Azclips.Extensions.Compute.Processors.NewNetworkInterfaceProcessor_Orchestrator
            {
                Name = "nic", ResourceGroupName = "rg", VirtualNetworkName = "vnet",
                NetworkSecurityGroup = "nsg"
            }
            : new Microsoft.Azure.Azclips.Extensions.Storage.Processors.NewStorageAccountProcessor_Orchestrator
            {
                AccountName = "account", ResourceGroupName = "rg"
            };
        processor.Init(new ProcessorContext(pipeline, new FakeEventPublisher(),
            new FakeAuthHandler(null), new FakeOutputHandler(), new Profile { TenantId = "tenant" }));

        await Assert.ThrowsAsync<MissingRequiredArgumentException>(() => processor.ProcessAsync());

        Assert.Equal(0, pipeline.SendCount);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/providers/Microsoft.Management/managementGroups/group")]
    public async Task BindingExplicitRoleScope_DoesNotRequireSubscription(string scope)
    {
        var metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata("role assignment list", ClientType.CLI);
        var processor = new ListRoleAssignmentProcessor_Orchestrator();
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"value\":[]}")
        });
        processor.Init(new ProcessorContext(pipeline, new FakeEventPublisher(),
            new FakeAuthHandler(null), new FakeOutputHandler(), new Profile { TenantId = "tenant" }));

        CreateBinder().BindArgumentsToProcessor(processor,
            new Dictionary<string, object> { ["scope"] = Value(scope) },
            metadata.Parameters, metadata.ParameterAliases, new Profile());
        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task MissingSubscription_FailsBeforeHttp()
    {
        bool sent = false;
        var processor = new ListResourceGroupProcessor();
        processor.Init(new ProcessorContext(
            new FakeHttpPipeline((_, _) =>
            {
                sent = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }), new FakeEventPublisher(), new FakeAuthHandler(null), new FakeOutputHandler(),
            new Profile { TenantId = "tenant" }));

        var error = await Assert.ThrowsAsync<MissingRequiredArgumentException>(() => processor.ProcessAsync());

        Assert.Contains("subscription", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("profile", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(sent);
    }

    public static IEnumerable<object[]> SubscriptionScopedProcessors()
    {
        Assembly[] assemblies =
        [
            typeof(ListResourceGroupProcessor).Assembly,
            typeof(Microsoft.Azure.Azclips.Extensions.Compute.Processors.GetVMProcessor).Assembly,
            typeof(Microsoft.Azure.Azclips.Extensions.Network.Processors.GetVirtualNetworkProcessor).Assembly,
            typeof(Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors.IdentityProcessorBase).Assembly,
            typeof(Microsoft.Azure.Azclips.Extensions.Storage.Processors.ListStorageAccountProcessor).Assembly
        ];
        return assemblies.SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && !t.ContainsGenericParameters && t.GetProperty("SubscriptionId") is not null
                && t.GetMethod("CreateRequestUri", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is not null)
            .Select(t => new object[] { t });
    }

    [Theory]
    [MemberData(nameof(SubscriptionScopedProcessors))]
    public void SubscriptionScopedUri_RejectsMissingSubscription(Type processorType)
    {
        var processor = Activator.CreateInstance(processorType)!;
        var method = processorType.GetMethod("CreateRequestUri", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(processor, null));

        Assert.IsType<MissingRequiredArgumentException>(error.InnerException);
    }

    private static ArgumentParsingService CreateBinder()
    {
        var logger = new ConsoleLoggingService();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logger), logger), new ProfileService(logger));
    }

    private static List<List<string>> Value(string value) => [[value]];
}
