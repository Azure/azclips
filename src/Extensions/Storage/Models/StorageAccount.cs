// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    public Dictionary<string, string>? Tags { get; set; }
    public StorageAccountProperties? Properties { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

// Subset of the response properties worth surfacing as command output. The service
// returns considerably more; unmodelled fields are ignored during deserialization.
public class StorageAccountProperties
{
    public string? ProvisioningState { get; set; }
    public string? PrimaryLocation { get; set; }
    public string? StatusOfPrimary { get; set; }
    public string? SecondaryLocation { get; set; }
    public string? StatusOfSecondary { get; set; }
    public string? AccessTier { get; set; }
    public DateTime? CreationTime { get; set; }
    public Endpoints? PrimaryEndpoints { get; set; }
    public Endpoints? SecondaryEndpoints { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

// Request shape for the create operation, distinct from the read-only-heavy StorageAccount.
public class StorageAccountCreateParameters
{
    public Sku Sku { get; set; } = null!;
    public string Kind { get; set; } = null!;
    public string Location { get; set; } = null!;
    public Dictionary<string, string>? Tags { get; set; }
    public StorageAccountPropertiesCreateParameters? Properties { get; set; }
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
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class Sku
{
    public string Name { get; set; } = null!;

    // Read-only: derived by the service from Name, so it must never be sent on create.
    public string? Tier { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}
