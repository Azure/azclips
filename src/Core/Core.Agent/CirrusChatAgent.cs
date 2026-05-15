using Core.Abstractions;
using Core.Agent.Hosting;
using Core.Agent.Tools;
using Core.Agent.Tools.Cache;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Agent;

/// <summary>
/// Single-agent entry point built on the GitHub Copilot SDK. Hosts construct
/// one of these, drive a chat loop they own with <see cref="SendAsync"/>, and
/// race the send Task against <see cref="ConsoleApprovalPrompt.PumpOneAsync"/>
/// to handle approvals during a busy turn.
///
/// Pattern:    classic chat-agent with tools (run_command + cache lookups);
///             no Workflow / no sub-agents / no LLM router — those were
///             MAF-era complexity that the SDK obviates.
/// Avoid:      ❌ running a built-in <c>Console.ReadLine</c> chat loop inside
///                the agent — host owns input, so the approval pump and the
///                chat prompt share a single stdin reader (no race);
///             ❌ JSON-envelope outputs (force streaming markdown);
///             ❌ wrapping cache tools in approval (only run_command is mutating);
///             ❌ trusting FinishReason for stream termination (use SessionIdle).
/// </summary>
public sealed class CirrusChatAgent : IAsyncDisposable
{
    public const string ModelId = "claude-opus-4.7-1m-internal";

    // SDK default is 60s, which is way too tight for interactive azv2 tool
    // calls — `azv2 login --use-device-code` alone routinely blocks for minutes
    // while the user pastes the code in a browser. Use a long ceiling and rely
    // on user-driven cancellation to unstick a genuinely hung turn instead.
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromHours(1);

    private readonly CopilotClient _client;
    private readonly CopilotSession _session;

    public string Model => ModelId;

    private CirrusChatAgent(CopilotClient client, CopilotSession session)
    {
        _client = client;
        _session = session;
    }

    public static async Task<CirrusChatAgent> CreateAsync(
        string flavor,
        IAuthService auth,
        RunCommandTool.RunCommandExecutor runCommand,
        ConsoleApprovalPrompt approvalSink,
        ConsoleEventRenderer renderer,
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

        var eventRenderer = new EventRenderer
        {
            OnEvent = renderer.OnEvent,
            OnText  = renderer.OnText,
        };

        var tools = new List<Microsoft.Extensions.AI.AIFunction>(cacheTools.All) { runCmdTool };
        var toolNames = tools.Select(t => t.Name).ToList();

        // Pull in two SDK built-ins that have no cirrus equivalent and are
        // genuinely useful regardless of mode: web_fetch (Azure docs) +
        // web_search (community answers for unknown error codes).
        toolNames.Add("web_fetch");
        toolNames.Add("web_search");

        var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = ModelId,
            Streaming = true,
            SystemMessage = new SystemMessageConfig { Content = systemPrompt },
            Tools = tools,
            AvailableTools = toolNames,
            OnPermissionRequest = async (req, invocation) =>
            {
                // Only the mutating run_command goes through the user-facing prompt.
                // Cache tools are read-only — silently approve.
                var toolName = (req as PermissionRequestCustomTool)?.ToolName;
                if (!string.Equals(toolName, "run_command", StringComparison.OrdinalIgnoreCase))
                    return new PermissionRequestResult { Kind = PermissionRequestResultKind.Approved };

                var ok = await approvalSink.RequestAsync(req, ct).ConfigureAwait(false);
                return new PermissionRequestResult
                {
                    Kind = ok ? PermissionRequestResultKind.Approved
                              : PermissionRequestResultKind.Rejected
                };
            },
            OnEvent = eventRenderer.Forward,
        }, ct).ConfigureAwait(false);

        return new CirrusChatAgent(client, session);
    }

    /// <summary>
    /// Send a single user prompt and wait for the assistant turn (or chain of
    /// tool turns) to complete. Hosts call this from their own chat loop.
    /// </summary>
    public Task SendAsync(string prompt, CancellationToken ct) =>
        _session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TurnTimeout,
            cancellationToken: ct);

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
