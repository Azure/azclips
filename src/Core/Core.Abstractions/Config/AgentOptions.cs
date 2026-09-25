// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

[ConfigurationSection("agent")]
public sealed class AgentOptions
{
    // Off by default for Private Preview. The user opts in with
    // `az config set --key agent.enabled --value true`; until then a failed
    // command surfaces its original error and never spawns the agent.
    [ConfigurationKey("enabled", Description = "Enable or disable agent features. Off by default; set true to opt in.")]
    public bool Enabled { get; set; } = false;
    [ConfigurationKey("provider", Description = "Agent model provider: builtin (default, Microsoft's AOAI deployment via APIM, requires 'az login') | github (user's Copilot subscription, requires Copilot login).", AllowedValues = ["builtin", "github"])]
    public string Provider { get; set; } = "builtin";
    public BuiltinProviderOptions Builtin { get; set; } = new();

    // oneshot: agent proposes a fix and exits (az-CLI-like). interactive: agent
    // stays in a chat loop until the user exits or the escape valve trips.
    [ConfigurationKey("sessionStyle", Description = "Agent interaction style: oneshot (propose a fix and exit) | interactive (stay in a chat loop until you exit).", AllowedValues = ["oneshot", "interactive"])]
    public string SessionStyle { get; set; } = "oneshot";

    // Interactive-only display settings. Meaningless in oneshot (which prints no
    // tool events at all), hence namespaced under interactive.
    public InteractiveOptions Interactive { get; set; } = new();

    // Diagnostic JSONL trace of agent sessions (events, tool I/O, rendered output).
    // Two separate knobs so the path can have a usable default while recording stays
    // off until explicitly enabled (you can't express "off" with a defaulted path
    // alone). Local debugging / evaluation harness only.
    public TraceOptions Trace { get; set; } = new();

    // Single source of truth for the SessionStyle string comparison, so the
    // "interactive" literal doesn't drift across call sites.
    public bool IsInteractive =>
        string.Equals(SessionStyle, "interactive", System.StringComparison.OrdinalIgnoreCase);
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
    [ConfigurationKey("endpoint", Description = "Endpoint URL for the builtin provider (APIM in front of AOAI).", IsUri = true)]
    public string Endpoint { get; set; } = "https://azclis-copilot-apim-beta-eus.azure-api.net/azclips/openai/";
    [ConfigurationKey("scope", Description = "Delegated API scope requested when acquiring a token for the builtin APIM provider.")]
    public string Scope { get; set; } = "api://a5ede409-60d3-4a6c-93e6-eb2e7271e8e3/Azclis.Intelligent.All";
    [ConfigurationKey("modelId", Description = "Model deployment name used by the builtin provider.")]
    public string ModelId { get; set; } = "gpt-5.4";
    [ConfigurationKey("apiVersion", Description = "Azure OpenAI API version used by the builtin provider.")]
    public string ApiVersion { get; set; } = "2025-04-01-preview";
}