// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

public enum RecoveryTrigger
{
    Automatic,
    Manual,
}

public sealed record RecoveryRequest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public RecoveryTrigger Trigger { get; init; } = RecoveryTrigger.Manual;
    public required FailureRecord Failure { get; init; }
    public string? RequestTraceId { get; init; }
    public string? RequestSpanId { get; init; }
    public string? TenantId { get; init; }
    public string? DataBoundary { get; init; }
}

public sealed record RecoverySessionSettings
{
    public RecoveryTrigger Trigger { get; }
    public string SessionStyle { get; }
    public bool IsInteractive => SessionStyle == "interactive";

    private RecoverySessionSettings(RecoveryTrigger trigger, string sessionStyle)
    {
        Trigger = trigger;
        SessionStyle = sessionStyle;
    }

    public static RecoverySessionSettings Resolve(AgentOptions options, RecoveryTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(trigger))
        {
            throw new ArgumentOutOfRangeException(nameof(trigger));
        }

        if (trigger == RecoveryTrigger.Manual)
        {
            return new(trigger, "oneshot");
        }

        var style = options.SessionStyle?.ToLowerInvariant();
        if (style is not ("oneshot" or "interactive"))
        {
            throw new ArgumentException("agent.sessionStyle must be oneshot or interactive.", nameof(options));
        }

        return new(trigger, style);
    }
}
