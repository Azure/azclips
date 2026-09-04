// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// VirtualMachines_Start. The swagger operation declares neither a request body nor a success
/// response schema (200/202 are empty), so both type arguments are <see cref="object"/> and the
/// command writes no output on success.
/// </summary>
public class StartVMProcessor : ComputeProcessorBase<object, object>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    /// <summary>
    /// One or more complete virtual machine resource IDs. Supplies the subscription, resource group
    /// and name, so it stands in for <c>--name</c>/<c>--resource-group</c>; when both are given the
    /// ID wins.
    /// </summary>
    public string[]? Ids { get; set; }

    public bool AcquirePolicyToken { get; set; }

    public string? ChangeReference { get; set; }

    /// <summary>
    /// Skips long-running-operation polling: the command returns as soon as ARM accepts the start
    /// request instead of waiting for the VM to finish starting.
    /// </summary>
    public bool NoWait { get; set; }

    protected override HttpMethod Method => HttpMethod.Post;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{Name}/start?api-version=2025-11-01";

    // The POST target itself is not GET-able, so the final call must land on the Location monitor
    // URI rather than falling back to the original request URI.
    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    protected override string ResourceIdProvider => "Microsoft.Compute";
    protected override string ResourceIdType => "virtualMachines";

    protected override void SetResourceIdArguments(string subscriptionId, string resourceGroup, string name)
    {
        SubscriptionId = subscriptionId;
        ResourceGroupName = resourceGroup;
        Name = name;
    }

    internal override Task<HttpResponseMessage> SendAsync()
    {
        // A single id stands in for --name/--resource-group; multiple ids are applied one at a time
        // by the fan-out in ProcessAsync, which sets the fields itself.
        if (!HasMultipleResourceIds(Ids))
        {
            ApplyResourceId(Ids);
        }
        ValidateResourceIdArguments(ResourceGroupName, Name, Ids);
        return NoWait ? base.SendAsync() : SendAsyncLRO();
    }

    public override async Task ProcessAsync()
    {
        if (!HasMultipleResourceIds(Ids))
        {
            await base.ProcessAsync();
            return;
        }

        // TODO revisit this batch logic with resiliency and parallelism in mind. For now the VMs are
        // started sequentially, so without --no-wait each VM is polled to completion before the next
        // one is even requested. A failure aborts the remaining ids.
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            foreach (string id in Ids!)
            {
                var (subscriptionId, resourceGroup, name) = ParseResourceId(id);
                SetResourceIdArguments(subscriptionId, resourceGroup, name);

                // Start returns no body, so there is nothing to collect; the response is only kept
                // long enough for SendAsync to have surfaced any failure.
                using HttpResponseMessage response = await SendAsync();
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
