// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Core.Abstractions;

[ConfigurationSection("agent")]
public sealed class AgentOptions
{
    // Off by default. manual saves failures for `az troubleshoot`; auto also starts diagnosis.
    [ConfigurationKey("recovery", Description = "Error recovery: off (default; disable and clear saved failures) | manual (save failures for az troubleshoot) | auto (also start diagnosis after a failure).", AllowedValues = ["off", "manual", "auto"])]
    public string Recovery { get; set; } = "off";

    /// <summary>Number of saved failures kept when recovery is on. Fixed; not a configuration key.</summary>
    public const int RecoveryHistorySize = 10;
    [ConfigurationKey("provider", Description = "Agent model provider: builtin (default, Microsoft's AOAI deployment via APIM, requires 'az login') | github (user's Copilot subscription, requires Copilot login).", AllowedValues = ["builtin", "github"])]
    public string Provider { get; set; } = "builtin";
    public BuiltinProviderOptions Builtin { get; set; } = new();

    // oneshot: agent proposes a fix and exits (az-CLI-like). interactive: agent
    // stays in a chat loop until the user exits or the escape valve trips.
    [ConfigurationKey("sessionStyle", Description = "Agent interaction style: oneshot (propose a fix and exit) | interactive (stay in a chat loop until you exit).", AllowedValues = ["oneshot", "interactive"])]
    public string SessionStyle { get; set; } = "oneshot";

    [ConfigurationKey("reasoningEffort", Description = "Reasoning effort requested from the model for all providers and interaction styles: low | medium | high.", AllowedValues = ["low", "medium", "high"], IsRequired = true)]
    public string ReasoningEffort { get; set; } = "medium";

    // Interactive-only display settings. Meaningless in oneshot (which prints no
    // tool events at all), hence namespaced under interactive.
    public InteractiveOptions Interactive { get; set; } = new();

    // Diagnostic JSONL trace of agent sessions (events, tool I/O, rendered output).
    // Two separate knobs so the path can have a usable default while recording stays
    // off until explicitly enabled (you can't express "off" with a defaulted path
    // alone). Local debugging / evaluation harness only.
    public TraceOptions Trace { get; set; } = new();

    // Derived views are not configuration keys. Keep them out of serialized options as well.

    // Single source of truth for the SessionStyle string comparison, so the
    // "interactive" literal doesn't drift across call sites.
    [JsonIgnore]
    public bool IsInteractive =>
        string.Equals(SessionStyle, "interactive", System.StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsRecoveryEnabled => !string.Equals(ValidatedRecovery(), "off", System.StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsRecoveryAutomatic => string.Equals(ValidatedRecovery(), "auto", System.StringComparison.OrdinalIgnoreCase);

    // File and environment values bypass the config set validation, so an unknown value fails here.
    private string ValidatedRecovery() =>
        Recovery is { } value && (value.Equals("off", System.StringComparison.OrdinalIgnoreCase) ||
                                  value.Equals("manual", System.StringComparison.OrdinalIgnoreCase) ||
                                  value.Equals("auto", System.StringComparison.OrdinalIgnoreCase))
            ? value
            : throw new System.ArgumentException("Configuration 'agent.recovery' must be off, manual, or auto.");

    [JsonIgnore]
    public bool IsBuiltinProvider =>
        string.Equals(Provider, "builtin", System.StringComparison.OrdinalIgnoreCase);

    // Unsupported provider values are neither builtin nor github.
    [JsonIgnore]
    public bool IsGithubProvider =>
        string.Equals(Provider, "github", System.StringComparison.OrdinalIgnoreCase);
}

public sealed class InteractiveOptions
{
    // Off by default — internal tool details (get_command / find_* args and
    // output) stay hidden so the interactive session shows only the shell
    // commands the user cares about. Turn on to debug what the agent is doing.
    [ConfigurationKey("verbose", Description = "When true, interactive agent sessions also show internal tool details (get_command / find_* args and output), not just shell commands. Off by default to keep the session clean.")]
    public bool Verbose { get; set; } = false;
}

public sealed class TraceOptions
{
    // Off by default — recording writes nothing until the user opts in.
    [ConfigurationKey("enabled", Description = "When true, every agent session writes a JSONL trace (one file per session) under agent.trace.path. Off by default. Local debug/evaluation only.")]
    public bool Enabled { get; set; } = false;

    // Where the JSONL trace files go (one file per session). Defaults to
    // ~/.azclips/trace to match schema/docs defaults; the leading ~ is expanded
    // at the point of use. The directory is created on first write if it doesn't exist.
    [ConfigurationKey("path", Description = "Directory for agent JSONL trace files (used when agent.trace.enabled=true). Created on first write if absent.")]
    public string Path { get; set; } = HomePaths.GetDefaultTraceConfigValue();
}

public sealed class BuiltinProviderOptions
{
    /// <summary>
    /// The same ARM token authenticates the builtin model provider and the diagnostic data boundary
    /// lookup. This is deliberately a constant, not a configurable property with a schema key.
    /// </summary>
    public const string ArmScope = "https://management.core.windows.net//.default";

    [ConfigurationKey("endpoint", Description = "Endpoint URL for the builtin provider (APIM in front of AOAI).", IsUri = true)]
    public string Endpoint { get; set; } = "https://azclis-copilot-apim-beta-eus.azure-api.net/azclips/openai/";
    [ConfigurationKey("modelId", Description = "Model deployment name used by the builtin provider.")]
    public string ModelId { get; set; } = "gpt-5.4-mini";
    [ConfigurationKey("apiVersion", Description = "Azure OpenAI API version used by the builtin provider.")]
    public string ApiVersion { get; set; } = "2025-04-01-preview";
}
