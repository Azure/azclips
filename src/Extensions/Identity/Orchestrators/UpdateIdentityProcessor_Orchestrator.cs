// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Processors;

public class UpdateIdentityProcessor_Orchestrator : UpdateIdentityProcessor
{
    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (!HasMultipleIdentityResourceIds(Ids))
        {
            ApplyIdentityResourceId(Ids);
        }
        ResolveSubscriptionId();
        ValidateIdentityNameArguments(ResourceGroup, ResourceName, Ids);

        JsonObject patchBody = BuildPatchBody();
        if (Set.HasJsonUpdateOperations(Add, Remove))
        {
            patchBody = await GetCurrentIdentityAsync();
            patchBody.MergePatch(BuildPatchBody());
            patchBody.ApplyGenericUpdate(Set, Add, Remove, ParseForceString());
        }

        PatchBodyOverride = patchBody;
        return await base.SendAsync();
    }

    private async Task<JsonObject> GetCurrentIdentityAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildIdentityUri(ResourceGroup, ResourceName));
        using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Core.Abstractions.Runtime.Events.TracingHttpResponseMessage, response);
        await response.EnsureSuccessAsync(Publisher);

        JsonObject? identity = await response.Content.ReadFromJsonAsync<JsonObject>(JsonOptions);
        return identity ?? throw new InvalidOperationException($"Failed to deserialize identity '{ResourceName}'.");
    }

    private bool ParseForceString()
    {
        if (string.IsNullOrWhiteSpace(ForceString))
            return false;

        return ForceString.Trim().ToLowerInvariant() switch
        {
            "1" or "t" or "true" or "y" or "yes" => true,
            "0" or "f" or "false" or "n" or "no" => false,
            _ => throw new InvalidOperationException($"Invalid --force-string value '{ForceString}'.")
        };
    }

}
