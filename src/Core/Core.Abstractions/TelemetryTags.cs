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
    public const string InstallationId = "installationId";
    public const string ProductName = "productName";
    public const string ProductVersion = "productVersion";
    public const string SchemaVersion = "schemaVersion";
    public const string TelemetryVersion = "telemetryVersion";
    public const string CommandOutputType = "commandOutputType";
    public const string ExceptionType = "exceptionType";
    public const string OperatingSystemType = "osType";
    public const string OperatingSystemVersion = "osVersion";
    public const string DistributionName = "distributionName";
    public const string DistributionId = "distributionId";
    public const string ShellType = "shellType";
    public const string Locale = "locale";
    public const string DotNetVersion = "dotnetVersion";
    public const string PowerShellVersion = "powerShellVersion";

    // Per-command tags (set on the per-invocation activity).
    public const string CommandName = "commandName";
    public const string CommandExtensionName = "commandExtensionName";
    public const string CommandExtensionVersion = "commandExtensionVersion";
    public const string CommandParameters = "commandParameters";
    public const string SubscriptionId = "subscriptionId";
    public const string TenantId = "tenantId";
    public const string EntraObjectId = "entraObjectId";
    public const string AuthType = "authType";
    public const string AuthSuccess = "authSuccess";
    public const string AuthStatusCode = "authStatusCode";
    public const string AuthDurationTotalMs = "authDurationTotalMs";
    public const string ClientRequestId = "clientRequestId";
    public const string Mode = "mode";
    /// <summary>Resolved ARM boundary, distinct from the fallback routing outcome.</summary>
    public const string DataBoundary = "dataBoundary";
    public const string DataBoundaryOutcome = "dataBoundaryOutcome";
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
    /// <summary>
    /// Wall-clock cost of acquiring the caller's Azure token, recorded on success and failure alike.
    /// One acquisition serves both the model provider and the data boundary lookup. Absent for any
    /// provider other than builtin, which is the only one needing an Azure token.
    /// </summary>
    public const string AgentTokenElapsedMs = PropertyTagPrefix + "agent.tokenElapsedMs";
    public const string RecoveryExitCode = PropertyTagPrefix + "recovery.exitCode";
    public const string RecoveryReprompts = PropertyTagPrefix + "recovery.reprompts";
    public const string RecoveryTrigger = PropertyTagPrefix + "recovery.trigger";
    public const string AgentModel = PropertyTagPrefix + "agent.model";
    public const string AgentModelSource = PropertyTagPrefix + "agent.modelSource";
    public const string ProcessName = PropertyTagPrefix + "process";
    public const string AgentToolName = PropertyTagPrefix + "agent.tool.name";
    public const string AgentToolRequest = PropertyTagPrefix + "agent.tool.request";
    public const string AgentToolResponse = PropertyTagPrefix + "agent.tool.response";

    /// <summary>ok, cancelled, error, or unknown — the terminal Agent.Session outcome.</summary>
    public const string RecoveryOutcome = PropertyTagPrefix + "recovery.outcome";
    /// <summary>
    /// Why recovery ended in error; absent otherwise. A closed classification only — see
    /// <c>RecoveryFailure</c> for the value list. Never the message, which can echo subscription
    /// ids, resource names, or the user's own input.
    /// </summary>
    public const string RecoveryFailureReason = PropertyTagPrefix + "recovery.failureReason";
    /// <summary>Why recovery did not run, on CLI.Activity.AiRecoverySkipped.</summary>
    public const string RecoverySkipReason = PropertyTagPrefix + "recovery.skipReason";
}
