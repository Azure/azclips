// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Core.Abstractions;

public sealed class AgentOptions
{
    // Off by default for Private Preview. The user opts in with
    // `az config set --key agent.enabled --value true`; until then a failed
    // command surfaces its original error and never spawns the agent.
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "builtin";
    public BuiltinProviderOptions Builtin { get; set; } = new();

    // oneshot: agent proposes a fix and exits (az-CLI-like). interactive: agent
    // stays in a chat loop until the user exits or the escape valve trips.
    public string SessionStyle { get; set; } = "oneshot";

    // Interactive-only display settings. Meaningless in oneshot (which prints no
    // tool events at all), hence namespaced under interactive.
    public InteractiveOptions Interactive { get; set; } = new();

    // Diagnostic JSONL trace of agent sessions (events, tool I/O, rendered output).
    // Two separate knobs so the path can have a usable default while recording stays
    // off until explicitly enabled (you can't express "off" with a defaulted path
    // alone). Local debugging / evaluation harness only.
    public TraceOptions Trace { get; set; } = new();

    // Derived views over the settable properties above, not settings in their own right.
    //
    // [JsonIgnore] is load-bearing: ConfigurationService builds `--include-defaults` by serializing
    // this object, so any public getter here shows up as an apparent config key regardless of
    // whether it is bindable or listed in ConfigurationSchema. Anything computed added below needs
    // the same attribute — ConfigurationServiceTests pins the whole agent key set for that reason.

    // Single source of truth for the SessionStyle string comparison, so the
    // "interactive" literal doesn't drift across call sites.
    [JsonIgnore]
    public bool IsInteractive =>
        string.Equals(SessionStyle, "interactive", System.StringComparison.OrdinalIgnoreCase);

    // Same idea for the provider. Builtin authenticates the model with an Azure token and is the
    // only provider that talks to Microsoft-hosted endpoints; github uses the user's own Copilot
    // credential and never needs an Azure login, so anything ARM-shaped must be gated on this.
    [JsonIgnore]
    public bool IsBuiltinProvider =>
        string.Equals(Provider, "builtin", System.StringComparison.OrdinalIgnoreCase);
}

public sealed class InteractiveOptions
{
    // Off by default — internal tool details (get_command / find_* args and
    // output) stay hidden so the interactive session shows only the shell
    // commands the user cares about. Turn on to debug what the agent is doing.
    public bool Verbose { get; set; } = false;
}

public sealed class TraceOptions
{
    // Off by default — recording writes nothing until the user opts in.
    public bool Enabled { get; set; } = false;

    // Where the JSONL trace files go (one file per session). Defaults to
    // ~/.azclips/trace to match schema/docs defaults; the leading ~ is expanded
    // at the point of use. The directory is created on first write if it doesn't exist.
    public string Path { get; set; } = HomePaths.GetDefaultTraceConfigValue();
}

public sealed class BuiltinProviderOptions
{
    /// <summary>
    /// AAD scope requested for the builtin provider's token. A constant rather than a property, so
    /// it is neither in <c>ConfigurationSchema</c> nor picked up by the options serializer that
    /// backs <c>az config list --include-defaults</c> — a bindable property would still surface it
    /// as an apparent setting even with the schema entry gone.
    ///
    /// It must stay an ARM audience: the same token also authorizes the ARM data boundary lookup,
    /// and pointing it elsewhere would break that lookup with an opaque 401 while aiming the
    /// model's bearer token at an arbitrary audience.
    /// </summary>
    public const string ArmScope = "https://management.core.windows.net//.default";

    public string Endpoint { get; set; } = "https://azclis-copilot-apim-beta-eus.azure-api.net/azclips/openai/";
    public string ModelId { get; set; } = "gpt-5.4";
    public string ApiVersion { get; set; } = "2025-04-01-preview";
}
