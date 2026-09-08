// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Models;

// Response shape. Most members are marked read-only by the service, so this type is
// never used for a create request; see StorageAccountCreateParameters.
public class StorageAccount
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string Kind { get; set; } = null!;
    public string Location { get; set; } = null!;
    public Sku? Sku { get; set; }
    public Identity? Identity { get; set; }
    public ExtendedLocation? ExtendedLocation { get; set; }
    public List<string>? Zones { get; set; }
    public Placement? Placement { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public StorageAccountProperties? Properties { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

// Mirrors what the create command can set, so every supplied option is visible in the output,
// plus the service-assigned status fields. The service returns more than this; unmodelled
// fields are ignored during deserialization.
public class StorageAccountProperties
{
    public string? ProvisioningState { get; set; }
    public string? PrimaryLocation { get; set; }
    public string? StatusOfPrimary { get; set; }
    public string? SecondaryLocation { get; set; }
    public string? StatusOfSecondary { get; set; }
    public DateTime? CreationTime { get; set; }
    public Endpoints? PrimaryEndpoints { get; set; }
    public Endpoints? SecondaryEndpoints { get; set; }
    public string? AccessTier { get; set; }
    public string? MinimumTlsVersion { get; set; }
    public string? PublicNetworkAccess { get; set; }
    public string? AllowedCopyScope { get; set; }
    public string? LargeFileSharesState { get; set; }
    public string? DnsEndpointType { get; set; }
    public bool? SupportsHttpsTrafficOnly { get; set; }
    public bool? AllowBlobPublicAccess { get; set; }
    public bool? AllowSharedKeyAccess { get; set; }
    public bool? IsHnsEnabled { get; set; }
    public bool? IsSftpEnabled { get; set; }
    public bool? IsLocalUserEnabled { get; set; }
    public bool? EnableExtendedGroups { get; set; }
    public bool? IsNfsV3Enabled { get; set; }
    public bool? AllowCrossTenantReplication { get; set; }
    public bool? DefaultToOAuthAuthentication { get; set; }
    public RoutingPreference? RoutingPreference { get; set; }
    public DualStackEndpointPreference? DualStackEndpointPreference { get; set; }
    public GeoPriorityReplicationStatus? GeoPriorityReplicationStatus { get; set; }
    public KeyPolicy? KeyPolicy { get; set; }
    public SasPolicy? SasPolicy { get; set; }
    public ImmutableStorageAccount? ImmutableStorageWithVersioning { get; set; }
    public NetworkRuleSet? NetworkAcls { get; set; }
    public AzureFilesIdentityBasedAuthentication? AzureFilesIdentityBasedAuthentication { get; set; }
    public Encryption? Encryption { get; set; }
    public CustomDomain? CustomDomain { get; set; }

    // Only populated when show requests it via $expand. The sibling blobRestoreStatus expansion is
    // deliberately not modelled: it reports progress of a blob-range restore started by
    // `az storage blob restore` / Restore-AzStorageBlobRange, which this extension does not ship.
    public GeoReplicationStats? GeoReplicationStats { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

// Read-only throughout; returned only when show passes $expand=geoReplicationStats.
public class GeoReplicationStats
{
    public string? Status { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public bool? CanFailover { get; set; }
    public bool? CanPlannedFailover { get; set; }
    public string? PostFailoverRedundancy { get; set; }
    public string? PostPlannedFailoverRedundancy { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

// ARM list envelope. Both list operations return this shape; nextLink drives pagination.
public class StorageAccountListResult
{
    public StorageAccount[]? Value { get; set; }
    public string? NextLink { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

// Request shape for the create operation, distinct from the read-only-heavy StorageAccount.
public class StorageAccountCreateParameters
{
    public Sku Sku { get; set; } = null!;
    public string Kind { get; set; } = null!;
    public string Location { get; set; } = null!;
    public Dictionary<string, string>? Tags { get; set; }
    public ExtendedLocation? ExtendedLocation { get; set; }
    public Identity? Identity { get; set; }
    public List<string>? Zones { get; set; }
    public Placement? Placement { get; set; }
    public StorageAccountPropertiesCreateParameters? Properties { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ExtendedLocation
{
    public string? Name { get; set; }

    // EdgeZone is the only value the service defines, so the processor sets it rather than
    // exposing a flag for it.
    public string? Type { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class Placement
{
    public string? ZonePlacementPolicy { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class Identity
{
    public string? PrincipalId { get; set; }
    public string? TenantId { get; set; }
    public string? Type { get; set; }

    // Keyed by the ARM resource id of each identity; the service expects an empty object as the
    // value, which it populates with principalId/clientId on the response.
    public Dictionary<string, object>? UserAssignedIdentities { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

// Every member is optional and nullable: JsonOptions omits nulls, so an unset member is
// absent from the request and the service default applies. Sending an explicit value
// would override defaults that differ from the CLR default (e.g. SupportsHttpsTrafficOnly
// defaults to true).
public class StorageAccountPropertiesCreateParameters
{
    public string? AccessTier { get; set; }
    public string? MinimumTlsVersion { get; set; }
    public string? PublicNetworkAccess { get; set; }
    public string? AllowedCopyScope { get; set; }
    public string? LargeFileSharesState { get; set; }
    public string? DnsEndpointType { get; set; }
    public bool? SupportsHttpsTrafficOnly { get; set; }
    public bool? AllowBlobPublicAccess { get; set; }
    public bool? AllowSharedKeyAccess { get; set; }
    public bool? IsHnsEnabled { get; set; }
    public bool? IsSftpEnabled { get; set; }
    public bool? IsLocalUserEnabled { get; set; }
    public bool? EnableExtendedGroups { get; set; }
    public bool? IsNfsV3Enabled { get; set; }
    public bool? AllowCrossTenantReplication { get; set; }
    public bool? DefaultToOAuthAuthentication { get; set; }
    public RoutingPreference? RoutingPreference { get; set; }
    public DualStackEndpointPreference? DualStackEndpointPreference { get; set; }
    public GeoPriorityReplicationStatus? GeoPriorityReplicationStatus { get; set; }
    public KeyPolicy? KeyPolicy { get; set; }
    public SasPolicy? SasPolicy { get; set; }
    public ImmutableStorageAccount? ImmutableStorageWithVersioning { get; set; }
    public NetworkRuleSet? NetworkAcls { get; set; }
    public CustomDomain? CustomDomain { get; set; }
    public AzureFilesIdentityBasedAuthentication? AzureFilesIdentityBasedAuthentication { get; set; }
    public Encryption? Encryption { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class RoutingPreference
{
    public string? RoutingChoice { get; set; }
    public bool? PublishMicrosoftEndpoints { get; set; }
    public bool? PublishInternetEndpoints { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class DualStackEndpointPreference
{
    public bool? PublishIpv6Endpoint { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class GeoPriorityReplicationStatus
{
    public bool? IsBlobEnabled { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class KeyPolicy
{
    public int? KeyExpirationPeriodInDays { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SasPolicy
{
    public string? SasExpirationPeriod { get; set; }
    public string? ExpirationAction { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ImmutableStorageAccount
{
    public bool? Enabled { get; set; }
    public AccountImmutabilityPolicyProperties? ImmutabilityPolicy { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkRuleSet
{
    // A comma-separated combination of Logging, Metrics and AzureServices, or None.
    public string? Bypass { get; set; }
    public List<VirtualNetworkRule>? VirtualNetworkRules { get; set; }
    public List<IPRule>? IpRules { get; set; }
    public List<IPRule>? Ipv6Rules { get; set; }
    public string? DefaultAction { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class IPRule
{
    // Serializes as "value"; the Azure SDKs alias this as ipAddressOrRange.
    public string? Value { get; set; }
    public string? Action { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualNetworkRule
{
    // Serializes as "id"; the Azure SDKs alias this as virtualNetworkResourceId.
    public string? Id { get; set; }
    public string? Action { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class AccountImmutabilityPolicyProperties
{
    public int? ImmutabilityPeriodSinceCreationInDays { get; set; }
    public string? State { get; set; }
    public bool? AllowProtectedAppendWrites { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class AzureFilesIdentityBasedAuthentication
{
    public string? DirectoryServiceOptions { get; set; }
    public ActiveDirectoryProperties? ActiveDirectoryProperties { get; set; }
    public string? DefaultSharePermission { get; set; }
    public SmbOAuthSettings? SmbOAuthSettings { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ActiveDirectoryProperties
{
    public string? DomainName { get; set; }
    public string? NetBiosDomainName { get; set; }
    public string? ForestName { get; set; }
    public string? DomainGuid { get; set; }
    public string? DomainSid { get; set; }
    public string? AzureStorageSid { get; set; }
    public string? SamAccountName { get; set; }
    public string? AccountType { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SmbOAuthSettings
{
    public bool? IsSmbOAuthEnabled { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

// The service spells several of these members in lower case rather than camel case, so they are
// mapped explicitly; the Azure SDKs hide this behind friendlier aliases.
public class Encryption
{
    public EncryptionServices? Services { get; set; }
    public string? KeySource { get; set; }
    public bool? RequireInfrastructureEncryption { get; set; }

    [JsonPropertyName("keyvaultproperties")]
    public KeyVaultProperties? KeyVaultProperties { get; set; }

    [JsonPropertyName("identity")]
    public EncryptionIdentity? EncryptionIdentity { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class EncryptionServices
{
    public EncryptionService? Blob { get; set; }
    public EncryptionService? File { get; set; }
    public EncryptionService? Table { get; set; }
    public EncryptionService? Queue { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class EncryptionService
{
    public bool? Enabled { get; set; }
    public string? KeyType { get; set; }
    public DateTime? LastEnabledTime { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class KeyVaultProperties
{
    [JsonPropertyName("keyname")]
    public string? KeyName { get; set; }

    [JsonPropertyName("keyversion")]
    public string? KeyVersion { get; set; }

    [JsonPropertyName("keyvaulturi")]
    public string? KeyVaultUri { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class EncryptionIdentity
{
    public string? UserAssignedIdentity { get; set; }
    public string? FederatedIdentityClientId { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class CustomDomain
{
    public string? Name { get; set; }

    // Only meaningful on update, so no flag is exposed on create.
    public bool? UseSubDomainName { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class Endpoints
{
    public string? Blob { get; set; }
    public string? Queue { get; set; }
    public string? Table { get; set; }
    public string? File { get; set; }
    public string? Web { get; set; }
    public string? Dfs { get; set; }

    // Populated when a routing preference publishes the corresponding endpoint set.
    public Endpoints? MicrosoftEndpoints { get; set; }
    public Endpoints? InternetEndpoints { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class Sku
{
    public string Name { get; set; } = null!;

    // Read-only: derived by the service from Name, so it must never be sent on create.
    public string? Tier { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}
