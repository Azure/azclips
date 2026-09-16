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
    public const string Mode = "mode";
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
    /// <summary>eu, global, unknown, or not_applicable — the caller tenant's ARM data boundary.</summary>
    /// <remarks>
    /// <c>not_applicable</c> is emitted for the github provider, whose traffic never reaches the
    /// Microsoft endpoint, so the lookup is skipped rather than attempted. It is a telemetry state,
    /// not a boundary: consumers must not treat it, or <c>unknown</c>, as <c>global</c>.
    /// </remarks>
    public const string RecoveryDataBoundary = PropertyTagPrefix + "recovery.dataBoundary";
    /// <summary>
    /// Wall-clock cost of acquiring the caller's Azure token, recorded on success and failure alike.
    /// One acquisition serves both the model provider and the ARM data boundary lookup, so this is
    /// the shared startup cost rather than something either one owns. Absent for the github
    /// provider, which needs no Azure token.
    /// </summary>
    public const string RecoveryTokenElapsedMs = PropertyTagPrefix + "recovery.tokenElapsedMs";
    /// <summary>
    /// Wall-clock cost of the data boundary lookup: profile read, ARM call, parse. Excludes the
    /// token, which is acquired by the caller and timed by
    /// <see cref="RecoveryTokenElapsedMs"/> — pair the two for the full startup probe.
    /// </summary>
    public const string RecoveryDataBoundaryElapsedMs = PropertyTagPrefix + "recovery.dataBoundary.elapsedMs";
    /// <summary>
    /// Wall-clock cost of the ARM request alone, recorded on success and failure alike. Absent when the
    /// lookup returned before reaching ARM (no tenant, or no token). Subtracting it from
    /// <see cref="RecoveryDataBoundaryElapsedMs"/> leaves the local profile read.
    /// </summary>
    public const string RecoveryDataBoundaryArmElapsedMs = PropertyTagPrefix + "recovery.dataBoundary.armElapsedMs";
    /// <summary>Short, non-sensitive reason the boundary came back unknown; absent otherwise.</summary>
    public const string RecoveryDataBoundaryFailureReason = PropertyTagPrefix + "recovery.dataBoundary.failureReason";
    public const string RecoveryExitCode = PropertyTagPrefix + "recovery.exitCode";
    public const string RecoveryReprompts = PropertyTagPrefix + "recovery.reprompts";
    public const string AgentModel = PropertyTagPrefix + "agent.model";
    public const string ProcessName = PropertyTagPrefix + "process";
}
