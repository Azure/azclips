// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class NewPublicIpAddressProcessor : NetworkProcessorBase<PublicIpAddress, PublicIpAddress>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public Dictionary<string, string>? Tags { get; set; }
    public string ExtendedLocation { get; set; } = string.Empty;
    public List<string> Zones { get; set; } = [];

    public string Sku { get; set; } = string.Empty;
    public string SkuTier { get; set; } = string.Empty;

    public string AllocationMethod { get; set; } = string.Empty;
    public string IpAddressVersion { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string PublicIpPrefix { get; set; } = string.Empty;

    public string DomainNameLabel { get; set; } = string.Empty;
    public string DomainNameLabelScope { get; set; } = string.Empty;
    public string ReverseFqdn { get; set; } = string.Empty;

    public string DdosProtectionMode { get; set; } = string.Empty;
    public string DdosProtectionPlan { get; set; } = string.Empty;
    public string DdosCustomPolicy { get; set; } = string.Empty;

    public List<IpTag> IpTags { get; set; } = [];

    // 0 means "not specified"; the CLI binder has no nullable-int support.
    public int IdleTimeoutInMinutes { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{Name}?api-version={ApiVersion}";

    protected override PublicIpAddress? CreateRequestBody()
    {
        var properties = new PublicIpAddressProperties
        {
            PublicIpAllocationMethod = AllocationMethod.Length > 0 ? AllocationMethod : null,
            PublicIpAddressVersion = IpAddressVersion.Length > 0 ? IpAddressVersion : null,
            IpAddress = IpAddress.Length > 0 ? IpAddress : null,
            PublicIpPrefix = PublicIpPrefix.Length > 0 ? new SubResource { Id = PublicIpPrefix } : null
        };

        if (IdleTimeoutInMinutes > 0)
        {
            properties.IdleTimeoutInMinutes = IdleTimeoutInMinutes;
        }
        if (IpTags.Count > 0)
        {
            properties.IpTags = IpTags;
        }
        if (DomainNameLabel.Length > 0 || DomainNameLabelScope.Length > 0 || ReverseFqdn.Length > 0)
        {
            properties.DnsSettings = new PublicIpAddressDnsSettings
            {
                DomainNameLabel = DomainNameLabel.Length > 0 ? DomainNameLabel : null,
                DomainNameLabelScope = DomainNameLabelScope.Length > 0 ? DomainNameLabelScope : null,
                ReverseFqdn = ReverseFqdn.Length > 0 ? ReverseFqdn : null
            };
        }
        if (DdosProtectionMode.Length > 0 || DdosProtectionPlan.Length > 0 || DdosCustomPolicy.Length > 0)
        {
            properties.DdosSettings = new DdosSettings
            {
                ProtectionMode = DdosProtectionMode.Length > 0 ? DdosProtectionMode : null,
                DdosProtectionPlan = DdosProtectionPlan.Length > 0 ? new SubResource { Id = DdosProtectionPlan } : null,
                DdosCustomPolicy = DdosCustomPolicy.Length > 0 ? new SubResource { Id = DdosCustomPolicy } : null
            };
        }

        return new PublicIpAddress
        {
            Location = Location,
            Properties = properties,
            Tags = Tags,
            Zones = Zones.Count > 0 ? Zones : null,
            Sku = Sku.Length > 0 || SkuTier.Length > 0
                ? new PublicIpAddressSku
                {
                    Name = Sku.Length > 0 ? Sku : null,
                    Tier = SkuTier.Length > 0 ? SkuTier : null
                }
                : null,
            ExtendedLocation = ExtendedLocation.Length > 0
                ? new Models.ExtendedLocation { Name = ExtendedLocation }
                : null
        };
    }

    // swagger declares final-state-via azure-async-operation with a final-state-schema, but ARM
    // returns no such body here; fall back to no final URI.
    protected override Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
