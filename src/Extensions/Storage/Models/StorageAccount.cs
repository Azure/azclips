// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Models;

public class StorageAccount
{
    // Fully qualified resource ID. Response-only.
    public string? Id { get; set; }

    // The name of the storage account. Response-only.
    public string? Name { get; set; }

    // The resource type, Microsoft.Storage/storageAccounts. Response-only.
    public string? Type { get; set; }

    public string Kind { get; set; } = null!;
    public string Location { get; set; } = null!;
    public Sku? Sku { get; set; }

    // Key/value pairs that describe the resource.
    public Dictionary<string, string>? Tags { get; set; }

    public StorageAccountProperties? Properties { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

// Properties of the storage account. All members are response-only; the service
// derives them from the create request.
public class StorageAccountProperties
{
    // Status of the account at the time the operation was called: Creating, ResolvingDNS or Succeeded.
    public string? ProvisioningState { get; set; }

    // Location of the primary data center for the storage account.
    public string? PrimaryLocation { get; set; }

    // Whether the primary location is available or unavailable.
    public string? StatusOfPrimary { get; set; }

    // Location of the geo-replicated secondary. Only set for Standard_GRS and Standard_RAGRS.
    public string? SecondaryLocation { get; set; }

    // Whether the secondary location is available or unavailable.
    public string? StatusOfSecondary { get; set; }

    // The access tier used for billing: Hot, Cool, Premium, Cold or Smart.
    public string? AccessTier { get; set; }

    // Creation date and time of the storage account in UTC.
    public DateTime? CreationTime { get; set; }

    // URLs used to retrieve a public blob, queue, or table object.
    public Endpoints? PrimaryEndpoints { get; set; }

    // URLs for the secondary location. Only set when the SKU name is Standard_RAGRS.
    public Endpoints? SecondaryEndpoints { get; set; }

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

    // The SKU tier, derived by the service from the SKU name. Response-only.
    public string? Tier { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}