// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Identity.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Customization;

public class DeleteIdentityProcessor_Orchestrator : DeleteIdentityProcessor
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

            //handle client customisation to enable batch processing of multiple identities
            foreach (var id in Ids)
            {
                var identityId = IdentityIdResolver.ParseIdentityResourceId(id);

                using HttpResponseMessage _ = await CreateExtendedProcessor<DeleteIdentityProcessor>(p =>
                {
                    p.SubscriptionId = identityId.SubscriptionId;
                    p.ResourceGroup = identityId.ResourceGroup;
                    p.ResourceName = identityId.ResourceName;
                }).SendAsync();
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
