// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors
{
    /// <summary>
    /// The customer-facing <c>az storage account create</c> / <c>New-AzStorageAccount</c> command.
    /// <para>
    /// Owns the full argument surface, the validation rules and the flat-argument-to-nested-model
    /// mapping. None of that is derivable from the storage TypeSpec, so it is hand-written here
    /// rather than in <see cref="NewStorageAccountProcessor"/>, which stays pure transport and
    /// mirrors what codegen would emit. The assembled body is handed to that processor via
    /// <c>CreateExtendedProcessor</c>.
    /// </para>
    /// </summary>
    public class NewStorageAccountProcessor_Customized : StorageProcessorBase<StorageAccountCreateParameters, StorageAccount>
    {
        public string SubscriptionId { get; set; } = string.Empty;

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
        public string EnableFilesAadds { get; set; } = string.Empty;
        public string EnableFilesAadkerb { get; set; } = string.Empty;
        public string EnableFilesAdds { get; set; } = string.Empty;
        public string DomainName { get; set; } = string.Empty;
        public string NetBiosDomainName { get; set; } = string.Empty;
        public string ForestName { get; set; } = string.Empty;
        public string DomainGuid { get; set; } = string.Empty;
        public string DomainSid { get; set; } = string.Empty;
        public string AzureStorageSid { get; set; } = string.Empty;
        public string SamAccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string DefaultSharePermission { get; set; } = string.Empty;
        public string EnableSmbOauth { get; set; } = string.Empty;
        public string EncryptionKeySource { get; set; } = string.Empty;
        public string EncryptionKeyName { get; set; } = string.Empty;
        public string EncryptionKeyVault { get; set; } = string.Empty;
        public string EncryptionKeyVersion { get; set; } = string.Empty;
        public string RequireInfrastructureEncryption { get; set; } = string.Empty;
        public string KeyVaultUserIdentityId { get; set; } = string.Empty;
        public string KeyVaultFederatedClientId { get; set; } = string.Empty;
        public string EncryptionKeyTypeForTable { get; set; } = string.Empty;
        public string EncryptionKeyTypeForQueue { get; set; } = string.Empty;
        public string CustomDomainName { get; set; } = string.Empty;

        // Builds the request body (validating as it goes) and delegates the PUT plus
        // long-running-operation polling to the atomic NewStorageAccountProcessor.
        internal override async Task<HttpResponseMessage> SendAsync()
        {
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

            NewStorageAccountProcessor processor = CreateExtendedProcessor<NewStorageAccountProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroupName = ResourceGroupName;
                p.AccountName = AccountName;
                p.Body = body;
            });

            return await processor.SendAsync();
        }

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
                DefaultAction, Subnet, VnetName, Action,
                EnableFilesAadds, EnableFilesAadkerb, EnableFilesAdds,
                DomainName, NetBiosDomainName, ForestName, DomainGuid, DomainSid,
                AzureStorageSid, SamAccountName, AccountType,
                DefaultSharePermission, EnableSmbOauth,
                EncryptionKeySource, EncryptionKeyName, EncryptionKeyVault, EncryptionKeyVersion,
                RequireInfrastructureEncryption, KeyVaultUserIdentityId, KeyVaultFederatedClientId,
                EncryptionKeyTypeForTable, EncryptionKeyTypeForQueue,
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
                AzureFilesIdentityBasedAuthentication = BuildAzureFilesIdentityBasedAuthentication(),
                Encryption = BuildEncryption(),
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
            // VnetName and Action only qualify other network rules, so on their own they would be
            // dropped silently while still marking the properties bag as supplied, which sends an
            // otherwise empty object.
            if (!string.IsNullOrEmpty(VnetName) && string.IsNullOrEmpty(Subnet))
            {
                throw new ArgumentException("VnetName can only be specified together with Subnet.");
            }

            bool anyRule = !string.IsNullOrEmpty(Subnet) || IpAddress.Count > 0 || Ipv6Address.Count > 0;
            if (!string.IsNullOrEmpty(Action) && !anyRule)
            {
                throw new ArgumentException("Action can only be specified together with Subnet, IpAddress or Ipv6Address.");
            }

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

        // The three enablers map onto a single directoryServiceOptions value, so at most one may be
        // true. azure-cli lets a later flag silently overwrite an earlier one; that is rejected here
        // instead. defaultSharePermission and SMB OAuth apply to any option, so they create the
        // parent with 'None' when no enabler was given.
        private AzureFilesIdentityBasedAuthentication? BuildAzureFilesIdentityBasedAuthentication()
        {
            bool? aadds = ParseBool(EnableFilesAadds);
            bool? aadkerb = ParseBool(EnableFilesAadkerb);
            bool? adds = ParseBool(EnableFilesAdds);

            if ((aadds == true ? 1 : 0) + (aadkerb == true ? 1 : 0) + (adds == true ? 1 : 0) > 1)
            {
                throw new ArgumentException(
                    "At most one of EnableFilesAadds, EnableFilesAadkerb and EnableFilesAdds can be true.");
            }

            string[] requiredAdFields = [DomainName, NetBiosDomainName, ForestName, DomainGuid, DomainSid, AzureStorageSid];
            bool anyAdField = requiredAdFields.Any(field => !string.IsNullOrEmpty(field))
                || !string.IsNullOrEmpty(SamAccountName)
                || !string.IsNullOrEmpty(AccountType);

            string? options = null;
            ActiveDirectoryProperties? directoryProperties = null;

            if (adds == true)
            {
                if (requiredAdFields.Any(string.IsNullOrEmpty))
                {
                    throw new ArgumentException(
                        "EnableFilesAdds requires all of DomainName, NetBiosDomainName, ForestName, DomainGuid, DomainSid and AzureStorageSid.");
                }

                options = "AD";
                directoryProperties = new ActiveDirectoryProperties
                {
                    DomainName = DomainName,
                    NetBiosDomainName = NetBiosDomainName,
                    ForestName = ForestName,
                    DomainGuid = DomainGuid,
                    DomainSid = DomainSid,
                    AzureStorageSid = AzureStorageSid,
                    SamAccountName = NullIfEmpty(SamAccountName),
                    AccountType = NullIfEmpty(AccountType),
                };
            }
            else if (aadkerb == true)
            {
                options = "AADKERB";
                // Only the domain name and GUID are meaningful for Kerberos.
                directoryProperties = string.IsNullOrEmpty(DomainName) && string.IsNullOrEmpty(DomainGuid)
                    ? null
                    : new ActiveDirectoryProperties
                    {
                        DomainName = NullIfEmpty(DomainName),
                        DomainGuid = NullIfEmpty(DomainGuid),
                    };
            }
            else if (aadds == true)
            {
                options = "AADDS";
            }
            else if (aadds == false || aadkerb == false || adds == false)
            {
                if (anyAdField)
                {
                    throw new ArgumentException(
                        "Active Directory properties cannot be specified when Azure Files identity based authentication is disabled.");
                }

                options = "None";
            }

            if (options is null)
            {
                if (anyAdField)
                {
                    throw new ArgumentException(
                        "Active Directory properties require EnableFilesAdds or EnableFilesAadkerb to be true.");
                }

                if (string.IsNullOrEmpty(DefaultSharePermission) && string.IsNullOrEmpty(EnableSmbOauth))
                {
                    return null;
                }

                options = "None";
            }

            return new AzureFilesIdentityBasedAuthentication
            {
                DirectoryServiceOptions = options,
                ActiveDirectoryProperties = directoryProperties,
                DefaultSharePermission = NullIfEmpty(DefaultSharePermission),
                SmbOAuthSettings = string.IsNullOrEmpty(EnableSmbOauth)
                    ? null
                    : new SmbOAuthSettings { IsSmbOAuthEnabled = ParseBool(EnableSmbOauth) },
            };
        }

        // Built only when an encryption flag is supplied, matching Azure PowerShell; the service
        // otherwise applies its own defaults. Whenever it is built, services must be non-null, which
        // both azure-cli and Azure PowerShell work around by defaulting to blob.
        private Encryption? BuildEncryption()
        {
            bool anyKeyVaultKey = !string.IsNullOrEmpty(EncryptionKeyVault)
                || !string.IsNullOrEmpty(EncryptionKeyName)
                || !string.IsNullOrEmpty(EncryptionKeyVersion);
            bool anyEncryptionIdentity = !string.IsNullOrEmpty(KeyVaultUserIdentityId)
                || !string.IsNullOrEmpty(KeyVaultFederatedClientId);
            bool any = anyKeyVaultKey
                || anyEncryptionIdentity
                || !string.IsNullOrEmpty(EncryptionKeySource)
                || !string.IsNullOrEmpty(RequireInfrastructureEncryption)
                || !string.IsNullOrEmpty(EncryptionKeyTypeForTable)
                || !string.IsNullOrEmpty(EncryptionKeyTypeForQueue);
            if (!any)
            {
                return null;
            }

            bool hasVaultAndName = !string.IsNullOrEmpty(EncryptionKeyVault) && !string.IsNullOrEmpty(EncryptionKeyName);
            if (anyKeyVaultKey && !hasVaultAndName)
            {
                throw new ArgumentException("EncryptionKeyVault and EncryptionKeyName must be specified together.");
            }

            if (anyEncryptionIdentity && !hasVaultAndName)
            {
                throw new ArgumentException(
                    "KeyVaultUserIdentityId and KeyVaultFederatedClientId can only be specified together with EncryptionKeyVault and EncryptionKeyName.");
            }

            // An explicit source wins; otherwise a key vault key implies the key vault source.
            string keySource = string.IsNullOrEmpty(EncryptionKeySource)
                ? anyKeyVaultKey ? "Microsoft.Keyvault" : "Microsoft.Storage"
                : EncryptionKeySource;

            if (keySource.Equals("Microsoft.Keyvault", StringComparison.OrdinalIgnoreCase) && !hasVaultAndName)
            {
                throw new ArgumentException(
                    "EncryptionKeySource 'Microsoft.Keyvault' requires EncryptionKeyVault and EncryptionKeyName.");
            }

            EncryptionServices services = new EncryptionServices
            {
                Table = string.IsNullOrEmpty(EncryptionKeyTypeForTable)
                    ? null
                    : new EncryptionService { Enabled = true, KeyType = EncryptionKeyTypeForTable },
                Queue = string.IsNullOrEmpty(EncryptionKeyTypeForQueue)
                    ? null
                    : new EncryptionService { Enabled = true, KeyType = EncryptionKeyTypeForQueue },
            };
            if (services.Table is null && services.Queue is null)
            {
                services.Blob = new EncryptionService { Enabled = true };
            }

            return new Encryption
            {
                KeySource = keySource,
                Services = services,
                RequireInfrastructureEncryption = ParseBool(RequireInfrastructureEncryption),
                KeyVaultProperties = anyKeyVaultKey
                    ? new KeyVaultProperties
                    {
                        KeyName = NullIfEmpty(EncryptionKeyName),
                        KeyVersion = NullIfEmpty(EncryptionKeyVersion),
                        KeyVaultUri = NullIfEmpty(EncryptionKeyVault),
                    }
                    : null,
                EncryptionIdentity = anyEncryptionIdentity
                    ? new EncryptionIdentity
                    {
                        UserAssignedIdentity = NullIfEmpty(KeyVaultUserIdentityId),
                        FederatedIdentityClientId = NullIfEmpty(KeyVaultFederatedClientId),
                    }
                    : null,
            };
        }

        // UseSubDomainName is meaningful only on update, so only the name is sent on create.
        private CustomDomain? BuildCustomDomain() =>
            string.IsNullOrEmpty(CustomDomainName)
                ? null
                : new CustomDomain { Name = CustomDomainName };

        private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

        private static List<string>? NullIfEmpty(List<string> values) => values.Count == 0 ? null : values;

        // The option name is captured from the caller's expression so a malformed value reports
        // which option was wrong, rather than surfacing a bare FormatException from the parser.
        private static int? ParseInt(string value, [CallerArgumentExpression(nameof(value))] string? name = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return int.TryParse(value, out int parsed)
                ? parsed
                : throw new ArgumentException($"{name} must be a whole number, but was '{value}'.");
        }

        private static bool? ParseBool(string value, [CallerArgumentExpression(nameof(value))] string? name = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return bool.TryParse(value, out bool parsed)
                ? parsed
                : throw new ArgumentException($"{name} must be true or false, but was '{value}'.");
        }
    }
}
