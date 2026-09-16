using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Common;
using Core.Agent.Hooks;
using Core.Agent.Hosting;
using Core.Agent.Plan;
using Core.Agent.Providers;
using Core.Agent.Telemetry;
using Core.Agent.Tools;
using Core.Agent.Tools.Resource.Cache;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Sku;
using Core.Agent.Tools.Shell;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Extensions.AI;
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
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromMinutes(10);

    private readonly CopilotClient _client;
    private readonly CopilotSession _session;
    private readonly string _model;
    private readonly SessionState? _state;
    private readonly AgentInvocationContext _invocationContext;

    // Tracks, for the in-flight SendAsync, whether the agent called a turn-closing
    // tool — propose_plan (asks for plan approval) or submit_response (ends the
    // session). Diagnostic/help tools like get_command DON'T count: a turn that
    // only queried then replied in prose is still a protocol violation. The host
    // reads this after SendAsync to force a correction on a bare-prose turn.
    private int _sendCalledClosingTool;

    public string Model => _model;

    public SessionErrored? LastError { get; private set; }

    // True when the most recent SendAsync called a turn-closing tool (propose_plan
    // or submit_response). A turn that ends without one — even if it called
    // get_command/bash — replied in prose and broke the act-through-tools contract.
    public bool LastSendCalledClosingTool => Volatile.Read(ref _sendCalledClosingTool) != 0;

    // True once a plan was rendered this session. Informational only — under the
    // unified-mind model the plan is a mid-turn artifact and submit_response is the
    // sole terminator, so the host no longer exits on this.
    public bool PlanShown => _state?.PlanShown ?? false;

    // True once submit_response has rendered its outcome for this session.
    public bool SubmitRendered => _state?.SubmitRendered ?? false;

    // The plan the agent proposed but that submit_response never rendered (because
    // the agent stopped after propose_plan without submitting). The one-shot host
    // reads these on exit to render it as a fallback, so the plan is never lost.
    public bool HasPendingPlan => _state?.LastIssues is { Count: > 0 } || _state?.LastPlanSteps is { Count: > 0 };
    public IReadOnlyList<string>? PendingIssues => _state?.LastIssues;
    public IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? PendingPlanSteps => _state?.LastPlanSteps;
    public IReadOnlyList<ConsoleApprovalPrompt.UserInputNeed>? PendingNeeds => _state?.LastNeedsUserInput;
    public IReadOnlyList<ConsoleApprovalPrompt.ChosenDefault>? PendingDefaults => _state?.LastDefaults;

    // True once submit_response rendered its final outcome AND the mode wants the
    // session to end on it. one-shot ends on submit_response (single response,
    // then exit); inline does NOT — it keeps the conversation open so the user can
    // reply to the plan/questions, and ends only when the user types exit/quit.
    public bool SessionClosed =>
        (_state?.IsOneShot ?? true) && (_state?.SubmitRendered ?? false);

    // Hook for the host renderer to learn when submit_response has rendered its
    // final outcome, so it can suppress any trailing model prose. Forwards to
    // SessionState — null is fine (no-op when no suppressor is wired).
    public void SetOnFinalRendered(Action? callback)
    {
        if (_state is not null) _state.OnFinalRendered = callback;
    }

    private ChatAgent(
        CopilotClient client, CopilotSession session, string model, SessionState? state,
        AgentInvocationContext invocationContext)
    {
        _client = client;
        _session = session;
        _model = model;
        _state = state;
        _invocationContext = invocationContext;
    }

    /// <param name="ct">
    /// Long-lived cancellation token expected to survive for the agent's full
    /// lifetime — captured by SDK hooks (OnPreToolUse, OnSessionStart,
    /// OnUserPromptSubmitted, OnPermissionRequest) and reused on every hook
    /// invocation after CreateAsync returns. Pass the host's top-level CTS
    /// (e.g. the one tied to Ctrl+C) rather than an init-scoped token; an
    /// already-cancelled ct silently breaks approvals and environment probing.
    /// </param>
    public static async Task<ChatAgent> CreateAsync(
        string flavor,
        IAuthService auth,
        IProfileService profileService,
        string azExePath,
        ConsoleApprovalPrompt approvalSink,
        IAgentRenderer renderer,
        AgentOptions options,
        string entrypoint,
        SpinnerService? spinner = null,
        ILogger? logger = null,
        string? failedCommand = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var invocationContext = new AgentInvocationContext(Activity.Current);
        using var create = invocationContext.StartOperation("Agent.Create", ct);
        var sdkLogger = logger ?? NullLogger.Instance;
        // BYOM (builtin provider) authenticates the LLM with an Azure token, not a
        // GitHub credential — so the daemon's default auto-login, which reads the
        // GitHub keychain and prompts on macOS, touches a credential this path never
        // uses. Disable it for builtin only; the github provider keeps auto-login so
        // subscription users' stored/gh-CLI credentials still work.
        var useBuiltin = string.Equals(options.Provider, "builtin", StringComparison.OrdinalIgnoreCase);
        var client = new CopilotClient(new CopilotClientOptions
        {
            Logger = sdkLogger,
            Connection = RuntimeConnection.ForStdio(path: BundledDaemonResolver.Resolve()),
            UseLoggedInUser = useBuiltin ? false : null,
        });

        try
        {
            return await CreateCoreAsync(
                client, invocationContext, flavor, auth, profileService, azExePath, approvalSink,
                renderer, options, entrypoint, spinner, failedCommand, ct).ConfigureAwait(false);
        }
        catch
        {
            // Same order as the successful path in DisposeAsync: tear the SDK down first, clear the
            // invocation context in a finally. Bind has already installed the wrapped hooks and
            // tools by now, so SDK cleanup can still call them — disposing the context first makes
            // EnterCallback throw ObjectDisposedException and mask the create failure being
            // rethrown here.
            try
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                invocationContext.Dispose();
            }
            throw;
        }
    }

    private static async Task<ChatAgent> CreateCoreAsync(
        CopilotClient client,
        AgentInvocationContext invocationContext,
        string flavor,
        IAuthService auth,
        IProfileService profileService,
        string azExePath,
        ConsoleApprovalPrompt approvalSink,
        IAgentRenderer renderer,
        AgentOptions options,
        string entrypoint,
        SpinnerService? spinner,
        string? failedCommand,
        CancellationToken ct)
    {
        var systemPrompt = LoadSystemPrompt(flavor, entrypoint);
        var armDetails = new ArmDetailsClient(auth);
        var cacheTools = new CachedResourceTools(armDetails, profileService);
        var refresher = new ArmCacheRefresher(auth, profileService);
        // The resource-reading tools push the active profile's tenant into
        // ResourceCache on every invocation (see CachedResourceTools.SyncTenantAsync),
        // so a mid-session `az account set` is reflected on the next query.
        // Nothing to pin at startup; wire only the background refresher here.
        ResourceCache.ConfigureSingleton(
            currentTenantId: null,
            refresher: refresher);
        var helpTools = new HelpTools(azExePath);
        var skuTools = new AvailableLocationsTool(auth, profileService);
        var submitTool = new SubmitResponseTool();
        var planTool = new ProposePlanTool();

        ChatAgent? self = null;
        SessionErrored? earlyError = null;

        var eventRenderer = new EventRenderer
        {
            // One line, because the body below is the only translation of an SDK event into agent
            // state, and CreateCoreAsync needs a live daemon to reach. Leaving it inline left it
            // with no coverage at all: a test could exercise the handler it hand-rolled and still
            // be blind to this call site losing a branch. #884 reworks this closure next.
            OnEvent = ev => HandleAgentEvent(ev, self, err => earlyError = err, spinner, renderer),
            OnText  = renderer.OnText,
        };

        var state = new SessionState
        {
            IsOneShot = !options.IsInteractive,
        };
        // Pre-fetch the failed command's help so the agent doesn't burn a turn
        // looking up the command it was triggered by. Same lookup the LLM's
        // get_command uses — on a hit it's the parameter table. On a miss (any
        // error payload, including not_found) FailedCommandHelp skips injection
        // entirely rather than seeding the session with an error blob.
        var failedHelp = await FailedCommandHelp.BuildContextAsync(helpTools, failedCommand, ct).ConfigureAwait(false);
        var sessionConfig = BuildBaseSessionConfig(
            systemPrompt, cacheTools, helpTools, skuTools, submitTool, planTool,
            azExePath, approvalSink, eventRenderer, state, failedHelp, entrypoint, ct);

        string resolvedModel;
        switch (options.Provider?.ToLowerInvariant())
        {
            case "builtin":
                resolvedModel = options.Builtin.ModelId;
                sessionConfig.Model = resolvedModel;
                sessionConfig.Provider = await BuiltinProviderConfig.BuildAsync(options.Builtin, auth, ct).ConfigureAwait(false);
                break;

            case "github":
                resolvedModel = string.Empty;
                break;

            default:
                throw new ArgumentException(
                    $"Unknown agent.provider value: {BuiltinProviderConfig.FormatProviderForError(options.Provider)}. Expected: builtin | github.",
                    nameof(options));
        }

        var agent = await ConnectAsync(
            client, sessionConfig, resolvedModel, state, invocationContext, ct).ConfigureAwait(false);
        self = agent;
        if (earlyError is not null) agent.LastError = earlyError;
        return agent;
    }

    internal static async Task<ChatAgent> ConnectAsync(
        CopilotClient client, SessionConfig sessionConfig, string model, SessionState? state,
        AgentInvocationContext invocationContext, CancellationToken ct = default)
    {
        invocationContext.Bind(sessionConfig);
        var session = await client.CreateSessionAsync(sessionConfig, ct).ConfigureAwait(false);
        return new ChatAgent(client, session, model, state, invocationContext);
    }

    // Duplicates the classic-CLI-boundary line already present in every
    // prompts/*.md (added in RN2 layer 1). The prompt version lives in a
    // long system message that models sometimes skim; injecting the same
    // reminder via SessionStart's AdditionalContext puts it in a spot the
    // model reads at every session boot. Wording deliberately mirrors the
    // prompt copy (NOT classic Azure CLI / training data may be
    // wrong / verify with get_command) so the two channels reinforce each
    // other instead of introducing wording drift.
    private const string AzclipsBoundary =
        "Reminder: this `az` is NOT the classic Azure CLI. " +
        "Your training data about classic `az` may be wrong; verify every command with get_command().";

    private static SessionConfig BuildBaseSessionConfig(
        string systemPrompt,
        CachedResourceTools cacheTools,
        HelpTools helpTools,
        AvailableLocationsTool skuTools,
        SubmitResponseTool submitTool,
        ProposePlanTool planTool,
        string azExePath,
        ConsoleApprovalPrompt approvalSink,
        EventRenderer eventRenderer,
        SessionState state,
        string? failedHelp,
        string entrypoint,
        CancellationToken ct)
    {
        // Customize the SDK's default system message. Our oneshot/inline prompt
        // REPLACES the identity section so it leads the message (the agent knows
        // it's the azclips error-recovery agent up front, not after the SDK's
        // generic preamble). All modes strip: tone (we don't want the generic
        // code-assistant voice), code_change_rules (azclips doesn't edit code),
        // and custom_instructions (repo CLAUDE.md / commit / notify rules leak
        // in here). oneshot additionally strips tool_instructions and
        // runtime_instructions — a single-shot plan needs neither the per-tool
        // usage guides nor the multi-turn runtime scaffolding; interactive
        // (inline/tui) keeps them because it actually runs tools across turns.
        // Kept in all modes: tool_efficiency, environment_context, guidelines,
        // safety.
        var sections = new Dictionary<SystemMessageSection, SectionOverride>
        {
            [SystemMessageSection.Identity] = new() { Action = SectionOverrideAction.Replace, Content = systemPrompt },
            [SystemMessageSection.Tone] = new() { Action = SectionOverrideAction.Remove },
            [SystemMessageSection.CodeChangeRules] = new() { Action = SectionOverrideAction.Remove },
            [SystemMessageSection.CustomInstructions] = new() { Action = SectionOverrideAction.Remove },
        };
        if (string.Equals(entrypoint, "oneshot", StringComparison.OrdinalIgnoreCase))
        {
            sections[SystemMessageSection.ToolInstructions] = new() { Action = SectionOverrideAction.Remove };
            sections[SystemMessageSection.RuntimeInstructions] = new() { Action = SectionOverrideAction.Remove };
        }

        return new SessionConfig
        {
            SessionId = Guid.NewGuid().ToString(),
            ConfigDirectory = HomePaths.GetDefaultHomeRoot(),
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Customize,
                Sections = sections,
            },
            Tools = cacheTools.All.Concat(helpTools.All).Concat(skuTools.All)
                .Concat(submitTool.All).Concat(planTool.All).Cast<AIFunctionDeclaration>().ToList(),
            // All built-in tools available (bash, view, edit, grep, glob,
            // web_fetch, web_search, todo, ...) plus our custom cache + help tools.
            AvailableTools = null,
            Hooks = new SessionHooks
            {
                OnPreToolUse = (input, _) => PreToolUseHook.HandleAsync(input, azExePath, approvalSink, state, ct),
                OnSessionStart = async (_, _) =>
                {
                    var envJson = await ShellOutHelper.InvokeAzAsync(azExePath, new[] { "agent", "tool", "env" }, ct).ConfigureAwait(false);
                    var ctx = EnvSnapshotFormatter.Format(envJson);
                    ctx = string.IsNullOrEmpty(ctx) ? AzclipsBoundary : $"{ctx}\n\n{AzclipsBoundary}";
                    // Append the pre-fetched help for the failed command (if any) so
                    // the agent starts with it instead of spending a turn on get_command.
                    if (!string.IsNullOrEmpty(failedHelp))
                        ctx = $"{ctx}\n\n{failedHelp}";
                    // When the snapshot reports signed-out, the fix will need `az login`,
                    // so pre-fetch its help too — the agent won't burn a turn looking it
                    // up. Mirrors the failed-command pre-fetch; best-effort (a failed
                    // lookup just means the agent falls back to get_command).
                    if (EnvSnapshotFormatter.IsSignedOut(envJson))
                    {
                        var loginHelp = await FailedCommandHelp.BuildContextAsync(helpTools, "login", ct).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(loginHelp))
                            ctx = $"{ctx}\n\n{loginHelp}";
                    }
                    return new SessionStartHookOutput { AdditionalContext = ctx };
                },
                OnUserPromptSubmitted = async (_, _) =>
                {
                    var envJson = await ShellOutHelper.InvokeAzAsync(azExePath, new[] { "agent", "tool", "env" }, ct).ConfigureAwait(false);
                    return new UserPromptSubmittedHookOutput { AdditionalContext = EnvSnapshotFormatter.Format(envJson) };
                },
            },
            OnPermissionRequest = async (req, invocation) =>
            {
                var toolName = (req as PermissionRequestCustomTool)?.ToolName;
                // Cache tools are read-only — silently approve. Bash + every
                // other built-in mutator goes through the user prompt.
                if (toolName is not null && PreToolUseHook.IsReadOnlyCacheTool(toolName))
                    return PermissionDecision.ApproveOnce();

                var ok = await approvalSink.RequestAsync(req, ct).ConfigureAwait(false);
                return ok ? PermissionDecision.ApproveOnce() : PermissionDecision.Reject(feedback: null);
            },
            OnEvent = eventRenderer.Forward,
        };
    }
    /// <summary>
    /// A fatal session error arrives on the SDK's own event thread, which is not synchronised with
    /// the host's turn boundaries, so this can run concurrently with an in-flight SendAsync. It has
    /// to cancel whichever turn currently holds the slot, not a handle recorded separately that may
    /// no longer be the live one. The SDK also faults the pending send on its own, so the effect
    /// here is which of the two wins: Cancel runs its registrations synchronously on this thread,
    /// so the turn unwinds as a cancellation rather than as an SDK fault.
    /// </summary>
    internal void OnSessionErrored(SessionErrored error)
    {
        LastError = error;
        _invocationContext.CancelCurrentOperation();
    }

    /// <summary>
    /// Translates one SDK event into agent state, spinner verb and rendered output.
    /// </summary>
    /// <remarks>
    /// Extracted so the wiring is testable. <see cref="CreateCoreAsync"/> needs a live daemon, so
    /// while this lived inline it had no coverage: a test could hand-roll an equivalent handler,
    /// exercise that, and stay blind to the real call site dropping a branch. <paramref name="self"/>
    /// is null until construction finishes, which is the whole reason for
    /// <paramref name="onEarlyError"/> — an error arriving during session creation has no turn to
    /// attribute itself to yet.
    /// </remarks>
    internal static void HandleAgentEvent(
        AgentEvent ev,
        ChatAgent? self,
        Action<SessionErrored> onEarlyError,
        SpinnerService? spinner,
        IAgentRenderer renderer)
    {
        if (ev is SessionErrored err)
        {
            if (self is not null) self.OnSessionErrored(err);
            else                  onEarlyError(err);
        }
        if (ev is ToolStarted ts && self is not null)
        {
            if (PreToolUseHook.IsClosingTool(ts.Name, self._state?.IsOneShot ?? false))
                Volatile.Write(ref self._sendCalledClosingTool, 1);
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
    }

    public async Task SendAsync(string prompt, CancellationToken ct)
    {
        // Reserving the slot IS publishing the handle, under one lock, so there is no window in
        // between for an abort to find nothing. The older order published the handle first to close
        // a supposed Esc window ahead of StartOperation, but that window cannot be reached from the
        // host: SendAndPumpAsync starts AbortKeyWatcher.RunAsync only after agent.SendAsync has
        // returned its Task, and everything before this method's first await already ran
        // synchronously on the caller's thread by then. An abort between entering SendAsync and
        // reserving the slot is therefore a no-op by construction -- and publishing first is what
        // let a rejected overlapping send stomp the live turn's handle.
        //
        // Disposal order is load-bearing, and `using` disposes in reverse acquisition order: the
        // Operation must leave the slot before the source it carries is disposed. That is what lets
        // CancelCurrentOperation take the reference under the lock and cancel outside it -- an abort
        // racing the end of a turn sees either a live source or an empty slot, never a disposed one.
        // The source is built on its own line so the two scopes below are independent statements
        // whose order can be swapped, which is how that guarantee is reverse-validated. If
        // StartOperation throws, the outer scope still disposes the source, so nothing leaks.
        var turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using (turnCts)
        using (var send = _invocationContext.StartOperation("Agent.Send", turnCts))
        {
            send.Activity?.SetTag(TelemetryTags.AgentModel, _model);
            LastError = null;
            Volatile.Write(ref _sendCalledClosingTool, 0);
            if (_state is not null) _state.SubmittedThisTurn = false;
            await _session.SendAndWaitAsync(
                new MessageOptions { Prompt = prompt },
                timeout: TurnTimeout,
                cancellationToken: turnCts.Token).ConfigureAwait(false);
        }
    }

    public async Task AbortAsync(CancellationToken ct = default)
    {
        _invocationContext.CancelCurrentOperation();
        try { await _session.AbortAsync(ct).ConfigureAwait(false); } catch { }
    }
    private static readonly string[] ValidEntrypoints = ["oneshot", "inline"];

    private static string LoadSystemPrompt(string flavor, string entrypoint)
    {
        if (!ValidEntrypoints.Contains(entrypoint, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"entrypoint must be one of {string.Join(" | ", ValidEntrypoints)} — refusing to guess which system prompt to load. Got: '{entrypoint}'.",
                nameof(entrypoint));

        // Prompts live at a layout-specific location resolved by
        // AgentInstallLayout. The layout flavor (packaged vs dev) is baked
        // into the dll at compile time, so this dll can only look at the
        // path it was built to look at — no cross-install ambiguity even
        // when an MSI-installed daemon and a dev-built one coexist.
        var path = Path.Combine(AgentInstallLayout.PromptsDir,
            $"{entrypoint.ToLowerInvariant()}.md");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Agent system prompt not found: {path}. " +
                (AgentInstallLayout.IsPackaged
                    ? "Packaging bug — stage_agent should have staged prompts under Extensions/Microsoft.Azure.Azclips.Extensions.Agent/prompts/."
                    : "Dev build bug — csproj should copy prompts/ to the build output."),
                path);
        var content = File.ReadAllText(path);
        return content.Replace("{{flavor}}", flavor, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(5);

    public async ValueTask DisposeAsync()
    {
        if (!_invocationContext.BeginDispose())
            return;
        // Cancel the in-flight turn before waiting on the SDK. Without this nothing tells the send
        // to stop, so it ends only as a side effect of the connection going away -- or, when the
        // SDK is genuinely wedged, not before the two five-second timeouts below have run out and
        // abandoned it. Cancelling first gives SendAsync a chance to unwind and close Agent.Send
        // itself, which is the only path that reports the turn rather than marking it abandoned.
        _invocationContext.CancelCurrentOperation();
        try
        {
            await WithTimeout(_session.DisposeAsync().AsTask(), DisposeTimeout).ConfigureAwait(false);
            await WithTimeout(_client.DisposeAsync().AsTask(), DisposeTimeout).ConfigureAwait(false);
        }
        finally
        {
            _invocationContext.Dispose();
        }
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
