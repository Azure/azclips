// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Get Share Properties answers entirely in response headers, so the generated
    /// <see cref="GetPropertiesShareProcessor"/> declares no response type and writes nothing. This processor maps
    /// the spec's response headers onto <see cref="ShareItemInternal"/>, the same shape List Shares returns per share.
    /// </summary>
    public class GetPropertiesShareProcessor_Orchestrator : ProcessorBase<object, ShareItemInternal>, ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName));

        public string? Snapshot { get; set; }
        public string? LeaseId { get; set; }
        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                using HttpResponseMessage response = await CreateExtendedProcessor<GetPropertiesShareProcessor>(p =>
                {
                    p.Url = Endpoint;
                    p.Sharesnapshot = Snapshot;
                    p.LeaseId = LeaseId;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();
                await WriteOutput(ToShareItem(response));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private ShareItemInternal ToShareItem(HttpResponseMessage response)
        {
            IReadOnlyDictionary<string, string> headers = FileWireFormat.ReadHeaders(response);
            string? Text(string name) => headers.TryGetValue(name, out string? value) && value.Length > 0 ? value : null;
            bool? Bool(string name) => Text(name) is { } value ? bool.Parse(value) : null;
            int? Int(string name) => Text(name) is { } value ? int.Parse(value, CultureInfo.InvariantCulture) : null;
            long? Long(string name) => Text(name) is { } value ? long.Parse(value, CultureInfo.InvariantCulture) : null;

            return new ShareItemInternal
            {
                Name = ShareNameFromUrl(),
                Snapshot = Snapshot,
                Metadata = FileWireFormat.ReadMetadata(headers),
                Properties = new SharePropertiesInternal
                {
                    LastModified = Text("Last-Modified")!,
                    ETag = Text("ETag")!,
                    Quota = Int("x-ms-share-quota") ?? 0,
                    ProvisionedIops = Int("x-ms-share-provisioned-iops"),
                    ProvisionedIngressMBps = Int("x-ms-share-provisioned-ingress-mbps"),
                    ProvisionedEgressMBps = Int("x-ms-share-provisioned-egress-mbps"),
                    ProvisionedBandwidthMiBps = Int("x-ms-share-provisioned-bandwidth-mibps"),
                    NextAllowedQuotaDowngradeTime = Text("x-ms-share-next-allowed-quota-downgrade-time"),
                    AccessTier = Text("x-ms-access-tier"),
                    AccessTierChangeTime = Text("x-ms-access-tier-change-time"),
                    AccessTierTransitionState = Text("x-ms-access-tier-transition-state"),
                    LeaseStatus = Text("x-ms-lease-status"),
                    LeaseState = Text("x-ms-lease-state"),
                    LeaseDuration = Text("x-ms-lease-duration"),
                    EnabledProtocols = Text("x-ms-enabled-protocols"),
                    RootSquash = Text("x-ms-root-squash"),
                    EnableSnapshotVirtualDirectoryAccess = Bool("x-ms-enable-snapshot-virtual-directory-access"),
                    PaidBurstingEnabled = Bool("x-ms-share-paid-bursting-enabled"),
                    PaidBurstingMaxIops = Long("x-ms-share-paid-bursting-max-iops"),
                    PaidBurstingMaxBandwidthMibps = Long("x-ms-share-paid-bursting-max-bandwidth-mibps"),
                    NextAllowedProvisionedIopsDowngradeTime = Text("x-ms-share-next-allowed-provisioned-iops-downgrade-time"),
                    NextAllowedProvisionedBandwidthDowngradeTime = Text("x-ms-share-next-allowed-provisioned-bandwidth-downgrade-time"),
                    EnableSmbDirectoryLease = Bool("x-ms-enable-smb-directory-lease"),
                    CreationTime = Text("x-ms-share-creation-time"),
                },
            };
        }

        /// <summary>The share name is the first URL path segment; the response does not echo it.</summary>
        private string ShareNameFromUrl() =>
            Uri.UnescapeDataString(new Uri(Endpoint).AbsolutePath.TrimStart('/').Split('/')[0]);
    }
}
