// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cli.Tests;

/// <summary>
/// Offline tests for resource-group deployment commands (<c>az deployment group ...</c>).
/// </summary>
public class ResourceGroupDeploymentCliTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    [Fact]
    public void CliBinding_List_BindsFilter()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("deployment group list", ClientType.CLI);
        var processor = (ListResourceGroupDeploymentProcessor)metadata.GetInstance();

        CreateBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>
            {
                ["resource-group"] = Value("test-rg"),
                ["filter"] = Value("provisioningState eq 'Failed'"),
            },
            metadata,
            new Profile { SubscriptionId = TestSubscriptionId });

        Assert.Equal("provisioningState eq 'Failed'", processor.Filter);
    }

    [Fact]
    public async Task List_WithFilter_SendsODataFilter()
    {
        Uri? sentUri = await CaptureListUri(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.Filter = "provisioningState eq 'Failed'";
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups/test-rg/providers/Microsoft.Resources/deployments?api-version=2025-04-01&$filter=provisioningState%20eq%20%27Failed%27",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task List_WithoutFilter_OmitsODataFilter()
    {
        Uri? sentUri = await CaptureListUri(p =>
        {
            p.SubscriptionId = TestSubscriptionId;
            p.ResourceGroupName = "test-rg";
        });

        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups/test-rg/providers/Microsoft.Resources/deployments?api-version=2025-04-01",
            sentUri!.AbsoluteUri);
    }

    private static async Task<Uri?> CaptureListUri(Action<ListResourceGroupDeploymentProcessor> configure)
    {
        Uri? sentUri = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return JsonResponse("""{"value":[]}""");
        });
        ListResourceGroupDeploymentProcessor processor = CreateProcessor(new ListResourceGroupDeploymentProcessor(), pipeline, new FakeOutputHandler());
        configure(processor);

        await processor.ProcessAsync();
        return sentUri;
    }

    private static ArgumentParsingService CreateBinder()
    {
        var logging = new ConsoleLoggingService();
        return new ArgumentParsingService(
            new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
    }

    private static List<List<string>> Value(string value) => [new() { value }];

    internal static T CreateProcessor<T>(T processor, FakeHttpPipeline pipeline, FakeOutputHandler output)
        where T : IProcessor
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        return processor;
    }

    internal static HttpResponseMessage JsonResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
}
