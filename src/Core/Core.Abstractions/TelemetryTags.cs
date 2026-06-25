// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions;

/// <summary>
/// Tag keys emitted on Cirrus telemetry activities.
/// </summary>
public static class TelemetryTags
{
    public const int Version = 1;
    // Per-process invariants.
    public const string Id = "Id";
    public const string IsMicrosoftInternal = "IsMicrosoftInternal";
    public const string ProductName = "productName";
    public const string ProductVersion = "productVersion";
    public const string SchemaVersion = "schemaVersion";
    public const string TelemetryVersion = "telemetryVersion";
    public const string ClientType = "clientType";
    public const string EventName = "eventName";

    // Per-command tags (set on the per-invocation activity).
    public const string Command = "command";
    public const string ModuleName = "module.name";
    public const string ModuleVersion = "module.version";
    public const string Parameter = "parameter";
    public const string SubscriptionId = "subscriptionId";
    public const string TenantId = "tenantId";

    // todo: reserved for later product level instrumentation.
    public const string PropertyTagPrefix = "properties.";
}
