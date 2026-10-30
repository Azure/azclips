// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Adds the legacy <c>-Id</c> input (a subscription deployment resource id) and the CLI <c>--no-wait</c> input
/// (return once ARM accepts the DELETE, without polling) to the delete-deployment processor.
/// </summary>
public class RemoveSubscriptionDeploymentProcessor_Orchestrator : RemoveSubscriptionDeploymentProcessor
{
    public string? Id { get; set; }

    public bool NoWait { get; set; }

    internal override Task<HttpResponseMessage> SendAsync() => NoWait ? SendAsyncNoPoll() : base.SendAsync();

    protected override async Task BeforeProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(Id))
        {
            (SubscriptionId, Name) = DeploymentIdResolver.ParseSubscription(Id);
        }

        await base.BeforeProcessAsync();
    }
}
