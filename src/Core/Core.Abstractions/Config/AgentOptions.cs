// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Core.Abstractions;

[ConfigurationSection("agent")]
public sealed class AgentOptions
{
    [ConfigurationKey("provider", Description = "Agent model provider: builtin (default, Microsoft's AOAI deployment via APIM, requires 'az login') | github (user's Copilot subscription, requires Copilot login).", AllowedValues = ["builtin", "github"])]
    public string Provider { get; set; } = "builtin";
    [ConfigurationSection("builtin", IncludeInSchema = false)]
    public BuiltinProviderOptions Builtin { get; set; } = new();
    public RecoveryOptions Recovery { get; set; } = new();

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

    [JsonIgnore]
    public bool IsBuiltinProvider =>
        string.Equals(Provider, "builtin", System.StringComparison.OrdinalIgnoreCase);

    // Unsupported provider values are neither builtin nor github.
    [JsonIgnore]
    public bool IsGithubProvider =>
        string.Equals(Provider, "github", System.StringComparison.OrdinalIgnoreCase);
}

public sealed class RecoveryOptions
{
    [ConfigurationKey("trigger", Description = "Recovery trigger: off (disable and clear history), manual (save failures for troubleshoot), or auto (also start automatic diagnosis).", AllowedValues = ["off", "manual", "auto"])]
    public string Trigger { get; set; } = "off";

    [ConfigurationKey("style", Description = "Recovery interaction style for both manual and automatic diagnosis: oneshot or interactive.", AllowedValues = ["oneshot", "interactive"])]
    public string Style { get; set; } = "oneshot";

    [ConfigurationKey("historySize", Description = "Maximum encrypted failure records retained. Must be positive; reductions take effect on the next successful history write.", Minimum = 1)]
    public int HistorySize { get; set; } = 10;

    [JsonIgnore]
    public bool IsEnabled => IsAutomatic || string.Equals(Trigger, "manual", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsAutomatic => string.Equals(Trigger, "auto", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsInteractive => string.Equals(Style, "interactive", StringComparison.OrdinalIgnoreCase);
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

    // Where the JSONL trace files go (one file per session). Defaults to the
    // agent/trace directory under the local app data root; a leading ~ in a user value
    // is expanded at the point of use. The directory is created on first write if it doesn't exist.
    [ConfigurationKey("path", Description = "Directory for agent JSONL trace files (used when agent.trace.enabled=true). Created on first write if absent.")]
    public string Path { get; set; } = HomePaths.GetDefaultAgentTraceDirectory();
}

public sealed class BuiltinProviderOptions
{
    /// <summary>
    /// The same ARM token authenticates the builtin model provider and the diagnostic data boundary
    /// lookup. This is deliberately a constant, not a configurable property with a schema key.
    /// </summary>
    public const string ArmScope = "https://management.core.windows.net//.default";

    [ConfigurationKey("modelId", Description = "Model deployment name used by the builtin provider.")]
    public string ModelId { get; set; } = "gpt-5.4-mini";
    [ConfigurationKey("apiVersion", Description = "Azure OpenAI API version used by the builtin provider.")]
    public string ApiVersion { get; set; } = "2025-04-01-preview";
}
