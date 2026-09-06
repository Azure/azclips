// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Identity.Models;
using Microsoft.Azure.Azclips.Extensions.Identity.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Customization;

public class CreateIdentityProcessor_Orchestrator : CreateIdentityProcessor
{
    [ArgumentInfo(
    Required = false,
    Description = @"Resource tags.",
    SerializedName = @"TagsInput")]
    public string[]? TagsInput { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Restrictions on which resource providers this identity can be assigned to.",
    SerializedName = @"ResourceRestriction")]
    public string[]? ResourceRestriction { get; set; }

    protected override UserAssignedIdentity CreateRequestBody()
    {
        Tags = IdentityCliArgumentParser.ParseTags(TagsInput);
        ResourceRestrictionProviders = IdentityCliArgumentParser.ParseResourceRestrictionProviders(ResourceRestriction);
        return base.CreateRequestBody();
    }
}
