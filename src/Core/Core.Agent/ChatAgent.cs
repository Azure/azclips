using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Hooks;
using Core.Agent.Hosting;
using Core.Agent.Probes;
using Core.Agent.Tools.Cache;
using Core.Agent.Tools.Help;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Agent;

/// <summary>
/// Single-agent entry point built on the GitHub Copilot SDK. Hosts construct
/// one of these, drive a chat loop they own with <see cref="SendAsync"/>, and
/// race the send Task against <see cref="ConsoleApprovalPrompt.PumpOneAsync"/>
/// to handle approvals during a busy turn.
/// </summary>
public sealed class ChatAgent : IAsyncDisposable
{
    public const string ModelId = "claude-opus-4.7-1m-internal";

    private static readonly TimeSpan TurnTimeout = TimeSpan.FromMinutes(10);

    private readonly CopilotClient _client;
    private readonly CopilotSession _session;

    private CancellationTokenSource? _activeTurnCts;

    public string Model => ModelId;

    public SessionErrored? LastError { get; private set; }

    private ChatAgent(CopilotClient client, CopilotSession session)
    {
        _client = client;
        _session = session;
    }

    public static async Task<ChatAgent> CreateAsync(
        string flavor,
        IAuthService auth,
        IAuthContextService authContext,
        string azv2ExePath,
        ConsoleApprovalPrompt approvalSink,
        IAgentRenderer renderer,
        SpinnerService? spinner = null,
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
        var helpTools = new HelpTools();
        var prober = new EnvironmentProber(authContext, flavor);

        ChatAgent? self = null;

        var eventRenderer = new EventRenderer
        {
            OnEvent = ev =>
            {
                if (ev is SessionErrored err)
                {
                    if (self is not null) self.LastError = err;
                    try { self?._activeTurnCts?.Cancel(); } catch { }
                }
                if (spinner is not null)
                {
                    switch (ev)
                    {
                        case ToolStarted t: spinner.SetVerb(SpinnerService.VerbForTool(t.Name)); break;
                        case ToolCompleted: case ToolFailed: spinner.SetVerb("thinking"); break;
                    }
                }
                renderer.OnEvent(ev);
            },
            OnText  = renderer.OnText,
        };

        var session = await client.CreateSessionAsync(new SessionConfig
        {
            SessionId = Guid.NewGuid().ToString(),
            ConfigDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cirrus"),
            Model = ModelId,
            Streaming = true,
            SystemMessage = new SystemMessageConfig { Content = systemPrompt },
            Tools = cacheTools.All.Concat(helpTools.All).ToList(),
            // All built-in tools available (bash, view, edit, grep, glob,
            // web_fetch, web_search, todo, ...) plus our custom cache + help tools.
            AvailableTools = null,
            Hooks = new SessionHooks
            {
                OnPreToolUse = (input, _) => Task.FromResult(HandlePreToolUse(input, azv2ExePath)),
                OnSessionStart = async (_, _) =>
                {
                    var snap = await prober.ProbeAsync(ct).ConfigureAwait(false);
                    return new SessionStartHookOutput { AdditionalContext = snap.FormatForSystemPrompt() };
                },
                OnUserPromptSubmitted = async (_, _) =>
                {
                    var snap = await prober.ProbeAsync(ct).ConfigureAwait(false);
                    return new UserPromptSubmittedHookOutput { AdditionalContext = snap.FormatForSystemPrompt() };
                },
            },
            OnPermissionRequest = async (req, invocation) =>
            {
                var toolName = (req as PermissionRequestCustomTool)?.ToolName;
                // Cache tools are read-only — silently approve. Bash + every
                // other built-in mutator goes through the user prompt.
                if (toolName is not null && IsReadOnlyCacheTool(toolName))
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

        var agent = new ChatAgent(client, session);
        self = agent;
        return agent;
    }

    public async Task SendAsync(string prompt, CancellationToken ct)
    {
        LastError = null;
        var oldCts = Interlocked.Exchange(ref _activeTurnCts,
            CancellationTokenSource.CreateLinkedTokenSource(ct));
        oldCts?.Dispose();
        try
        {
            await _session.SendAndWaitAsync(
                new MessageOptions { Prompt = prompt },
                timeout: TurnTimeout,
                cancellationToken: _activeTurnCts!.Token).ConfigureAwait(false);
        }
        finally
        {
            var done = Interlocked.Exchange(ref _activeTurnCts, null);
            done?.Dispose();
        }
    }

    public async Task AbortAsync(CancellationToken ct = default)
    {
        try { _activeTurnCts?.Cancel(); } catch { }
        try { await _session.AbortAsync(ct).ConfigureAwait(false); } catch { }
    }

    private static PreToolUseHookOutput? HandlePreToolUse(PreToolUseHookInput input, string azv2ExePath)
    {
        if (!string.Equals(input.ToolName, "bash", StringComparison.OrdinalIgnoreCase))
            return null;

        var command = ExtractBashCommand(input.ToolArgs);
        if (command is null) return null;

        if (PlaceholderDetector.TryDetect(command, out var placeholder))
        {
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = $"Detected placeholder '{placeholder}' in command. Ask the user for the real value.",
            };
        }

        var rewritten = RewriteAzv2(command, azv2ExePath);
        if (!ReferenceEquals(rewritten, command))
        {
            return new PreToolUseHookOutput
            {
                PermissionDecision = "ask",
                ModifiedArgs = new Dictionary<string, object?> { ["command"] = rewritten },
            };
        }

        return null;
    }

    private static string? ExtractBashCommand(object? args)
    {
        if (args is null) return null;
        if (args is JsonElement je && je.ValueKind == JsonValueKind.Object &&
            je.TryGetProperty("command", out var cmd) && cmd.ValueKind == JsonValueKind.String)
            return cmd.GetString();
        if (args is IDictionary<string, object?> dict && dict.TryGetValue("command", out var v))
            return v?.ToString();
        return null;
    }

    private static string RewriteAzv2(string command, string azv2ExePath)
    {
        var trimmed = command.TrimStart();
        if (!trimmed.StartsWith("azv2 ", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Equals("azv2", StringComparison.OrdinalIgnoreCase))
            return command;

        var rest = trimmed.Length > 4 ? trimmed[5..] : string.Empty;
        var quoted = azv2ExePath.Contains(' ') ? $"\"{azv2ExePath}\"" : azv2ExePath;
        return string.IsNullOrEmpty(rest) ? quoted : $"{quoted} {rest}";
    }

    private static bool IsReadOnlyCacheTool(string toolName) =>
        toolName.StartsWith("find_", StringComparison.OrdinalIgnoreCase) ||
        toolName.StartsWith("get_",  StringComparison.OrdinalIgnoreCase) ||
        toolName.StartsWith("list_", StringComparison.OrdinalIgnoreCase);

    private static string LoadSystemPrompt(string flavor, string mode)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "agent.md");
        var content = File.ReadAllText(path);
        return content
            .Replace("{{flavor}}", flavor, StringComparison.OrdinalIgnoreCase)
            .Replace("{{mode}}", mode, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(5);

    public async ValueTask DisposeAsync()
    {
        await WithTimeout(_session.DisposeAsync().AsTask(), DisposeTimeout).ConfigureAwait(false);
        await WithTimeout(_client.DisposeAsync().AsTask(),  DisposeTimeout).ConfigureAwait(false);
    }

    private static async Task WithTimeout(Task work, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            var winner = await Task.WhenAny(work, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)).ConfigureAwait(false);
            if (winner == work) await work.ConfigureAwait(false);
        }
        catch { }
    }
}
