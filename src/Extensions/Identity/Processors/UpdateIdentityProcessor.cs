// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Identity.Models;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Processors
{
    public class UpdateIdentityProcessor : IdentityProcessorBase<IdentityUpdate, UserAssignedIdentity>
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Name of resource group.",
        SerializedName = @"ResourceGroup")]
        public string ResourceGroup { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Name of the user assigned identity.",
        SerializedName = @"ResourceName")]
        public string ResourceName { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Resource tags. Format: key1=value1 key2=value2",
        SerializedName = @"Tags")]
        public Dictionary<string, string>? Tags { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Location.",
        SerializedName = @"Location")]
        public string? Location { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Enum to configure regional restrictions on identity assignment, as necessary.",
        SerializedName = @"IsolationScope")]
        public string? IsolationScope { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Restrictions on which resource providers this identity can be assigned to.",
        SerializedName = @"ResourceRestriction")]
        public List<string> ResourceRestrictionProviders { get; set; } = [];

        protected override HttpMethod Method => HttpMethod.Patch;

        protected override string CreateRequestUri() =>
            $"{ManagementEndpoint}/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourceGroups/{Uri.EscapeDataString(ResourceGroup)}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{Uri.EscapeDataString(ResourceName)}?api-version={IdentityApiVersion}";

        protected override IdentityUpdate CreateRequestBody() =>
            new()
            {
                Location = Location,
                Tags = Tags,
                Properties = ResourceRestrictionProviders.Count == 0 && string.IsNullOrWhiteSpace(IsolationScope) ? null
                : new UserAssignedIdentityProperties
                {
                    AssignmentRestrictions = ResourceRestrictionProviders.Count == 0 ? null : new AssignmentRestrictions { Providers = ResourceRestrictionProviders },
                    IsolationScope = IsolationScope
                }
            };
    }
}
