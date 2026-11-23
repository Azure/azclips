// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Shapes the <see cref="NewPublicIPAddressProcessor"/> request body behind public IP address create:
/// SKU, allocation method, IP version, and idle timeout defaults, edge zone, DDoS plan, DDoS custom policy,
/// and public IP prefix name-or-ID expansion, and IP tags. The route, method, and api-version stay in the atomic processor.
/// </summary>
public class NewPublicIPAddressProcessor_Orchestrator : NewPublicIPAddressProcessor
{
    internal const string DefaultSkuName = "Standard";
    internal const string BasicSkuName = "Basic";
    internal const string DynamicAllocation = "Dynamic";
    internal const string StaticAllocation = "Static";
    internal const string DefaultIpVersion = "IPv4";
    internal const int DefaultIdleTimeoutInMinutes = 4;
    internal const string EdgeZoneType = "EdgeZone";

    /// <summary>Edge zone to create the public IP address in.</summary>
    public string? EdgeZone { get; set; }

    /// <summary>IP tags as ordered type/value pairs.</summary>
    public IList<KeyValuePair<string, string>>? IpTagPairs { get; set; }

    /// <summary>IP tags as <c>TYPE=VALUE</c> strings.</summary>
    public string[]? IpTagValues { get; set; }

    /// <summary>CLI tags as <c>KEY=VALUE</c> strings (a value is required). When given, they replace <see cref="NewPublicIPAddressProcessor.Tags"/>.</summary>
    public string[]? TagsInput { get; set; }

    /// <summary>
    /// Shaping runs here, not in BeforeProcessAsync, so it also applies when this processor runs
    /// as a child of another processor. Every step is idempotent because the body can be built more than once.
    /// </summary>
    protected override PublicIPAddress? CreateRequestBody()
    {
        Tags = NetworkTagsParser.ParseTags(TagsInput, requireValue: true) ?? Tags;
        ApplyDefaults();
        ApplyEdgeZone();
        ApplyResourceIds();
        ApplyIpTags();
        return base.CreateRequestBody();
    }

    private void ApplyDefaults()
    {
        // The SKU default runs first so the allocation default sees the effective SKU.
        if (string.IsNullOrWhiteSpace(SkuName))
        {
            SkuName = DefaultSkuName;
        }

        if (string.IsNullOrWhiteSpace(PublicIPAllocationMethod))
        {
            PublicIPAllocationMethod = string.Equals(SkuName, BasicSkuName, StringComparison.OrdinalIgnoreCase)
                ? DynamicAllocation
                : StaticAllocation;
        }

        if (string.IsNullOrWhiteSpace(PublicIPAddressVersion))
        {
            PublicIPAddressVersion = DefaultIpVersion;
        }

        if (IdleTimeoutInMinutes is not > 0)
        {
            IdleTimeoutInMinutes = DefaultIdleTimeoutInMinutes;
        }
    }

    private void ApplyEdgeZone()
    {
        if (!string.IsNullOrWhiteSpace(EdgeZone))
        {
            ExtendedLocationName = EdgeZone;
            ExtendedLocationType = EdgeZoneType;
        }
        else if (Body is not null)
        {
            Body.ExtendedLocation = null;
        }
    }

    private void ApplyResourceIds()
    {
        DdosProtectionPlanId = ToNetworkResourceId(DdosProtectionPlanId, "ddosProtectionPlans");
        DdosCustomPolicyId = ToNetworkResourceId(DdosCustomPolicyId, "ddosCustomPolicies");
        PublicIpPrefixId = ToNetworkResourceId(PublicIpPrefixId, "publicIPPrefixes");
    }

    private string? ToNetworkResourceId(string? nameOrId, string resourceType)
    {
        if (string.IsNullOrWhiteSpace(nameOrId) || nameOrId.StartsWith('/'))
        {
            return nameOrId;
        }

        return $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/{resourceType}/{nameOrId}";
    }

    private void ApplyIpTags()
    {
        var pairs = new List<KeyValuePair<string, string>>();
        if (IpTagPairs is not null)
        {
            pairs.AddRange(IpTagPairs);
        }

        if (IpTagValues is not null)
        {
            pairs.AddRange(IpTagValues.Where(value => !string.IsNullOrWhiteSpace(value)).Select(ParseIpTag));
        }

        if (pairs.Count > 0)
        {
            IpTags = pairs.Select(pair => new IpTag { IpTagType = pair.Key, Tag = pair.Value }).ToList();
        }
    }

    private static KeyValuePair<string, string> ParseIpTag(string value)
    {
        int separator = value.IndexOf('=');
        string type = separator < 0 ? string.Empty : value[..separator].Trim();
        if (type.Length == 0)
        {
            throw AzclipsErrorException.InvalidInput($"Invalid IP tag '{value}'. Use the format TYPE=VALUE.");
        }

        return new KeyValuePair<string, string>(type, value[(separator + 1)..]);
    }
}
