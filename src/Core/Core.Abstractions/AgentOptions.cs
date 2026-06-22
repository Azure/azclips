namespace Core.Abstractions;

public sealed class AgentOptions
{
    // Off by default for Private Preview. The user opts in with
    // `azv2 config set --key agent.enabled --value true`; until then a failed
    // command surfaces its original error and never spawns the agent.
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "builtin";
    public BuiltinProviderOptions Builtin { get; set; } = new();

    // oneShot: agent proposes a fix and exits (az-CLI-like). interactive: agent
    // stays in a chat loop until the user exits or the escape valve trips.
    public string SessionStyle { get; set; } = "oneShot";

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

public sealed class TraceOptions
{
    // Off by default — recording writes nothing until the user opts in.
    public bool Enabled { get; set; } = false;

    // Where the JSONL trace files go (one file per session). Defaults under the
    // cirrus home dir so enabling trace "just works" without also setting a path.
    // The directory is created on first write if it doesn't exist.
    public string Path { get; set; } = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
        ".cirrus", "trace");
}

public sealed class BuiltinProviderOptions
{
    public string Endpoint { get; set; } = "https://azclis-copilot-apim-beta-eus.azure-api.net/cirrus/openai/";
    public string Scope { get; set; } = "https://management.core.windows.net//.default";
    public string ModelId { get; set; } = "gpt-5.4";
    public string ApiVersion { get; set; } = "2025-04-01-preview";
}