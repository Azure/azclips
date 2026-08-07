// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors
{
    public class NewStorageAccountProcessor : StorageProcessorBase<StorageAccount>
    {
        public string AccountName { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string SkuName { get; set; } = string.Empty;

        // KEY=VALUE pairs.
        public List<string> Tags { get; set; } = [];

        // Optional properties are strings, not the model's bool?/enum types: the argument
        // binder assigns via Convert.ChangeType, which cannot target Nullable<T>. Empty
        // means "not supplied" and is dropped from the request.
        public string AccessTier { get; set; } = string.Empty;
        public string MinimumTlsVersion { get; set; } = string.Empty;
        public string PublicNetworkAccess { get; set; } = string.Empty;
        public string AllowedCopyScope { get; set; } = string.Empty;
        public string LargeFileSharesState { get; set; } = string.Empty;
        public string DnsEndpointType { get; set; } = string.Empty;
        public string SupportsHttpsTrafficOnly { get; set; } = string.Empty;
        public string AllowBlobPublicAccess { get; set; } = string.Empty;
        public string AllowSharedKeyAccess { get; set; } = string.Empty;
        public string IsHnsEnabled { get; set; } = string.Empty;
        public string IsSftpEnabled { get; set; } = string.Empty;
        public string IsLocalUserEnabled { get; set; } = string.Empty;
        public string EnableExtendedGroups { get; set; } = string.Empty;
        public string IsNfsV3Enabled { get; set; } = string.Empty;
        public string AllowCrossTenantReplication { get; set; } = string.Empty;
        public string DefaultToOAuthAuthentication { get; set; } = string.Empty;

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{AccountName}?api-version=2026-04-01";
            StorageAccountCreateParameters body = new StorageAccountCreateParameters
            {
                Kind = Kind,
                Location = Location,
                Sku = new Sku
                {
                    Name = SkuName,
                },
                Tags = BuildTags(),
                Properties = BuildProperties(),
            };
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uri)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        // Returns null when no tags were supplied so the key is omitted entirely.
        private Dictionary<string, string>? BuildTags()
        {
            if (Tags.Count == 0)
            {
                return null;
            }

            Dictionary<string, string> tags = [];
            foreach (string tag in Tags)
            {
                int separator = tag.IndexOf('=');
                if (separator <= 0)
                {
                    throw new ArgumentException($"Tag '{tag}' is not in KEY=VALUE form.");
                }
                tags[tag[..separator]] = tag[(separator + 1)..];
            }
            return tags;
        }

        // Returns null when no optional property was supplied, keeping the minimal request
        // body at {sku, kind, location} rather than carrying an empty properties object.
        private StorageAccountPropertiesCreateParameters? BuildProperties()
        {
            string[] supplied =
            [
                AccessTier, MinimumTlsVersion, PublicNetworkAccess, AllowedCopyScope,
                LargeFileSharesState, DnsEndpointType, SupportsHttpsTrafficOnly,
                AllowBlobPublicAccess, AllowSharedKeyAccess, IsHnsEnabled, IsSftpEnabled,
                IsLocalUserEnabled, EnableExtendedGroups, IsNfsV3Enabled,
                AllowCrossTenantReplication, DefaultToOAuthAuthentication,
            ];
            if (supplied.All(string.IsNullOrEmpty))
            {
                return null;
            }

            return new StorageAccountPropertiesCreateParameters
            {
                AccessTier = NullIfEmpty(AccessTier),
                MinimumTlsVersion = NullIfEmpty(MinimumTlsVersion),
                PublicNetworkAccess = NullIfEmpty(PublicNetworkAccess),
                AllowedCopyScope = NullIfEmpty(AllowedCopyScope),
                LargeFileSharesState = NullIfEmpty(LargeFileSharesState),
                DnsEndpointType = NullIfEmpty(DnsEndpointType),
                SupportsHttpsTrafficOnly = ParseBool(SupportsHttpsTrafficOnly),
                AllowBlobPublicAccess = ParseBool(AllowBlobPublicAccess),
                AllowSharedKeyAccess = ParseBool(AllowSharedKeyAccess),
                IsHnsEnabled = ParseBool(IsHnsEnabled),
                IsSftpEnabled = ParseBool(IsSftpEnabled),
                IsLocalUserEnabled = ParseBool(IsLocalUserEnabled),
                EnableExtendedGroups = ParseBool(EnableExtendedGroups),
                IsNfsV3Enabled = ParseBool(IsNfsV3Enabled),
                AllowCrossTenantReplication = ParseBool(AllowCrossTenantReplication),
                DefaultToOAuthAuthentication = ParseBool(DefaultToOAuthAuthentication),
            };
        }

        private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

        private static bool? ParseBool(string value) => string.IsNullOrEmpty(value) ? null : bool.Parse(value);

        // Generic
        
        protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
        protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
    }
}



