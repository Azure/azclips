// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

public class ShowIdentityProcessor_Orchestrator : ShowIdentityProcessor
{
    [ArgumentInfo(
    Required = false,
    Description = @"One or more identity resource IDs.",
    SerializedName = @"Ids")]
    public string[]? Ids { get; set; }

    public override async Task ProcessAsync()
    {
        if (Ids is null || Ids.Length == 0)
        {
            await base.ProcessAsync();
            return;
        }

        try
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            var results = new List<UserAssignedIdentity>();
            //handle client customisation to enable batch processing of multiple identities
            foreach (var id in Ids)
            {
                var identityId = IdentityIdResolver.ParseIdentityResourceId(id);
                using HttpResponseMessage response = await CreateExtendedProcessor<ShowIdentityProcessor>(p =>
                {
                    p.SubscriptionId = identityId.SubscriptionId;
                    p.ResourceGroup = identityId.ResourceGroup;
                    p.ResourceName = identityId.ResourceName;
                }).SendAsync();

                if (await ReadResponseBodyAsync<UserAssignedIdentity>(response) is { } identity)
                {
                    results.Add(identity);
                }
            }

            await WriteOutput(results.ToArray());
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
