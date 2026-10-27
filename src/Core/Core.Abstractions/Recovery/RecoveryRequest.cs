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
    public string? SessionStyle { get; init; }
    public string? FailureId { get; init; }
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

    public static RecoverySessionSettings Resolve(
        AgentOptions options, RecoveryTrigger trigger, string? styleOverride = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(trigger))
        {
            throw new ArgumentOutOfRangeException(nameof(trigger));
        }

        if (styleOverride is not null)
        {
            var requested = styleOverride.ToLowerInvariant();
            if (requested is not ("oneshot" or "interactive"))
            {
                throw new ArgumentException("agent.recovery.style must be oneshot or interactive.", nameof(options));
            }

            return new(trigger, requested);
        }

        // An invalid stored style is treated as oneshot.
        var style = options.Recovery?.Style?.ToLowerInvariant();
        return new(trigger, style is "interactive" ? "interactive" : "oneshot");
    }
}
