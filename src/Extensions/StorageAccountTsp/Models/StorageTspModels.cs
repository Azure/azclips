// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Generated model classes for the StorageTsp extension.
//
//   Provider:    Microsoft.Storage
//   API version: 2026-04-01
//   Total types: 259
//
// The shape mirrors the swagger definitions with two adjustments:
//   * `allOf` inheritance from the ARM common-types `TrackedResource`/`ProxyResource`
//     is inlined into the concrete class (Id/Name/Type/Location/Tags/SystemData).
//   * Nested types deeper than the walker's traversal budget are typed as
//     `JsonElement?` so callers can still round-trip them via JsonNode.

using System.Text.Json;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models;

/// <summary></summary>
public class OperationListResult
{
    public List<Operation>? Value { get; set; }

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Operation
{
    public string? Name { get; set; }

    public OperationDisplay? Display { get; set; }

    public string? Origin { get; set; }

    public OperationProperties? OperationProperties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class OperationDisplay
{
    public string? Provider { get; set; }

    public string? Resource { get; set; }

    public string? Operation { get; set; }

    public string? Description { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class OperationProperties
{
    public ServiceSpecification? ServiceSpecification { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ServiceSpecification
{
    public List<MetricSpecification>? MetricSpecifications { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class MetricSpecification
{
    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public string? DisplayDescription { get; set; }

    public string? Unit { get; set; }

    public List<Dimension>? Dimensions { get; set; }

    public string? AggregationType { get; set; }

    public bool? FillGapWithZero { get; set; }

    public string? Category { get; set; }

    public string? ResourceIdDimensionNameOverride { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Dimension
{
    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ErrorResponse
{
    public ErrorResponseBody? Error { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ErrorResponseBody
{
    public string? Code { get; set; }

    public string? Message { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobContainer
{
    public ContainerProperties? ContainerProperties { get; set; }

    public string? Name { get; set; }

    public string? Etag { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ContainerProperties
{
    public string? Version { get; set; }

    public bool? Deleted { get; set; }

    public string? DeletedTime { get; set; }

    public int? RemainingRetentionDays { get; set; }

    public string? DefaultEncryptionScope { get; set; }

    public bool? DenyEncryptionScopeOverride { get; set; }

    public string? PublicAccess { get; set; }

    public string? LastModifiedTime { get; set; }

    public string? LeaseStatus { get; set; }

    public string? LeaseState { get; set; }

    public string? LeaseDuration { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }

    public ImmutabilityPolicyProperties? ImmutabilityPolicy { get; set; }

    public LegalHoldProperties? LegalHold { get; set; }

    public bool? HasLegalHold { get; set; }

    public bool? HasImmutabilityPolicy { get; set; }

    public ImmutableStorageWithVersioning? ImmutableStorageWithVersioning { get; set; }

    public bool? EnableNfsV3RootSquash { get; set; }

    public bool? EnableNfsV3AllSquash { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ImmutabilityPolicyProperties
{
    public ImmutabilityPolicyProperty? Properties { get; set; }

    public string? Etag { get; set; }

    public List<UpdateHistoryProperty>? UpdateHistory { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ImmutabilityPolicyProperty
{
    public int? ImmutabilityPeriodSinceCreationInDays { get; set; }

    public string? State { get; set; }

    public bool? AllowProtectedAppendWrites { get; set; }

    public bool? AllowProtectedAppendWritesAll { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class UpdateHistoryProperty
{
    public string? Update { get; set; }

    public int? ImmutabilityPeriodSinceCreationInDays { get; set; }

    public string? Timestamp { get; set; }

    public string? ObjectIdentifier { get; set; }

    public string? TenantId { get; set; }

    public string? Upn { get; set; }

    public bool? AllowProtectedAppendWrites { get; set; }

    public bool? AllowProtectedAppendWritesAll { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LegalHoldProperties
{
    public bool? HasLegalHold { get; set; }

    public List<TagProperty>? Tags { get; set; }

    public ProtectedAppendWritesHistory? ProtectedAppendWritesHistory { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TagProperty
{
    public string? Tag { get; set; }

    public string? Timestamp { get; set; }

    public string? ObjectIdentifier { get; set; }

    public string? TenantId { get; set; }

    public string? Upn { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ProtectedAppendWritesHistory
{
    public bool? AllowProtectedAppendWritesAll { get; set; }

    public string? Timestamp { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ImmutableStorageWithVersioning
{
    public bool? Enabled { get; set; }

    public string? TimeStamp { get; set; }

    public string? MigrationState { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ProxyResource
{
    public string? Value { get; set; }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string?(ProxyResource? v) => v?.Value;
    public static implicit operator ProxyResource(string? v) => new() { Value = v };
}

/// <summary></summary>
public class Resource
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Type { get; set; }

    public SystemData? SystemData { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class SystemData
{
    public string? CreatedBy { get; set; }

    public string? CreatedByType { get; set; }

    public string? CreatedAt { get; set; }

    public string? LastModifiedBy { get; set; }

    public string? LastModifiedByType { get; set; }

    public string? LastModifiedAt { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LegalHold
{
    public bool? HasLegalHold { get; set; }

    public List<string> Tags { get; set; } = [];

    public bool? AllowProtectedAppendWritesAll { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LeaseContainerRequest
{
    public string? Action { get; set; }

    public string? LeaseId { get; set; }

    public int? BreakPeriod { get; set; }

    public int? LeaseDuration { get; set; }

    public string? ProposedLeaseId { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LeaseContainerResponse
{
    public string? LeaseId { get; set; }

    public string? LeaseTimeSeconds { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class CloudError
{
    public CloudErrorBody? Error { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class CloudErrorBody
{
    public string? Code { get; set; }

    public string? Message { get; set; }

    public string? Target { get; set; }

    public List<CloudErrorBody>? Details { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ArmOperationStatusResourceProvisioningState
{
    public string? Status { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? StartTime { get; set; }

    public string? EndTime { get; set; }

    public double? PercentComplete { get; set; }

    public ErrorDetail? Error { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ErrorDetail
{
    public string? Code { get; set; }

    public string? Message { get; set; }

    public string? Target { get; set; }

    public List<ErrorDetail>? Details { get; set; }

    public List<ErrorAdditionalInfo>? AdditionalInfo { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ErrorAdditionalInfo
{
    public string? Type { get; set; }

    public string? Info { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListContainerItems
{
    public List<ListContainerItem> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListContainerItem
{
    public ContainerProperties? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AzureEntityResource
{
    public string? Etag { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ImmutabilityPolicy
{
    public ImmutabilityPolicyProperty Properties { get; set; } = null!;

    public string? Name { get; set; }

    public string? Etag { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobServiceProperties
{
    [System.Text.Json.Serialization.JsonPropertyName("blobServiceProperties")]
    public BlobServicePropertiesProperties? BlobServicePropertiess { get; set; }

    public string? Name { get; set; }

    public Sku? Sku { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobServicePropertiesProperties
{
    public CorsRules? Cors { get; set; }

    public string? DefaultServiceVersion { get; set; }

    public DeleteRetentionPolicy? DeleteRetentionPolicy { get; set; }

    public StaticWebsite? StaticWebsite { get; set; }

    public bool? IsVersioningEnabled { get; set; }

    public bool? AutomaticSnapshotPolicyEnabled { get; set; }

    public ChangeFeed? ChangeFeed { get; set; }

    public RestorePolicyProperties? RestorePolicy { get; set; }

    public DeleteRetentionPolicy? ContainerDeleteRetentionPolicy { get; set; }

    public LastAccessTimeTrackingPolicy? LastAccessTimeTrackingPolicy { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class CorsRules
{
    [System.Text.Json.Serialization.JsonPropertyName("corsRules")]
    public List<CorsRule>? CorsRuless { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class CorsRule
{
    public List<string> AllowedOrigins { get; set; } = [];

    public List<string> AllowedMethods { get; set; } = [];

    public int? MaxAgeInSeconds { get; set; }

    public List<string> ExposedHeaders { get; set; } = [];

    public List<string> AllowedHeaders { get; set; } = [];

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DeleteRetentionPolicy
{
    public bool? Enabled { get; set; }

    public int? Days { get; set; }

    public bool? AllowPermanentDelete { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StaticWebsite
{
    public bool? Enabled { get; set; }

    public string? IndexDocument { get; set; }

    public string? DefaultIndexDocumentPath { get; set; }

    public string? ErrorDocument404Path { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ChangeFeed
{
    public bool? Enabled { get; set; }

    public int? RetentionInDays { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class RestorePolicyProperties
{
    public bool? Enabled { get; set; }

    public int? Days { get; set; }

    public string? LastEnabledTime { get; set; }

    public string? MinRestoreTime { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LastAccessTimeTrackingPolicy
{
    public bool? Enable { get; set; }

    public string? Name { get; set; }

    public int? TrackingGranularityInDays { get; set; }

    public List<string>? BlobType { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Sku
{
    public string? Name { get; set; }

    public string? Tier { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobServiceItems
{
    public List<BlobServiceProperties>? Value { get; set; }

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountCheckNameAvailabilityParameters
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class CheckNameAvailabilityResult
{
    public bool? NameAvailable { get; set; }

    public string? Reason { get; set; }

    public string? Message { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccount
{
    public StorageAccountProperties? Properties { get; set; }

    public string? Name { get; set; }

    public Sku? Sku { get; set; }

    public string? Kind { get; set; }

    public Identity? Identity { get; set; }

    public ExtendedLocation? ExtendedLocation { get; set; }

    public List<string>? Zones { get; set; }

    public Placement? Placement { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountProperties
{
    public string? ProvisioningState { get; set; }

    public Endpoints? PrimaryEndpoints { get; set; }

    public string? PrimaryLocation { get; set; }

    public string? StatusOfPrimary { get; set; }

    public string? LastGeoFailoverTime { get; set; }

    public string? SecondaryLocation { get; set; }

    public string? StatusOfSecondary { get; set; }

    public string? CreationTime { get; set; }

    public CustomDomain? CustomDomain { get; set; }

    public SasPolicy? SasPolicy { get; set; }

    public KeyPolicy? KeyPolicy { get; set; }

    public KeyCreationTime? KeyCreationTime { get; set; }

    public Endpoints? SecondaryEndpoints { get; set; }

    public Encryption? Encryption { get; set; }

    public string? AccessTier { get; set; }

    public AzureFilesIdentityBasedAuthentication? AzureFilesIdentityBasedAuthentication { get; set; }

    public bool? EnableHttpsTrafficOnly { get; set; }

    public NetworkRuleSet? NetworkRuleSet { get; set; }

    public bool? IsSftpEnabled { get; set; }

    public bool? IsLocalUserEnabled { get; set; }

    public bool? EnableExtendedGroups { get; set; }

    public bool? IsHnsEnabled { get; set; }

    public GeoReplicationStats? GeoReplicationStats { get; set; }

    public bool? FailoverInProgress { get; set; }

    public string? LargeFileSharesState { get; set; }

    public List<PrivateEndpointConnection>? PrivateEndpointConnections { get; set; }

    public RoutingPreference? RoutingPreference { get; set; }

    public DualStackEndpointPreference? DualStackEndpointPreference { get; set; }

    public BlobRestoreStatus? BlobRestoreStatus { get; set; }

    public bool? AllowBlobPublicAccess { get; set; }

    public string? MinimumTlsVersion { get; set; }

    public bool? AllowSharedKeyAccess { get; set; }

    public bool? EnableNfsV3 { get; set; }

    public bool? AllowCrossTenantReplication { get; set; }

    public bool? DefaultToOAuthAuthentication { get; set; }

    public string? PublicNetworkAccess { get; set; }

    public ImmutableStorageAccount? ImmutableStorageWithVersioning { get; set; }

    public string? AllowedCopyScope { get; set; }

    public StorageAccountSkuConversionStatus? StorageAccountSkuConversionStatus { get; set; }

    public string? DnsEndpointType { get; set; }

    public bool? IsSkuConversionBlocked { get; set; }

    public bool? AccountMigrationInProgress { get; set; }

    public GeoPriorityReplicationStatus? GeoPriorityReplicationStatus { get; set; }

    public StorageAccountSharedKeyAccessProperties? AllowSharedKeyAccessForServices { get; set; }

    public StorageDataCollaborationPolicyProperties? DataCollaborationPolicyProperties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Endpoints
{
    public string? Blob { get; set; }

    public string? Queue { get; set; }

    public string? Table { get; set; }

    public string? File { get; set; }

    public string? Web { get; set; }

    public string? Dfs { get; set; }

    public StorageAccountMicrosoftEndpoints? MicrosoftEndpoints { get; set; }

    public StorageAccountInternetEndpoints? InternetEndpoints { get; set; }

    public StorageAccountIpv6Endpoints? Ipv6Endpoints { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountMicrosoftEndpoints
{
    public string? Blob { get; set; }

    public string? Queue { get; set; }

    public string? Table { get; set; }

    public string? File { get; set; }

    public string? Web { get; set; }

    public string? Dfs { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountInternetEndpoints
{
    public string? Blob { get; set; }

    public string? File { get; set; }

    public string? Web { get; set; }

    public string? Dfs { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountIpv6Endpoints
{
    public string? Blob { get; set; }

    public string? Queue { get; set; }

    public string? Table { get; set; }

    public string? File { get; set; }

    public string? Web { get; set; }

    public string? Dfs { get; set; }

    public StorageAccountMicrosoftEndpoints? MicrosoftEndpoints { get; set; }

    public StorageAccountInternetEndpoints? InternetEndpoints { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class CustomDomain
{
    public string? Name { get; set; }

    public bool? UseSubDomainName { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class SasPolicy
{
    public string? SasExpirationPeriod { get; set; }

    public string? ExpirationAction { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class KeyPolicy
{
    public int? KeyExpirationPeriodInDays { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class KeyCreationTime
{
    public string? Key1 { get; set; }

    public string? Key2 { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Encryption
{
    public EncryptionServices? Services { get; set; }

    public string? KeySource { get; set; }

    public bool? RequireInfrastructureEncryption { get; set; }

    public KeyVaultProperties? KeyVaultProperties { get; set; }

    public EncryptionIdentity? EncryptionIdentity { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class EncryptionServices
{
    public EncryptionService? Blob { get; set; }

    public EncryptionService? File { get; set; }

    public EncryptionService? Table { get; set; }

    public EncryptionService? Queue { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class EncryptionService
{
    public bool? Enabled { get; set; }

    public string? LastEnabledTime { get; set; }

    public string? KeyType { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class KeyVaultProperties
{
    public string? KeyName { get; set; }

    public string? KeyVersion { get; set; }

    public string? KeyVaultUri { get; set; }

    public string? CurrentVersionedKeyIdentifier { get; set; }

    public string? LastKeyRotationTimestamp { get; set; }

    public string? CurrentVersionedKeyExpirationTimestamp { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class EncryptionIdentity
{
    public string? EncryptionUserAssignedIdentity { get; set; }

    public string? EncryptionFederatedIdentityClientId { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AzureFilesIdentityBasedAuthentication
{
    public string? DirectoryServiceOptions { get; set; }

    public ActiveDirectoryProperties? ActiveDirectoryProperties { get; set; }

    public string? DefaultSharePermission { get; set; }

    public SmbOAuthSettings? SmbOAuthSettings { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
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

/// <summary></summary>
public class SmbOAuthSettings
{
    public bool? IsSmbOAuthEnabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NetworkRuleSet
{
    public string? Bypass { get; set; }

    public List<ResourceAccessRule>? ResourceAccessRules { get; set; }

    public List<VirtualNetworkRule>? VirtualNetworkRules { get; set; }

    public List<IPRule>? IpRules { get; set; }

    public List<IPRule>? Ipv6Rules { get; set; }

    public string? DefaultAction { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ResourceAccessRule
{
    public string? TenantId { get; set; }

    public string? ResourceId { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class VirtualNetworkRule
{
    public string? VirtualNetworkResourceId { get; set; }

    public string? Action { get; set; }

    public string? State { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class IPRule
{
    public string? IPAddressOrRange { get; set; }

    public string? Action { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class GeoReplicationStats
{
    public string? Status { get; set; }

    public string? LastSyncTime { get; set; }

    public bool? CanFailover { get; set; }

    public bool? CanPlannedFailover { get; set; }

    public string? PostFailoverRedundancy { get; set; }

    public string? PostPlannedFailoverRedundancy { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class PrivateEndpointConnection
{
    public PrivateEndpointConnectionProperties? Properties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class PrivateEndpointConnectionProperties
{
    public PrivateEndpoint? PrivateEndpoint { get; set; }

    public PrivateLinkServiceConnectionState PrivateLinkServiceConnectionState { get; set; } = null!;

    public string? ProvisioningState { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class PrivateEndpoint
{
    public string? Id { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class PrivateLinkServiceConnectionState
{
    public string? Status { get; set; }

    public string? Description { get; set; }

    public string? ActionRequired { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class RoutingPreference
{
    public string? RoutingChoice { get; set; }

    public bool? PublishMicrosoftEndpoints { get; set; }

    public bool? PublishInternetEndpoints { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DualStackEndpointPreference
{
    public bool? PublishIpv6Endpoint { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobRestoreStatus
{
    public string? Status { get; set; }

    public string? FailureReason { get; set; }

    public string? RestoreId { get; set; }

    public BlobRestoreParameters? Parameters { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobRestoreParameters
{
    public string? TimeToRestore { get; set; }

    public List<BlobRestoreRange> BlobRanges { get; set; } = [];

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobRestoreRange
{
    public string? StartRange { get; set; }

    public string? EndRange { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ImmutableStorageAccount
{
    public bool? Enabled { get; set; }

    public AccountImmutabilityPolicyProperties? ImmutabilityPolicy { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AccountImmutabilityPolicyProperties
{
    public int? ImmutabilityPeriodSinceCreationInDays { get; set; }

    public string? State { get; set; }

    public bool? AllowProtectedAppendWrites { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountSkuConversionStatus
{
    public string? SkuConversionStatus { get; set; }

    public string? TargetSkuName { get; set; }

    public string? StartTime { get; set; }

    public string? EndTime { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class GeoPriorityReplicationStatus
{
    public bool? IsBlobEnabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountSharedKeyAccessProperties
{
    public ServiceSharedKeyAccessProperties? Blob { get; set; }

    public ServiceSharedKeyAccessProperties? File { get; set; }

    public ServiceSharedKeyAccessProperties? Table { get; set; }

    public ServiceSharedKeyAccessProperties? Queue { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ServiceSharedKeyAccessProperties
{
    public bool? Enabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageDataCollaborationPolicyProperties
{
    public bool? AllowStorageConnectors { get; set; }

    public bool? AllowStorageDataShares { get; set; }

    public bool? AllowCrossTenantDataSharing { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Identity
{
    public string? PrincipalId { get; set; }

    public string? TenantId { get; set; }

    public string? Type { get; set; }

    public Dictionary<string, UserAssignedIdentity>? UserAssignedIdentities { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class UserAssignedIdentity
{
    public string? PrincipalId { get; set; }

    public string? ClientId { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ExtendedLocation
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Placement
{
    public string? ZonePlacementPolicy { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TrackedResource
{
    public Dictionary<string, string>? Tags { get; set; }

    public string? Location { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountCreateParameters
{
    public Sku Sku { get; set; } = null!;

    public string? Kind { get; set; }

    public string? Location { get; set; }

    public ExtendedLocation? ExtendedLocation { get; set; }

    public List<string>? Zones { get; set; }

    public Placement? Placement { get; set; }

    public Dictionary<string, string>? Tags { get; set; }

    public Identity? Identity { get; set; }

    public StorageAccountPropertiesCreateParameters? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountPropertiesCreateParameters
{
    public string? AllowedCopyScope { get; set; }

    public string? PublicNetworkAccess { get; set; }

    public SasPolicy? SasPolicy { get; set; }

    public KeyPolicy? KeyPolicy { get; set; }

    public CustomDomain? CustomDomain { get; set; }

    public Encryption? Encryption { get; set; }

    public NetworkRuleSet? NetworkRuleSet { get; set; }

    public string? AccessTier { get; set; }

    public AzureFilesIdentityBasedAuthentication? AzureFilesIdentityBasedAuthentication { get; set; }

    public bool? EnableHttpsTrafficOnly { get; set; }

    public bool? IsSftpEnabled { get; set; }

    public bool? IsLocalUserEnabled { get; set; }

    public bool? EnableExtendedGroups { get; set; }

    public bool? IsHnsEnabled { get; set; }

    public string? LargeFileSharesState { get; set; }

    public RoutingPreference? RoutingPreference { get; set; }

    public DualStackEndpointPreference? DualStackEndpointPreference { get; set; }

    public bool? AllowBlobPublicAccess { get; set; }

    public string? MinimumTlsVersion { get; set; }

    public bool? AllowSharedKeyAccess { get; set; }

    public bool? EnableNfsV3 { get; set; }

    public bool? AllowCrossTenantReplication { get; set; }

    public bool? DefaultToOAuthAuthentication { get; set; }

    public ImmutableStorageAccount? ImmutableStorageWithVersioning { get; set; }

    public string? DnsEndpointType { get; set; }

    public GeoPriorityReplicationStatus? GeoPriorityReplicationStatus { get; set; }

    public StorageAccountSharedKeyAccessProperties? AllowSharedKeyAccessForServices { get; set; }

    public StorageDataCollaborationPolicyProperties? DataCollaborationPolicyProperties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountUpdateParameters
{
    public Sku? Sku { get; set; }

    public Dictionary<string, string>? Tags { get; set; }

    public Identity? Identity { get; set; }

    public StorageAccountPropertiesUpdateParameters? Properties { get; set; }

    public string? Kind { get; set; }

    public List<string>? Zones { get; set; }

    public Placement? Placement { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountPropertiesUpdateParameters
{
    public CustomDomain? CustomDomain { get; set; }

    public Encryption? Encryption { get; set; }

    public SasPolicy? SasPolicy { get; set; }

    public KeyPolicy? KeyPolicy { get; set; }

    public string? AccessTier { get; set; }

    public AzureFilesIdentityBasedAuthentication? AzureFilesIdentityBasedAuthentication { get; set; }

    public bool? EnableHttpsTrafficOnly { get; set; }

    public bool? IsSftpEnabled { get; set; }

    public bool? IsLocalUserEnabled { get; set; }

    public bool? EnableExtendedGroups { get; set; }

    public NetworkRuleSet? NetworkRuleSet { get; set; }

    public string? LargeFileSharesState { get; set; }

    public RoutingPreference? RoutingPreference { get; set; }

    public DualStackEndpointPreference? DualStackEndpointPreference { get; set; }

    public bool? AllowBlobPublicAccess { get; set; }

    public string? MinimumTlsVersion { get; set; }

    public bool? AllowSharedKeyAccess { get; set; }

    public bool? AllowCrossTenantReplication { get; set; }

    public bool? DefaultToOAuthAuthentication { get; set; }

    public string? PublicNetworkAccess { get; set; }

    public ImmutableStorageAccount? ImmutableStorageWithVersioning { get; set; }

    public string? AllowedCopyScope { get; set; }

    public string? DnsEndpointType { get; set; }

    public GeoPriorityReplicationStatus? GeoPriorityReplicationStatus { get; set; }

    public StorageAccountSharedKeyAccessProperties? AllowSharedKeyAccessForServices { get; set; }

    public StorageDataCollaborationPolicyProperties? DataCollaborationPolicyProperties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountListResult
{
    public List<StorageAccount> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountListKeysResult
{
    public List<StorageAccountKey>? Keys { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountKey
{
    public string? KeyName { get; set; }

    public string? Value { get; set; }

    public string? Permissions { get; set; }

    public string? CreationTime { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountRegenerateKeyParameters
{
    public string? KeyName { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AccountSasParameters
{
    public string? Services { get; set; }

    public string? ResourceTypes { get; set; }

    public string? Permissions { get; set; }

    public string? IPAddressOrRange { get; set; }

    public string? Protocols { get; set; }

    public string? SharedAccessStartTime { get; set; }

    public string? SharedAccessExpiryTime { get; set; }

    public string? KeyToSign { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListAccountSasResponse
{
    public string? AccountSasToken { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ServiceSasParameters
{
    public string? CanonicalizedResource { get; set; }

    public string? Resource { get; set; }

    public string? Permissions { get; set; }

    public string? IPAddressOrRange { get; set; }

    public string? Protocols { get; set; }

    public string? SharedAccessStartTime { get; set; }

    public string? SharedAccessExpiryTime { get; set; }

    public string? Identifier { get; set; }

    public string? PartitionKeyStart { get; set; }

    public string? PartitionKeyEnd { get; set; }

    public string? RowKeyStart { get; set; }

    public string? RowKeyEnd { get; set; }

    public string? KeyToSign { get; set; }

    public string? CacheControl { get; set; }

    public string? ContentDisposition { get; set; }

    public string? ContentEncoding { get; set; }

    public string? ContentLanguage { get; set; }

    public string? ContentType { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListServiceSasResponse
{
    public string? ServiceSasToken { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountMigration
{
    public StorageAccountMigrationProperties StorageAccountMigrationDetails { get; set; } = null!;

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageAccountMigrationProperties
{
    public string? TargetSkuName { get; set; }

    public string? MigrationStatus { get; set; }

    public string? MigrationFailedReason { get; set; }

    public string? MigrationFailedDetailedReason { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileShare
{
    public FileShareProperties? FileShareProperties { get; set; }

    public string? Name { get; set; }

    public string? Etag { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileShareProperties
{
    public string? LastModifiedTime { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }

    public int? ShareQuota { get; set; }

    public int? ProvisionedIops { get; set; }

    public int? ProvisionedBandwidthMibps { get; set; }

    public int? IncludedBurstIops { get; set; }

    public long? MaxBurstCreditsForIops { get; set; }

    public string? NextAllowedQuotaDowngradeTime { get; set; }

    public string? NextAllowedProvisionedIopsDowngradeTime { get; set; }

    public string? NextAllowedProvisionedBandwidthDowngradeTime { get; set; }

    public string? EnabledProtocols { get; set; }

    public string? RootSquash { get; set; }

    public string? Version { get; set; }

    public bool? Deleted { get; set; }

    public string? DeletedTime { get; set; }

    public int? RemainingRetentionDays { get; set; }

    public string? AccessTier { get; set; }

    public string? AccessTierChangeTime { get; set; }

    public string? AccessTierStatus { get; set; }

    public long? ShareUsageBytes { get; set; }

    public string? LeaseStatus { get; set; }

    public string? LeaseState { get; set; }

    public string? LeaseDuration { get; set; }

    public List<SignedIdentifier>? SignedIdentifiers { get; set; }

    public string? SnapshotTime { get; set; }

    public FileSharePropertiesFileSharePaidBursting? FileSharePaidBursting { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class SignedIdentifier
{
    public string? Id { get; set; }

    public AccessPolicy? AccessPolicy { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AccessPolicy
{
    public string? StartTime { get; set; }

    public string? ExpiryTime { get; set; }

    public string? Permission { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileSharePropertiesFileSharePaidBursting
{
    public bool? PaidBurstingEnabled { get; set; }

    public int? PaidBurstingMaxIops { get; set; }

    public int? PaidBurstingMaxBandwidthMibps { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DeletedShare
{
    public string? DeletedShareName { get; set; }

    public string? DeletedShareVersion { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LeaseShareRequest
{
    public string? Action { get; set; }

    public string? LeaseId { get; set; }

    public int? BreakPeriod { get; set; }

    public int? LeaseDuration { get; set; }

    public string? ProposedLeaseId { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LeaseShareResponse
{
    public string? LeaseId { get; set; }

    public string? LeaseTimeSeconds { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileShareItems
{
    public List<FileShareItem> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileShareItem
{
    public FileShareProperties? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileServiceProperties
{
    [System.Text.Json.Serialization.JsonPropertyName("fileServiceProperties")]
    public FileServicePropertiesProperties? FileServicePropertiess { get; set; }

    public string? Name { get; set; }

    public Sku? Sku { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileServicePropertiesProperties
{
    public CorsRules? Cors { get; set; }

    public DeleteRetentionPolicy? ShareDeleteRetentionPolicy { get; set; }

    public ProtocolSettings? ProtocolSettings { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ProtocolSettings
{
    public SmbSetting? Smb { get; set; }

    public NfsSetting? Nfs { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class SmbSetting
{
    public Multichannel? Multichannel { get; set; }

    public string? Versions { get; set; }

    public string? AuthenticationMethods { get; set; }

    public string? KerberosTicketEncryption { get; set; }

    public string? ChannelEncryption { get; set; }

    public EncryptionInTransit? EncryptionInTransit { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Multichannel
{
    public bool? Enabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class EncryptionInTransit
{
    public bool? Required { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NfsSetting
{
    public EncryptionInTransit? EncryptionInTransit { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileServiceItems
{
    public List<FileServiceProperties>? Value { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileServiceUsage
{
    public FileServiceUsageProperties? Properties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileServiceUsageProperties
{
    public AccountLimits? StorageAccountLimits { get; set; }

    public FileShareLimits? FileShareLimits { get; set; }

    public FileShareRecommendations? FileShareRecommendations { get; set; }

    public BurstingConstants? BurstingConstants { get; set; }

    public AccountUsage? StorageAccountUsage { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AccountLimits
{
    public int? MaxFileShares { get; set; }

    public int? MaxProvisionedStorageGiB { get; set; }

    public int? MaxProvisionedIOPS { get; set; }

    public int? MaxProvisionedBandwidthMiBPerSec { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileShareLimits
{
    public int? MinProvisionedStorageGiB { get; set; }

    public int? MaxProvisionedStorageGiB { get; set; }

    public int? MinProvisionedIOPS { get; set; }

    public int? MaxProvisionedIOPS { get; set; }

    public int? MinProvisionedBandwidthMiBPerSec { get; set; }

    public int? MaxProvisionedBandwidthMiBPerSec { get; set; }

    public double? GuardrailIOScalar { get; set; }

    public double? GuardrailBandwidthScalar { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileShareRecommendations
{
    public int? BaseIOPS { get; set; }

    public double? IoScalar { get; set; }

    public int? BaseBandwidthMiBPerSec { get; set; }

    public double? BandwidthScalar { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BurstingConstants
{
    public int? BurstFloorIOPS { get; set; }

    public double? BurstIOScalar { get; set; }

    public int? BurstTimeframeSeconds { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AccountUsage
{
    public AccountUsageElements? LiveShares { get; set; }

    public AccountUsageElements? SoftDeletedShares { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AccountUsageElements
{
    public int? FileShareCount { get; set; }

    public int? ProvisionedStorageGiB { get; set; }

    public int? ProvisionedIOPS { get; set; }

    public int? ProvisionedBandwidthMiBPerSec { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class FileServiceUsages
{
    public List<FileServiceUsage> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class QueueServiceProperties
{
    [System.Text.Json.Serialization.JsonPropertyName("queueServiceProperties")]
    public QueueServicePropertiesProperties? QueueServicePropertiess { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class QueueServicePropertiesProperties
{
    public CorsRules? Cors { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListQueueServices
{
    public List<QueueServiceProperties>? Value { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DeletedAccount
{
    public DeletedAccountProperties? Properties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DeletedAccountProperties
{
    public string? StorageAccountResourceId { get; set; }

    public string? Location { get; set; }

    public string? RestoreReference { get; set; }

    public string? CreationTime { get; set; }

    public string? DeletionTime { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DeletedAccountListResult
{
    public List<DeletedAccount> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagementPolicy
{
    public ManagementPolicyProperties? Properties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagementPolicyProperties
{
    public string? LastModifiedTime { get; set; }

    public ManagementPolicySchema Policy { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagementPolicySchema
{
    public List<ManagementPolicyRule> Rules { get; set; } = [];

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagementPolicyRule
{
    public bool? Enabled { get; set; }

    public string? Name { get; set; }

    public string? Type { get; set; }

    public ManagementPolicyDefinition Definition { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagementPolicyDefinition
{
    public ManagementPolicyAction Actions { get; set; } = null!;

    public ManagementPolicyFilter? Filters { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagementPolicyAction
{
    public ManagementPolicyBaseBlob? BaseBlob { get; set; }

    public ManagementPolicySnapShot? Snapshot { get; set; }

    public ManagementPolicyVersion? Version { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagementPolicyBaseBlob
{
    public DateAfterModification? TierToCool { get; set; }

    public DateAfterModification? TierToArchive { get; set; }

    public DateAfterModification? TierToCold { get; set; }

    public DateAfterModification? TierToHot { get; set; }

    public DateAfterModification? Delete { get; set; }

    public bool? EnableAutoTierToHotFromCool { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DateAfterModification
{
    public float? DaysAfterModificationGreaterThan { get; set; }

    public float? DaysAfterLastAccessTimeGreaterThan { get; set; }

    public float? DaysAfterLastTierChangeGreaterThan { get; set; }

    public float? DaysAfterCreationGreaterThan { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagementPolicySnapShot
{
    public DateAfterCreation? TierToCool { get; set; }

    public DateAfterCreation? TierToArchive { get; set; }

    public DateAfterCreation? TierToCold { get; set; }

    public DateAfterCreation? TierToHot { get; set; }

    public DateAfterCreation? Delete { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DateAfterCreation
{
    public float? DaysAfterCreationGreaterThan { get; set; }

    public float? DaysAfterLastTierChangeGreaterThan { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagementPolicyVersion
{
    public DateAfterCreation? TierToCool { get; set; }

    public DateAfterCreation? TierToArchive { get; set; }

    public DateAfterCreation? TierToCold { get; set; }

    public DateAfterCreation? TierToHot { get; set; }

    public DateAfterCreation? Delete { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagementPolicyFilter
{
    public List<string>? PrefixMatch { get; set; }

    public List<string> BlobTypes { get; set; } = [];

    public List<TagFilter>? BlobIndexMatch { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TagFilter
{
    public string? Name { get; set; }

    public string? Op { get; set; }

    public string? Value { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobInventoryPolicy
{
    public BlobInventoryPolicyProperties? Properties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobInventoryPolicyProperties
{
    public string? LastModifiedTime { get; set; }

    public BlobInventoryPolicySchema Policy { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobInventoryPolicySchema
{
    public bool? Enabled { get; set; }

    public string? Destination { get; set; }

    public string? Type { get; set; }

    public List<BlobInventoryPolicyRule> Rules { get; set; } = [];

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobInventoryPolicyRule
{
    public bool? Enabled { get; set; }

    public string? Name { get; set; }

    public string? Destination { get; set; }

    public BlobInventoryPolicyDefinition Definition { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobInventoryPolicyDefinition
{
    public BlobInventoryPolicyFilter? Filters { get; set; }

    public string? Format { get; set; }

    public string? Schedule { get; set; }

    public string? ObjectType { get; set; }

    public List<string> SchemaFields { get; set; } = [];

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobInventoryPolicyFilter
{
    public List<string>? PrefixMatch { get; set; }

    public List<string>? ExcludePrefix { get; set; }

    public List<string>? BlobTypes { get; set; }

    public bool? IncludeBlobVersions { get; set; }

    public bool? IncludeSnapshots { get; set; }

    public bool? IncludeDeleted { get; set; }

    public BlobInventoryCreationTime? CreationTime { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class BlobInventoryCreationTime
{
    public int? LastNDays { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListBlobInventoryPolicy
{
    public List<BlobInventoryPolicy>? Value { get; set; }

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class PrivateEndpointConnectionListResult
{
    public List<PrivateEndpointConnection>? Value { get; set; }

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class EncryptionScope
{
    public EncryptionScopeProperties? EncryptionScopeProperties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class EncryptionScopeProperties
{
    public string? Source { get; set; }

    public string? State { get; set; }

    public string? CreationTime { get; set; }

    public string? LastModifiedTime { get; set; }

    public EncryptionScopeKeyVaultProperties? KeyVaultProperties { get; set; }

    public bool? RequireInfrastructureEncryption { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class EncryptionScopeKeyVaultProperties
{
    public string? KeyUri { get; set; }

    public string? CurrentVersionedKeyIdentifier { get; set; }

    public string? LastKeyRotationTimestamp { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class EncryptionScopeListResult
{
    public List<EncryptionScope> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TableServiceProperties
{
    [System.Text.Json.Serialization.JsonPropertyName("tableServiceProperties")]
    public TableServicePropertiesProperties? TableServicePropertiess { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TableServicePropertiesProperties
{
    public CorsRules? Cors { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListTableServices
{
    public List<TableServiceProperties>? Value { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NetworkSecurityPerimeterConfiguration
{
    public NetworkSecurityPerimeterConfigurationProperties? Properties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NetworkSecurityPerimeterConfigurationProperties
{
    public string? ProvisioningState { get; set; }

    public List<ProvisioningIssue>? ProvisioningIssues { get; set; }

    public NetworkSecurityPerimeter? NetworkSecurityPerimeter { get; set; }

    public NetworkSecurityPerimeterConfigurationPropertiesResourceAssociation? ResourceAssociation { get; set; }

    public NetworkSecurityPerimeterConfigurationPropertiesProfile? Profile { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ProvisioningIssue
{
    public string? Name { get; set; }

    public ProvisioningIssueProperties? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ProvisioningIssueProperties
{
    public string? IssueType { get; set; }

    public string? Severity { get; set; }

    public string? Description { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NetworkSecurityPerimeter
{
    public string? Id { get; set; }

    public string? PerimeterGuid { get; set; }

    public string? Location { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NetworkSecurityPerimeterConfigurationPropertiesResourceAssociation
{
    public string? Name { get; set; }

    public string? AccessMode { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NetworkSecurityPerimeterConfigurationPropertiesProfile
{
    public string? Name { get; set; }

    public float? AccessRulesVersion { get; set; }

    public List<NspAccessRule>? AccessRules { get; set; }

    public float? DiagnosticSettingsVersion { get; set; }

    public List<string>? EnabledLogCategories { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NspAccessRule
{
    public string? Name { get; set; }

    public NspAccessRuleProperties? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NspAccessRuleProperties
{
    public string? Direction { get; set; }

    public List<string>? AddressPrefixes { get; set; }

    public List<NspAccessRulePropertiesSubscriptionsItem>? Subscriptions { get; set; }

    public List<NetworkSecurityPerimeter>? NetworkSecurityPerimeters { get; set; }

    public List<string>? FullyQualifiedDomainNames { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NspAccessRulePropertiesSubscriptionsItem
{
    public string? Id { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class NetworkSecurityPerimeterConfigurationList
{
    public List<NetworkSecurityPerimeterConfiguration> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskAssignment
{
    public StorageTaskAssignmentProperties? Properties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskAssignmentProperties
{
    public string? TaskId { get; set; }

    public bool? Enabled { get; set; }

    public string? Description { get; set; }

    public StorageTaskAssignmentExecutionContext ExecutionContext { get; set; } = null!;

    public StorageTaskAssignmentReport Report { get; set; } = null!;

    public string? ProvisioningState { get; set; }

    public StorageTaskReportProperties? RunStatus { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskAssignmentExecutionContext
{
    public ExecutionTarget? Target { get; set; }

    public ExecutionTrigger Trigger { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ExecutionTarget
{
    public List<string>? Prefix { get; set; }

    public List<string>? ExcludePrefix { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ExecutionTrigger
{
    public string? Type { get; set; }

    public TriggerParameters Parameters { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TriggerParameters
{
    public string? StartFrom { get; set; }

    public int? Interval { get; set; }

    public string? IntervalUnit { get; set; }

    public string? EndBy { get; set; }

    public string? StartOn { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskAssignmentReport
{
    public string? Prefix { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskReportProperties
{
    public string? TaskAssignmentId { get; set; }

    public string? StorageAccountId { get; set; }

    public string? StartTime { get; set; }

    public string? FinishTime { get; set; }

    public string? ObjectsTargetedCount { get; set; }

    public string? ObjectsOperatedOnCount { get; set; }

    public string? ObjectFailedCount { get; set; }

    public string? ObjectsSucceededCount { get; set; }

    public string? RunStatusError { get; set; }

    public string? RunStatusEnum { get; set; }

    public string? SummaryReportPath { get; set; }

    public string? TaskId { get; set; }

    public string? TaskVersion { get; set; }

    public string? RunResult { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskAssignmentUpdateParameters
{
    public StorageTaskAssignmentUpdateProperties? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskAssignmentUpdateProperties
{
    public string? TaskId { get; set; }

    public bool? Enabled { get; set; }

    public string? Description { get; set; }

    public StorageTaskAssignmentUpdateExecutionContext? ExecutionContext { get; set; }

    public StorageTaskAssignmentUpdateReport? Report { get; set; }

    public string? ProvisioningState { get; set; }

    public StorageTaskReportProperties? RunStatus { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskAssignmentUpdateExecutionContext
{
    public ExecutionTarget? Target { get; set; }

    public ExecutionTriggerUpdate? Trigger { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ExecutionTriggerUpdate
{
    public string? Type { get; set; }

    public TriggerParametersUpdate? Parameters { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TriggerParametersUpdate
{
    public string? StartFrom { get; set; }

    public int? Interval { get; set; }

    public string? IntervalUnit { get; set; }

    public string? EndBy { get; set; }

    public string? StartOn { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskAssignmentUpdateReport
{
    public string? Prefix { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskAssignmentsList
{
    public List<StorageTaskAssignment> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Connector
{
    public StorageConnectorProperties Properties { get; set; } = null!;

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageConnectorProperties
{
    public string? UniqueId { get; set; }

    public string? State { get; set; }

    public string? CreationTime { get; set; }

    public string? Description { get; set; }

    public bool? TestConnection { get; set; }

    public string? DataSourceType { get; set; }

    public StorageConnectorSource Source { get; set; } = null!;

    public string? ProvisioningState { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageConnectorSource
{
    public string? Type { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DataShareSource
{
    public string? Type { get; set; }

    public StorageConnectorConnection Connection { get; set; } = null!;

    public StorageConnectorAuthProperties AuthProperties { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageConnectorConnection
{
    public string? Type { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DataShareConnection
{
    public string? Type { get; set; }

    public string? DataShareUri { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageConnectorAuthProperties
{
    public string? Type { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagedIdentityAuthProperties
{
    public string? Type { get; set; }

    public string? IdentityResourceId { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ConnectorUpdate
{
    public StorageConnectorPropertiesUpdate? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageConnectorPropertiesUpdate
{
    public string? State { get; set; }

    public string? Description { get; set; }

    public bool? TestConnection { get; set; }

    public StorageConnectorSourceUpdate? Source { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageConnectorSourceUpdate
{
    public string? Type { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DataShareSourceUpdate
{
    public string? Type { get; set; }

    public StorageConnectorAuthPropertiesUpdate? AuthProperties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageConnectorAuthPropertiesUpdate
{
    public string? Type { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ManagedIdentityAuthPropertiesUpdate
{
    public string? Type { get; set; }

    public string? IdentityResourceId { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TrackedResourceUpdate
{
    public Dictionary<string, string>? Tags { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ConnectorListResult
{
    public List<Connector> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TestExistingConnectionRequest
{
    public string? UniqueId { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TestConnectionResponse
{
    public string? StorageConnectorMethodName { get; set; }

    public string? StorageConnectorErrorMessage { get; set; }

    public string? StorageConnectorRequestId { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DataShare
{
    public StorageDataShareProperties Properties { get; set; } = null!;

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageDataShareProperties
{
    public string? DataShareIdentifier { get; set; }

    public string? Description { get; set; }

    public string? DataShareUri { get; set; }

    public List<StorageDataShareAccessPolicy> AccessPolicies { get; set; } = [];

    public List<StorageDataShareAsset> Assets { get; set; } = [];

    public string? ProvisioningState { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageDataShareAccessPolicy
{
    public string? PrincipalId { get; set; }

    public string? TenantId { get; set; }

    public string? Permission { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageDataShareAsset
{
    public string? AssetPath { get; set; }

    public string? DisplayName { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DataShareUpdate
{
    public StorageDataSharePropertiesUpdate? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageDataSharePropertiesUpdate
{
    public string? Description { get; set; }

    public List<StorageDataShareAccessPolicy>? AccessPolicies { get; set; }

    public List<StorageDataShareAsset>? Assets { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class DataShareListResult
{
    public List<DataShare> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AdvancedPlatformMetricsRule
{
    public AdvancedPlatformMetricsRuleProperties? Properties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AdvancedPlatformMetricsRuleProperties
{
    public string? RuleType { get; set; }

    public bool? Enabled { get; set; }

    public string? LastModifiedTime { get; set; }

    public List<string>? MetricsEmitted { get; set; }

    public AdvancedPlatformMetricsRuleConfig RuleConfig { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AdvancedPlatformMetricsRuleConfig
{
    public string? FilterType { get; set; }

    public List<string>? FilterValues { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class AdvancedPlatformMetricsRuleListResult
{
    public List<AdvancedPlatformMetricsRule> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class PrivateLinkResourceListResult
{
    public List<PrivateLinkResource>? Value { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class PrivateLinkResource
{
    public PrivateLinkResourceProperties? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class PrivateLinkResourceProperties
{
    public string? GroupId { get; set; }

    public List<string>? RequiredMembers { get; set; }

    public List<string>? RequiredZoneNames { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskReportSummary
{
    public List<StorageTaskReportInstance> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageTaskReportInstance
{
    public StorageTaskReportProperties? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListQueueResource
{
    public List<ListQueue> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListQueue
{
    public ListQueueProperties? QueueProperties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListQueueProperties
{
    public Dictionary<string, string>? Metadata { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageQueue
{
    public QueueProperties? QueueProperties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class QueueProperties
{
    public Dictionary<string, string>? Metadata { get; set; }

    public int? ApproximateMessageCount { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ObjectReplicationPolicy
{
    public ObjectReplicationPolicyProperties? Properties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ObjectReplicationPolicyProperties
{
    public string? PolicyId { get; set; }

    public string? EnabledTime { get; set; }

    public string? SourceAccount { get; set; }

    public string? DestinationAccount { get; set; }

    public List<ObjectReplicationPolicyRule>? Rules { get; set; }

    public ObjectReplicationPolicyPropertiesMetrics? Metrics { get; set; }

    public ObjectReplicationPolicyPropertiesPriorityReplication? PriorityReplication { get; set; }

    public ObjectReplicationPolicyPropertiesTagsReplication? TagsReplication { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ObjectReplicationPolicyRule
{
    public string? RuleId { get; set; }

    public string? SourceContainer { get; set; }

    public string? DestinationContainer { get; set; }

    public ObjectReplicationPolicyFilter? Filters { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ObjectReplicationPolicyFilter
{
    public List<string>? PrefixMatch { get; set; }

    public string? MinCreationTime { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ObjectReplicationPolicyPropertiesMetrics
{
    public bool? Enabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ObjectReplicationPolicyPropertiesPriorityReplication
{
    public bool? Enabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ObjectReplicationPolicyPropertiesTagsReplication
{
    public bool? Enabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ObjectReplicationPolicies
{
    public List<ObjectReplicationPolicy>? Value { get; set; }

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LocalUser
{
    public LocalUserProperties? Properties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LocalUserProperties
{
    public List<PermissionScope>? PermissionScopes { get; set; }

    public string? HomeDirectory { get; set; }

    public List<SshPublicKey>? SshAuthorizedKeys { get; set; }

    public string? Sid { get; set; }

    public bool? HasSharedKey { get; set; }

    public bool? HasSshKey { get; set; }

    public bool? HasSshPassword { get; set; }

    public int? UserId { get; set; }

    public int? GroupId { get; set; }

    public bool? AllowAclAuthorization { get; set; }

    public List<int>? ExtendedGroups { get; set; }

    public bool? IsNFSv3Enabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class PermissionScope
{
    public string? Permissions { get; set; }

    public string? Service { get; set; }

    public string? ResourceName { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class SshPublicKey
{
    public string? Description { get; set; }

    public string? Key { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LocalUsers
{
    public List<LocalUser> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LocalUserKeys
{
    public List<SshPublicKey>? SshAuthorizedKeys { get; set; }

    public string? SharedKey { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class LocalUserRegeneratePasswordResult
{
    public string? SshPassword { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Table
{
    public TableProperties? TableProperties { get; set; }

    public string? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TableProperties
{
    public string? TableName { get; set; }

    public List<TableSignedIdentifier>? SignedIdentifiers { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TableSignedIdentifier
{
    public string? Id { get; set; }

    public TableAccessPolicy? AccessPolicy { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class TableAccessPolicy
{
    public string? StartTime { get; set; }

    public string? ExpiryTime { get; set; }

    public string? Permission { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class ListTableResource
{
    public List<Table> Value { get; set; } = [];

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class StorageSkuListResult
{
    public List<SkuInformation>? Value { get; set; }

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class SkuInformation
{
    public string? Name { get; set; }

    public string? Tier { get; set; }

    public string? ResourceType { get; set; }

    public string? Kind { get; set; }

    public List<string>? Locations { get; set; }

    public List<SkuInformationLocationInfoItem>? LocationInfo { get; set; }

    public List<SKUCapability>? Capabilities { get; set; }

    public List<Restriction>? Restrictions { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class SkuInformationLocationInfoItem
{
    public string? Location { get; set; }

    public List<string>? Zones { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class SKUCapability
{
    public string? Name { get; set; }

    public string? Value { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Restriction
{
    public string? Type { get; set; }

    public List<string>? Values { get; set; }

    public string? ReasonCode { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class UsageListResult
{
    public List<Usage>? Value { get; set; }

    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class Usage
{
    public string? Unit { get; set; }

    public int? CurrentValue { get; set; }

    public int? Limit { get; set; }

    public UsageName? Name { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary></summary>
public class UsageName
{
    public string? Value { get; set; }

    public string? LocalizedValue { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>Common ARM managed service identity envelope. Emitted per-extension.</summary>
public class ManagedServiceIdentity
{
    public string? PrincipalId { get; set; }
    public string? TenantId { get; set; }
    public string Type { get; set; } = "None";
    public Dictionary<string, UserAssignedIdentity>? UserAssignedIdentities { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

