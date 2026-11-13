// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Adapts the legacy <c>az network public-ip create</c> / <c>New-AzPublicIpAddress</c> surface onto
/// the generated <see cref="CreateOrUpdatePublicIPAddressesProcessor"/>: <c>KEY=VALUE</c> tags and IP
/// tags, the name-or-id arguments, and the legacy SKU / allocation-method / idle-timeout defaults.
/// </summary>
public class CreateOrUpdatePublicIPAddressesProcessor_Customization : CreateOrUpdatePublicIPAddressesProcessor
{
    public List<string> TagSpecs { get; set; } = [];

    /// <summary>One <c>TYPE=VALUE</c> token per IP tag, e.g. <c>FirstPartyUsage=/Sql</c>.</summary>
    public List<string> IpTagSpecs { get; set; } = [];

    public string PublicIpPrefix { get; set; } = string.Empty;
    public string DdosProtectionPlan { get; set; } = string.Empty;
    public string DdosCustomPolicy { get; set; } = string.Empty;

    // Both legacy surfaces default the SKU to Standard (`az network public-ip create --sku` carries
    // default="Standard"; New-AzPublicIpAddress assigns Standard before applying -Sku).
    private const string DefaultSku = "Standard";

    // `az network public-ip create` sends IPv4 and an idle timeout of 4 when the caller omits them.
    private const string DefaultIpAddressVersion = "IPv4";
    private const int DefaultIdleTimeoutInMinutes = 4;

    protected override PublicIPAddress? CreateRequestBody()
    {
        if (string.IsNullOrEmpty(SkuName))
        {
            SkuName = DefaultSku;
        }
        if (string.IsNullOrEmpty(PublicIPAddressVersion))
        {
            PublicIPAddressVersion = DefaultIpAddressVersion;
        }
        if (IdleTimeoutInMinutes is null or <= 0)
        {
            IdleTimeoutInMinutes = DefaultIdleTimeoutInMinutes;
        }
        if (string.IsNullOrEmpty(PublicIPAllocationMethod))
        {
            // Only the Basic SKU supports dynamic allocation; the Standard tiers are static-only.
            PublicIPAllocationMethod = SkuName.Equals("Basic", StringComparison.OrdinalIgnoreCase) ? "Dynamic" : "Static";
        }

        // The legacy CLI accepts a bare name for these and resolves it against the command's
        // resource group; a value that already looks like a resource id is passed through.
        if (PublicIpPrefix.Length > 0)
        {
            PublicIpPrefixId = ResolveResourceId(PublicIpPrefix, "publicIPPrefixes");
        }
        if (DdosProtectionPlan.Length > 0)
        {
            DdosProtectionPlanId = ResolveResourceId(DdosProtectionPlan, "ddosProtectionPlans");
        }
        if (DdosCustomPolicy.Length > 0)
        {
            DdosCustomPolicyId = ResolveResourceId(DdosCustomPolicy, "ddosCustomPolicies");
        }

        if (TagSpecs.Count > 0)
        {
            Tags = KeyValueTokens.Parse(TagSpecs, "--tags").ToDictionary(pair => pair.Key, pair => pair.Value);
        }
        if (IpTagSpecs.Count > 0)
        {
            IpTags = KeyValueTokens.Parse(IpTagSpecs, "--ip-tags")
                .Select(pair => new IpTag { IpTagType = pair.Key, Tag = pair.Value })
                .ToList();
        }

        // The legacy surfaces take only the edge zone name; EdgeZone is the only extended location type.
        if (!string.IsNullOrEmpty(ExtendedLocationName))
        {
            ExtendedLocationType ??= "EdgeZone";
        }

        return base.CreateRequestBody();
    }

    private string ResolveResourceId(string nameOrId, string resourceType) =>
        nameOrId.StartsWith('/')
            ? nameOrId
            : $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/{resourceType}/{nameOrId}";
}
