// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions;

/// <summary>
/// Tag keys emitted on Azclips telemetry activities.
/// </summary>
public static class TelemetryTags
{
    public const int Version = 1;
    // Per-process invariants.
    public const string InstallationId = "InstallationId";
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
    public const string AuthType = "authType";
    public const string CloudName = "cloudName";
    public const string Mode = "mode";
    /// <summary>ARM data boundary (EU/Global) resolved for the invocation's profile. Tagged
    /// directly on the root activity (see ITelemetryService.AddTag) rather than read from
    /// ambient async-local state, so it stays correct even when resolved from a different
    /// async flow than the one that created/disposes the root (e.g. PowerShell's Run()).</summary>
    public const string DataBoundary = "dataBoundary";
    /// <summary>cold or warm — whether this was the first module import in the process.</summary>
    public const string ImportKind = "importKind";

    // todo: reserved for later product level instrumentation.
    public const string PropertyTagPrefix = "properties.";

    // AI error-recovery. All use PropertyTagPrefix, which DefaultTelemetryPropertyFilter
    // admits as the vetted product channel. Names, counts, and outcomes only — never
    // prompts, error text, or model output.
    public const string CommandTraceId = PropertyTagPrefix + "command.traceId";
    public const string CommandSpanId = PropertyTagPrefix + "command.spanId";
    public const string RecoverySessionStyle = PropertyTagPrefix + "recovery.sessionStyle";
    public const string RecoveryProvider = PropertyTagPrefix + "recovery.provider";
    public const string RecoveryExitCode = PropertyTagPrefix + "recovery.exitCode";
    public const string RecoveryReprompts = PropertyTagPrefix + "recovery.reprompts";
    public const string RecoveryTrigger = PropertyTagPrefix + "recovery.trigger";
    public const string RecoveryRequestTraceId = PropertyTagPrefix + "recovery.requestTraceId";
    public const string RecoveryRequestSpanId = PropertyTagPrefix + "recovery.requestSpanId";
    public const string AgentModel = PropertyTagPrefix + "agent.model";
    public const string ProcessName = PropertyTagPrefix + "process";
}
