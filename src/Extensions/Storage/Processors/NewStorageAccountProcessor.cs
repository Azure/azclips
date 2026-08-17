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
        public string EdgeZone { get; set; } = string.Empty;
        public string IdentityType { get; set; } = string.Empty;
        public string UserIdentityId { get; set; } = string.Empty;
        public List<string> Zones { get; set; } = [];
        public string ZonePlacementPolicy { get; set; } = string.Empty;
        public string RoutingChoice { get; set; } = string.Empty;
        public string PublishMicrosoftEndpoints { get; set; } = string.Empty;
        public string PublishInternetEndpoints { get; set; } = string.Empty;
        public string PublishIpv6Endpoint { get; set; } = string.Empty;
        public string EnableBlobGeoPriorityReplication { get; set; } = string.Empty;
        public string KeyExpirationPeriodInDays { get; set; } = string.Empty;
        public string SasExpirationPeriod { get; set; } = string.Empty;
        public string SasExpirationAction { get; set; } = string.Empty;
        public string EnableAlw { get; set; } = string.Empty;
        public string ImmutabilityPeriodSinceCreationInDays { get; set; } = string.Empty;
        public string ImmutabilityPolicyState { get; set; } = string.Empty;
        public string AllowProtectedAppendWrites { get; set; } = string.Empty;
        public List<string> Bypass { get; set; } = [];
        public string DefaultAction { get; set; } = string.Empty;
        public string Subnet { get; set; } = string.Empty;
        public string VnetName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public List<string> IpAddress { get; set; } = [];
        public List<string> Ipv6Address { get; set; } = [];
        public string CustomDomainName { get; set; } = string.Empty;

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
                ExtendedLocation = BuildExtendedLocation(),
                Identity = BuildIdentity(),
                Zones = NullIfEmpty(Zones),
                Placement = BuildPlacement(),
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
                RoutingChoice, PublishMicrosoftEndpoints, PublishInternetEndpoints,
                PublishIpv6Endpoint, EnableBlobGeoPriorityReplication,
                KeyExpirationPeriodInDays, CustomDomainName,
                SasExpirationPeriod, SasExpirationAction,
                EnableAlw, ImmutabilityPeriodSinceCreationInDays,
                ImmutabilityPolicyState, AllowProtectedAppendWrites,
                DefaultAction, Subnet, VnetName,
            ];
            if (supplied.All(string.IsNullOrEmpty)
                && Bypass.Count == 0
                && IpAddress.Count == 0
                && Ipv6Address.Count == 0)
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
                RoutingPreference = BuildRoutingPreference(),
                DualStackEndpointPreference = BuildDualStackEndpointPreference(),
                GeoPriorityReplicationStatus = BuildGeoPriorityReplicationStatus(),
                KeyPolicy = BuildKeyPolicy(),
                SasPolicy = BuildSasPolicy(),
                ImmutableStorageWithVersioning = BuildImmutableStorageAccount(),
                NetworkAcls = BuildNetworkRuleSet(),
                CustomDomain = BuildCustomDomain(),
            };
        }

        // EdgeZone is the only extended-location type the service defines, so it is set here
        // rather than exposed as a flag.
        private ExtendedLocation? BuildExtendedLocation() =>
            string.IsNullOrEmpty(EdgeZone)
                ? null
                : new ExtendedLocation { Name = EdgeZone, Type = "EdgeZone" };

        // A user-assigned identity is only attached when the type asks for one; the service rejects
        // the map otherwise. The empty object value is what the API expects on create.
        private Identity? BuildIdentity()
        {
            if (string.IsNullOrEmpty(IdentityType))
            {
                return null;
            }

            return new Identity
            {
                Type = IdentityType,
                UserAssignedIdentities = IdentityType.Contains("UserAssigned", StringComparison.OrdinalIgnoreCase)
                                         && !string.IsNullOrEmpty(UserIdentityId)
                    ? new Dictionary<string, object> { [UserIdentityId] = new() }
                    : null,
            };
        }

        private Placement? BuildPlacement() =>
            string.IsNullOrEmpty(ZonePlacementPolicy)
                ? null
                : new Placement { ZonePlacementPolicy = ZonePlacementPolicy };

        private RoutingPreference? BuildRoutingPreference()
        {
            if (string.IsNullOrEmpty(RoutingChoice)
                && string.IsNullOrEmpty(PublishMicrosoftEndpoints)
                && string.IsNullOrEmpty(PublishInternetEndpoints))
            {
                return null;
            }

            return new RoutingPreference
            {
                RoutingChoice = NullIfEmpty(RoutingChoice),
                PublishMicrosoftEndpoints = ParseBool(PublishMicrosoftEndpoints),
                PublishInternetEndpoints = ParseBool(PublishInternetEndpoints),
            };
        }

        private DualStackEndpointPreference? BuildDualStackEndpointPreference() =>
            string.IsNullOrEmpty(PublishIpv6Endpoint)
                ? null
                : new DualStackEndpointPreference { PublishIpv6Endpoint = ParseBool(PublishIpv6Endpoint) };

        private GeoPriorityReplicationStatus? BuildGeoPriorityReplicationStatus() =>
            string.IsNullOrEmpty(EnableBlobGeoPriorityReplication)
                ? null
                : new GeoPriorityReplicationStatus { IsBlobEnabled = ParseBool(EnableBlobGeoPriorityReplication) };

        private KeyPolicy? BuildKeyPolicy() =>
            string.IsNullOrEmpty(KeyExpirationPeriodInDays)
                ? null
                : new KeyPolicy { KeyExpirationPeriodInDays = ParseInt(KeyExpirationPeriodInDays) };

        // The service requires both fields, so the action defaults to Log when only a period is
        // given, and an action without a period is rejected rather than silently dropped.
        private SasPolicy? BuildSasPolicy()
        {
            if (string.IsNullOrEmpty(SasExpirationPeriod) && string.IsNullOrEmpty(SasExpirationAction))
            {
                return null;
            }

            if (string.IsNullOrEmpty(SasExpirationPeriod))
            {
                throw new ArgumentException("SasExpirationAction can only be specified together with SasExpirationPeriod.");
            }

            return new SasPolicy
            {
                SasExpirationPeriod = SasExpirationPeriod,
                ExpirationAction = string.IsNullOrEmpty(SasExpirationAction) ? "Log" : SasExpirationAction,
            };
        }

        // Account-level immutability is gated on --enable-alw: the policy sub-flags are only
        // meaningful when it is true, so supplying them otherwise is rejected rather than ignored.
        private ImmutableStorageAccount? BuildImmutableStorageAccount()
        {
            bool hasPolicy = !string.IsNullOrEmpty(ImmutabilityPeriodSinceCreationInDays)
                || !string.IsNullOrEmpty(ImmutabilityPolicyState)
                || !string.IsNullOrEmpty(AllowProtectedAppendWrites);

            bool enabled = !string.IsNullOrEmpty(EnableAlw) && bool.Parse(EnableAlw);
            if (hasPolicy && !enabled)
            {
                throw new ArgumentException(
                    "ImmutabilityPeriodSinceCreationInDays, ImmutabilityPolicyState and AllowProtectedAppendWrites require EnableAlw to be true.");
            }

            if (string.IsNullOrEmpty(EnableAlw))
            {
                return null;
            }

            return new ImmutableStorageAccount
            {
                Enabled = ParseBool(EnableAlw),
                ImmutabilityPolicy = hasPolicy
                    ? new AccountImmutabilityPolicyProperties
                    {
                        ImmutabilityPeriodSinceCreationInDays = ParseInt(ImmutabilityPeriodSinceCreationInDays),
                        State = NullIfEmpty(ImmutabilityPolicyState),
                        AllowProtectedAppendWrites = ParseBool(AllowProtectedAppendWrites),
                    }
                    : null,
            };
        }

        // defaultAction is required by the service, so any other rule flag without it is rejected
        // rather than producing a body the service refuses. Unlike azure-cli's create, IP rules are
        // accepted here so they do not require a follow-up network-rule command.
        private NetworkRuleSet? BuildNetworkRuleSet()
        {
            bool any = Bypass.Count > 0
                || !string.IsNullOrEmpty(DefaultAction)
                || !string.IsNullOrEmpty(Subnet)
                || IpAddress.Count > 0
                || Ipv6Address.Count > 0;
            if (!any)
            {
                return null;
            }

            if (string.IsNullOrEmpty(DefaultAction))
            {
                throw new ArgumentException("DefaultAction is required when any network rule is specified.");
            }

            string action = string.IsNullOrEmpty(Action) ? "Allow" : Action;

            foreach (string bypass in Bypass)
            {
                if (!ValidBypassValues.Contains(bypass, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"Bypass value '{bypass}' is not allowed. Allowed values: {string.Join(", ", ValidBypassValues)}.");
                }
            }

            return new NetworkRuleSet
            {
                DefaultAction = DefaultAction,
                Bypass = Bypass.Count == 0 ? null : string.Join(", ", Bypass),
                VirtualNetworkRules = string.IsNullOrEmpty(Subnet)
                    ? null
                    : [new VirtualNetworkRule { Id = ResolveSubnetId(), Action = action }],
                IpRules = BuildIpRules(IpAddress, action),
                Ipv6Rules = BuildIpRules(Ipv6Address, action),
            };
        }

        private static readonly string[] ValidBypassValues = ["None", "Logging", "Metrics", "AzureServices"];

        private static List<IPRule>? BuildIpRules(List<string> addresses, string action) =>
            addresses.Count == 0
                ? null
                : [.. addresses.Select(address => new IPRule { Value = address, Action = action })];

        // Accepts either a full subnet resource id or a subnet name paired with a virtual network
        // name, which is composed into an id. No lookup is performed.
        private string ResolveSubnetId()
        {
            if (Subnet.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase))
            {
                return Subnet;
            }

            if (string.IsNullOrEmpty(VnetName))
            {
                throw new ArgumentException("VnetName is required when Subnet is a name rather than a resource id.");
            }

            return $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}"
                 + $"/providers/Microsoft.Network/virtualNetworks/{VnetName}/subnets/{Subnet}";
        }

        // UseSubDomainName is meaningful only on update, so only the name is sent on create.
        private CustomDomain? BuildCustomDomain() =>
            string.IsNullOrEmpty(CustomDomainName)
                ? null
                : new CustomDomain { Name = CustomDomainName };

        private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

        private static List<string>? NullIfEmpty(List<string> values) => values.Count == 0 ? null : values;

        private static int? ParseInt(string value) => string.IsNullOrEmpty(value) ? null : int.Parse(value);

        private static bool? ParseBool(string value) => string.IsNullOrEmpty(value) ? null : bool.Parse(value);

        // Generic
        
        protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
        protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
    }
}



