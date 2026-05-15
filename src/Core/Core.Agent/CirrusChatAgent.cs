using Core.Abstractions;
using Core.Agent.Models;
using Core.Agent.Tools;
using Core.Agent.Tools.Cache;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Agent;

/// <summary>
/// Single-agent entry point built on the GitHub Copilot SDK. Hosts (CLI today,
/// TUI later) construct one of these, wire their <see cref="RunCommandTool.RunCommandExecutor"/>
/// (which owns the tee), and drive multi-turn chat via <see cref="RunChatAsync"/>.
///
/// Pattern:    classic chat-agent with tools (run_command + cache lookups);
///             no Workflow / no sub-agents / no LLM router — those were
///             MAF-era complexity that the SDK obviates.
/// Avoid:      ❌ JSON-envelope outputs (force streaming markdown);
///             ❌ wrapping cache tools in approval (only run_command is mutating);
///             ❌ trusting FinishReason for stream termination (use SessionIdle).
/// </summary>
public sealed class CirrusChatAgent : IAsyncDisposable
{
    public const string ModelId = "claude-opus-4.7-1m-internal";

    private readonly CopilotClient _client;
    private readonly CopilotSession _session;

    public string Model => ModelId;

    public Action<AgentEvent>? OnAgentEvent
    {
        get => _renderer.OnEvent;
        set => _renderer.OnEvent = value;
    }

    public Action<string>? OnAgentText
    {
        get => _renderer.OnText;
        set => _renderer.OnText = value;
    }

    private readonly EventRenderer _renderer;

    private CirrusChatAgent(CopilotClient client, CopilotSession session, EventRenderer renderer)
    {
        _client = client;
        _session = session;
        _renderer = renderer;
    }

    public static async Task<CirrusChatAgent> CreateAsync(
        string flavor,
        IAuthService auth,
        RunCommandTool.RunCommandExecutor runCommand,
        Func<PermissionRequest, CancellationToken, Task<bool>> approveTool,
        ILogger? logger = null,
        string mode = "azure",
        CancellationToken ct = default)
    {
        var sdkLogger = logger ?? NullLogger.Instance;
        var client = new CopilotClient(new CopilotClientOptions
        {
            Logger = sdkLogger,
            CliPath = BundledDaemonResolver.Resolve(),
        });

        // Day-1 safety: confirm the model is actually served by the local daemon.
        // Fail fast with a useful list instead of getting a vague RPC error later.
        var models = await client.ListModelsAsync(ct).ConfigureAwait(false);
        if (!models.Any(m => string.Equals(m.Id, ModelId, StringComparison.OrdinalIgnoreCase)))
        {
            var available = string.Join(", ", models.Select(m => m.Id));
            await client.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Copilot daemon does not advertise model '{ModelId}'. Available: {available}");
        }

        var systemPrompt = LoadSystemPrompt(flavor, mode);
        var armDetails = new ArmDetailsClient(auth);
        var cacheTools = new CachedResourceTools(armDetails);
        var runCmdTool = RunCommandTool.Build(runCommand);

        var renderer = new EventRenderer();

        var tools = new List<Microsoft.Extensions.AI.AIFunction>(cacheTools.All) { runCmdTool };
        var toolNames = tools.Select(t => t.Name).ToList();

        // Pull in two SDK built-ins that have no cirrus equivalent and are
        // genuinely useful regardless of mode: web_fetch (Azure docs) +
        // web_search (community answers for unknown error codes). Everything
        // else (powershell / edit / create / grep / glob / view / sql / task /
        // github-mcp-*) stays masked — we either own a better local
        // equivalent (run_command) or it's noise for an Azure CLI agent.
        toolNames.Add("web_fetch");
        toolNames.Add("web_search");

        var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = ModelId,
            Streaming = true,
            SystemMessage = new SystemMessageConfig { Content = systemPrompt },
            Tools = tools,
            // Session-wide allowlist: hide built-in claude-code tools we don't
            // want surfaced (grep/glob/view/bash/edit/...). The four cirrus
            // tools + web_fetch + web_search go through; everything else is
            // hidden. Drops first-turn input dramatically vs. the unrestricted
            // ~39k baseline (measured on T1 dry-run).
            AvailableTools = toolNames,
            OnPermissionRequest = async (req, _) =>
            {
                // Only the mutating run_command goes through the user-facing prompt.
                // Cache tools are read-only — silently approve.
                var toolName = (req as PermissionRequestCustomTool)?.ToolName;
                if (!string.Equals(toolName, "run_command", StringComparison.OrdinalIgnoreCase))
                    return new PermissionRequestResult { Kind = PermissionRequestResultKind.Approved };

                var ok = await approveTool(req, ct).ConfigureAwait(false);
                return new PermissionRequestResult
                {
                    Kind = ok ? PermissionRequestResultKind.Approved
                              : PermissionRequestResultKind.UserNotAvailable
                };
            },
            OnEvent = renderer.Forward,
        }, ct).ConfigureAwait(false);

        return new CirrusChatAgent(client, session, renderer);
    }

    /// <summary>
    /// Send the user's first prompt (typically the original CLI args joined with
    /// spaces) and enter a read-eval loop until the user types exit / quit /
    /// Ctrl-C. Used by the classic CLI chat fallback path.
    /// </summary>
    public async Task RunChatAsync(string firstPrompt, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(firstPrompt))
        {
            await _session.SendAndWaitAsync(
                new MessageOptions { Prompt = firstPrompt },
                timeout: TurnTimeout,
                cancellationToken: ct).ConfigureAwait(false);
        }

        while (!ct.IsCancellationRequested)
        {
            Console.WriteLine();
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line is null) break;
            line = line.Trim();
            if (line.Length == 0) continue;
            if (line is "exit" or "quit" or "/exit" or "/quit" or ":q") break;

            await _session.SendAndWaitAsync(
                new MessageOptions { Prompt = line },
                timeout: TurnTimeout,
                cancellationToken: ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Send a single user prompt and wait for the assistant turn (or chain of
    /// tool turns) to complete. Used by TUI hosts that own their own input
    /// loop and just need the SDK to drive one round-trip per Enter key.
    /// </summary>
    public Task SendAsync(string prompt, CancellationToken ct) =>
        _session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TurnTimeout,
            cancellationToken: ct);

    // SDK default is 60s, which is way too tight for interactive azv2 tool
    // calls — `azv2 login --use-device-code` alone routinely blocks for minutes
    // while the user pastes the code in a browser. Use a long ceiling and rely
    // on user-driven cancellation (Esc / Ctrl+C in the TUI) to unstick a
    // genuinely hung turn instead.
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromHours(1);

    private static string LoadSystemPrompt(string flavor, string mode)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "agent.md");
        var content = File.ReadAllText(path);
        return content
            .Replace("{{flavor}}", flavor, StringComparison.OrdinalIgnoreCase)
            .Replace("{{mode}}", mode, StringComparison.OrdinalIgnoreCase);
    }

    public async ValueTask DisposeAsync()
    {
        try { await _session.DisposeAsync().ConfigureAwait(false); } catch { }
        try { await _client.DisposeAsync().ConfigureAwait(false); } catch { }
    }
}
