// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Adapts the legacy <c>az network public-ip create</c> / <c>New-AzPublicIpAddress</c> surface onto
/// the generated <see cref="NewPublicIpAddressProcessor"/>: <c>KEY[=VALUE]</c> tags, <c>TYPE=VALUE</c> IP tags, the
/// name-or-id arguments, and the legacy SKU / allocation-method / idle-timeout defaults. The
/// generated processor stays a straight spec-to-body mapping and takes already-typed values.
/// </summary>
public class NewPublicIpAddressProcessor_Customization : NewPublicIpAddressProcessor
{
    public List<string> TagSpecs { get; set; } = [];

    /// <summary>One <c>TYPE=VALUE</c> token per IP tag, e.g. <c>FirstPartyUsage=/Sql</c>.</summary>
    public List<string> IpTagSpecs { get; set; } = [];

    // Both legacy surfaces default the SKU to Standard (`az network public-ip create --sku` carries
    // default="Standard"; New-AzPublicIpAddress assigns Standard before applying -Sku).
    private const string DefaultSku = "Standard";

    // `az network public-ip create` sends IPv4 and an idle timeout of 4 when the caller omits them.
    private const string DefaultIpAddressVersion = "IPv4";
    private const int DefaultIdleTimeoutInMinutes = 4;

    protected override PublicIpAddress? CreateRequestBody()
    {
        if (Sku.Length == 0)
        {
            Sku = DefaultSku;
        }
        if (IpAddressVersion.Length == 0)
        {
            IpAddressVersion = DefaultIpAddressVersion;
        }
        if (IdleTimeoutInMinutes <= 0)
        {
            IdleTimeoutInMinutes = DefaultIdleTimeoutInMinutes;
        }
        if (AllocationMethod.Length == 0)
        {
            // Only the Basic SKU supports dynamic allocation; the Standard tiers are static-only.
            AllocationMethod = Sku.Equals("Basic", StringComparison.OrdinalIgnoreCase) ? "Dynamic" : "Static";
        }

        // The legacy CLI accepts a bare name for these and resolves it against the command's
        // resource group; a value that already looks like a resource id is passed through.
        PublicIpPrefix = ResolveResourceId(PublicIpPrefix, "publicIPPrefixes");
        DdosProtectionPlan = ResolveResourceId(DdosProtectionPlan, "ddosProtectionPlans");
        DdosCustomPolicy = ResolveResourceId(DdosCustomPolicy, "ddosCustomPolicies");

        Tags = ParseTags();
        if (IpTagSpecs.Count > 0)
        {
            IpTags = ParseIpTags();
        }

        return base.CreateRequestBody();
    }

    private Dictionary<string, string>? ParseTags()
    {
        if (TagSpecs.Count == 0)
        {
            return null;
        }
        // Azure CLI tags are KEY[=VALUE], and the last occurrence of a repeated key wins.
        var tags = new Dictionary<string, string>();
        foreach (var (key, value) in KeyValueTokens.Parse(TagSpecs, "--tags", allowBareKey: true))
        {
            tags[key] = value;
        }
        return tags;
    }

    private List<IpTag> ParseIpTags() =>
        KeyValueTokens.Parse(IpTagSpecs, "--ip-tags")
            .Select(pair => new IpTag { IpTagType = pair.Key, Tag = pair.Value })
            .ToList();

    private string ResolveResourceId(string nameOrId, string resourceType) =>
        nameOrId.Length == 0 || nameOrId.StartsWith('/')
            ? nameOrId
            : $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/{resourceType}/{nameOrId}";
}
