// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Linq;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// List Shares answers in XML (<c>EnumerationResults</c>), while the generated models carry only JSON names,
    /// so the response is mapped by hand using the spec's <c>@Xml.name</c> element names.
    /// The request goes to the account root, which is the base <see cref="ProcessorBase.Url"/> resolution.
    /// </summary>
    public partial class ListSharesSegmentServiceProcessor : ISasAuthorized
    {
        public string? SasToken { get; set; }

        protected override Task<ListSharesResponse?> DeserializeResponseAsync(string rawBody)
        {
            XElement root = XDocument.Parse(rawBody).Root!;
            ListSharesResponse response = new()
            {
                ServiceEndpoint = root.Attribute("ServiceEndpoint")?.Value!,
                Prefix = Text(root, "Prefix"),
                Marker = Text(root, "Marker"),
                MaxResults = Int(root, "MaxResults"),
                ShareItems = root.Element("Shares")?.Elements("Share").Select(ToShareItem).ToList() ?? [],
                NextMarker = Text(root, "NextMarker")!,
            };
            return Task.FromResult<ListSharesResponse?>(response);
        }

        private static ShareItemInternal ToShareItem(XElement share)
        {
            XElement? metadata = share.Element("Metadata");
            return new ShareItemInternal
            {
                Name = Text(share, "Name")!,
                Snapshot = Text(share, "Snapshot"),
                Deleted = Bool(share, "Deleted"),
                Version = Text(share, "Version"),
                Properties = ToShareProperties(share.Element("Properties")),
                Metadata = metadata?.Elements().ToDictionary(m => m.Name.LocalName, m => m.Value),
            };
        }

        private static SharePropertiesInternal ToShareProperties(XElement? properties) => new()
        {
            LastModified = Text(properties, "Last-Modified")!,
            ETag = Text(properties, "Etag")!,
            Quota = Int(properties, "Quota") ?? 0,
            ProvisionedIops = Int(properties, "ProvisionedIops"),
            ProvisionedIngressMBps = Int(properties, "ProvisionedIngressMBps"),
            ProvisionedEgressMBps = Int(properties, "ProvisionedEgressMBps"),
            ProvisionedBandwidthMiBps = Int(properties, "ProvisionedBandwidthMiBps"),
            NextAllowedQuotaDowngradeTime = Text(properties, "NextAllowedQuotaDowngradeTime"),
            DeletedTime = Text(properties, "DeletedTime"),
            RemainingRetentionDays = Int(properties, "RemainingRetentionDays"),
            AccessTier = Text(properties, "AccessTier"),
            AccessTierChangeTime = Text(properties, "AccessTierChangeTime"),
            AccessTierTransitionState = Text(properties, "AccessTierTransitionState"),
            LeaseStatus = Text(properties, "LeaseStatus"),
            LeaseState = Text(properties, "LeaseState"),
            LeaseDuration = Text(properties, "LeaseDuration"),
            EnabledProtocols = Text(properties, "EnabledProtocols"),
            RootSquash = Text(properties, "RootSquash"),
            EnableSnapshotVirtualDirectoryAccess = Bool(properties, "EnableSnapshotVirtualDirectoryAccess"),
            PaidBurstingEnabled = Bool(properties, "PaidBurstingEnabled"),
            PaidBurstingMaxIops = Long(properties, "PaidBurstingMaxIops"),
            PaidBurstingMaxBandwidthMibps = Long(properties, "PaidBurstingMaxBandwidthMibps"),
            IncludedBurstIops = Long(properties, "IncludedBurstIops"),
            MaxBurstCreditsForIops = Long(properties, "MaxBurstCreditsForIops"),
            NextAllowedProvisionedIopsDowngradeTime = Text(properties, "NextAllowedProvisionedIopsDowngradeTime"),
            NextAllowedProvisionedBandwidthDowngradeTime = Text(properties, "NextAllowedProvisionedBandwidthDowngradeTime"),
            EnableSmbDirectoryLease = Bool(properties, "EnableSmbDirectoryLease"),
            CreationTime = Text(properties, "Creation-Time"),
        };

        /// <summary>The element's text, or null when it is absent or empty (the service emits e.g. <c>&lt;NextMarker /&gt;</c>).</summary>
        private static string? Text(XElement? parent, string name) =>
            parent?.Element(name)?.Value is { Length: > 0 } value ? value : null;

        private static bool? Bool(XElement? parent, string name) =>
            Text(parent, name) is { } value ? XmlConvert.ToBoolean(value) : null;

        private static int? Int(XElement? parent, string name) =>
            Text(parent, name) is { } value ? XmlConvert.ToInt32(value) : null;

        private static long? Long(XElement? parent, string name) =>
            Text(parent, name) is { } value ? XmlConvert.ToInt64(value) : null;
    }
}
