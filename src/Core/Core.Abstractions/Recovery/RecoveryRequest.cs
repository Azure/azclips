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
    public const int CurrentSchemaVersion = 3;
    public const string InputPipeArgument = "--recovery-input";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public RecoveryTrigger Trigger { get; init; } = RecoveryTrigger.Manual;
    public required string FailedCommand { get; init; }
    public required string ErrorText { get; init; }
    public string? Style { get; init; }
    public string? FailureId { get; init; }
    public string? CommandTraceId { get; init; }
    public string? CommandSpanId { get; init; }
    public string? RequestTraceId { get; init; }
    public string? RequestSpanId { get; init; }
    public string? TenantId { get; init; }
    public string? DataBoundary { get; init; }
}

public sealed record RecoverySessionSettings
{
    public RecoveryTrigger Trigger { get; }
    public string Style { get; }
    public bool IsInteractive => Style == "interactive";

    private RecoverySessionSettings(RecoveryTrigger trigger, string style)
    {
        Trigger = trigger;
        Style = style;
    }

    public static RecoverySessionSettings Resolve(
        AgentOptions options, RecoveryTrigger trigger, string? styleOverride = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(trigger))
        {
            throw new ArgumentOutOfRangeException(nameof(trigger));
        }

        var style = (styleOverride ?? options.Recovery?.Style)?.ToLowerInvariant();
        if (style is not ("oneshot" or "interactive"))
        {
            throw new ArgumentException("agent.recovery.style must be oneshot or interactive.", nameof(options));
        }

        return new(trigger, style);
    }
}
