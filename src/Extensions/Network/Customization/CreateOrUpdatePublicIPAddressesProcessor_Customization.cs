// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Public IP address create. Adds the location, SKU, allocation method and IP version defaults, and
/// sends the DNS settings and edge zone only when given. The request route and LRO polling
/// stay in <see cref="CreateOrUpdatePublicIPAddressesProcessor"/>.
/// </summary>
public class CreateOrUpdatePublicIPAddressesProcessor_Customization : CreateOrUpdatePublicIPAddressesProcessor
{
    private const string DefaultSku = "Standard";
    private const string DefaultVersion = "IPv4";
    private const string StaticAllocation = "Static";
    private const string DynamicAllocation = "Dynamic";
    private static readonly string[] StaticDefaultSkus = ["Standard", "StandardV2"];

    public string? Sku { get; set; }
    public string? Tier { get; set; }
    public string? AllocationMethod { get; set; }
    public string? Version { get; set; }
    public string? DomainNameLabel { get; set; }
    public string? DomainNameLabelScope { get; set; }
    public string? ReverseFqdn { get; set; }
    public string? EdgeZone { get; set; }

    /// <summary>CLI tags in KEY[=VALUE] form. They replace <c>Tags</c> when given.</summary>
    public string[]? TagsInput { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        Tags = NetworkTagsParser.Parse(TagsInput) ?? Tags;

        if (string.IsNullOrWhiteSpace(Location))
        {
            Location = await ResourceGroupLocationResolver.GetLocationAsync(
                CreateExtendedProcessor<GetResourceGroupsProcessor>(p =>
                {
                    p.SubscriptionId = SubscriptionId;
                    p.ResourceGroupName = ResourceGroupName;
                }),
                Publisher,
                JsonOptions);
        }

        ApplySkuAndAddressDefaults();
        ApplyDnsSettings();
        ApplyEdgeZone();

        return await base.SendAsync();
    }

    private void ApplySkuAndAddressDefaults()
    {
        string sku = IsGiven(Sku) ? Sku! : DefaultSku;
        SkuName = sku;

        if (IsGiven(Tier))
        {
            SkuTier = Tier;
        }

        PublicIPAllocationMethod = IsGiven(AllocationMethod)
            ? AllocationMethod
            : StaticDefaultSkus.Contains(sku, StringComparer.OrdinalIgnoreCase) ? StaticAllocation : DynamicAllocation;

        PublicIPAddressVersion = IsGiven(Version) ? Version : DefaultVersion;
    }

    // Each DNS setter creates "dnsSettings" on first use, so a setter runs only for a given input.
    private void ApplyDnsSettings()
    {
        if (IsGiven(DomainNameLabel))
        {
            DnsSettingsDomainNameLabel = DomainNameLabel;
        }

        if (IsGiven(DomainNameLabelScope))
        {
            DnsSettingsDomainNameLabelScope = DomainNameLabelScope;
        }

        if (IsGiven(ReverseFqdn))
        {
            DnsSettingsReverseFqdn = ReverseFqdn;
        }
    }

    private void ApplyEdgeZone()
    {
        if (IsGiven(EdgeZone))
        {
            ExtendedLocationName = EdgeZone;
            ExtendedLocationType = "EdgeZone";
        }
    }

    private static bool IsGiven(string? value) => !string.IsNullOrWhiteSpace(value);
}
