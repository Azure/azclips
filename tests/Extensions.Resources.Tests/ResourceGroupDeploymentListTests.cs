// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Tests.Common.Stubs;
using static Extensions.Resources.Tests.DeploymentProcessorTestKit;

namespace Extensions.Resources.Tests;

/// <summary>
/// Offline tests for listing resource-group deployments (<c>az deployment group list</c>).
/// </summary>
public class ResourceGroupDeploymentListTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

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
}
