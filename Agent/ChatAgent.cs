using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Common;
using Core.Agent.Hooks;
using Core.Agent.Hosting;
using Core.Agent.Plan;
using Core.Agent.Tools.Env;
using Core.Agent.Tools;
using Core.Agent.Tools.Resource.Cache;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Sku;
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

    private CancellationTokenSource? _activeTurnCts;

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

    // The plan the agent proposed but that submit_response never rendered (because
    // the agent stopped after propose_plan without submitting). The one-shot host
    // reads these on exit to render it as a fallback, so the plan is never lost.
    public bool SubmitRendered => _state?.SubmitRendered ?? false;
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

    private ChatAgent(CopilotClient client, CopilotSession session, string model, SessionState? state)
    {
        _client = client;
        _session = session;
        _model = model;
        _state = state;
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
        var sdkLogger = logger ?? NullLogger.Instance;
        var client = new CopilotClient(new CopilotClientOptions
        {
            Logger = sdkLogger,
            Connection = RuntimeConnection.ForStdio(path: BundledDaemonResolver.Resolve()),
        });

        try
        {
            return await CreateCoreAsync(
                client, flavor, auth, profileService, azExePath, approvalSink,
                renderer, options, entrypoint, spinner, failedCommand, ct).ConfigureAwait(false);
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<ChatAgent> CreateCoreAsync(
        CopilotClient client,
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
        var cacheTools = new CachedResourceTools(armDetails);
        var helpTools = new HelpTools();
        var skuTools = new AvailableLocationsTool(auth, profileService);
        var submitTool = new SubmitResponseTool();
        var planTool = new ProposePlanTool();
        var prober = new EnvironmentProber(profileService, flavor);

        ChatAgent? self = null;
        SessionErrored? earlyError = null;

        var eventRenderer = new EventRenderer
        {
            OnEvent = ev =>
            {
                if (ev is SessionErrored err)
                {
                    if (self is not null) self.LastError = err;
                    else                  earlyError = err;
                    try { self?._activeTurnCts?.Cancel(); } catch { }
                }
                if (ev is ToolStarted ts && self is not null)
                {
                    if (IsClosingTool(ts.Name, self._state?.IsOneShot ?? false))
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
            },
            OnText  = renderer.OnText,
        };

        var state = new SessionState
        {
            IsOneShot = !options.IsInteractive,
        };
        // Pre-fetch the failed command's help so the agent doesn't burn a turn
        // looking up the command it was triggered by. Same lookup the LLM's
        // get_command uses — on a hit it's the parameter table; on a miss it's the
        // not_found JSON with suggestions, which is still useful context.
        var failedHelp = FailedCommandHelp.BuildContext(helpTools, failedCommand);
        var sessionConfig = BuildBaseSessionConfig(
            systemPrompt, cacheTools, helpTools, skuTools, submitTool, planTool, prober,
            azExePath, approvalSink, eventRenderer, state, failedHelp, ct);

        string resolvedModel;
        switch (options.Provider?.ToLowerInvariant())
        {
            case "builtin":
                resolvedModel = options.Builtin.ModelId;
                sessionConfig.Model = resolvedModel;
                sessionConfig.Provider = await BuildBuiltinProviderConfigAsync(options.Builtin, auth, ct).ConfigureAwait(false);
                break;

            case "github":
                resolvedModel = string.Empty;
                break;

            default:
                throw new ArgumentException(
                    $"Unknown agent.provider value: {FormatProviderForError(options.Provider)}. Expected: builtin | github.",
                    nameof(options));
        }

        var session = await client.CreateSessionAsync(sessionConfig, ct).ConfigureAwait(false);

        var agent = new ChatAgent(client, session, resolvedModel, state);
        self = agent;
        if (earlyError is not null) agent.LastError = earlyError;
        return agent;
    }

    private static SessionConfig BuildBaseSessionConfig(
        string systemPrompt,
        CachedResourceTools cacheTools,
        HelpTools helpTools,
        AvailableLocationsTool skuTools,
        SubmitResponseTool submitTool,
        ProposePlanTool planTool,
        EnvironmentProber prober,
        string azExePath,
        ConsoleApprovalPrompt approvalSink,
        EventRenderer eventRenderer,
        SessionState state,
        string? failedHelp,
        CancellationToken ct)
    {
        return new SessionConfig
        {
            SessionId = Guid.NewGuid().ToString(),
            ConfigDirectory = HomePaths.GetDefaultHomeRoot(),
            Streaming = true,
            SystemMessage = new SystemMessageConfig { Content = systemPrompt },
            Tools = cacheTools.All.Concat(helpTools.All).Concat(skuTools.All)
                .Concat(submitTool.All).Concat(planTool.All).Cast<AIFunctionDeclaration>().ToList(),
            // All built-in tools available (bash, view, edit, grep, glob,
            // web_fetch, web_search, todo, ...) plus our custom cache + help tools.
            AvailableTools = null,
            Hooks = new SessionHooks
            {
                OnPreToolUse = (input, _) => HandlePreToolUseAsync(input, azExePath, approvalSink, state, ct),
                OnSessionStart = async (_, _) =>
                {
                    var snap = await prober.ProbeAsync(ct).ConfigureAwait(false);
                    var ctx = snap.FormatForSystemPrompt();
                    // Append the pre-fetched help for the failed command (if any) so
                    // the agent starts with it instead of spending a turn on get_command.
                    if (!string.IsNullOrEmpty(failedHelp))
                        ctx = string.IsNullOrEmpty(ctx) ? failedHelp : $"{ctx}\n\n{failedHelp}";
                    return new SessionStartHookOutput { AdditionalContext = ctx };
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
                    return PermissionDecision.ApproveOnce();

                var ok = await approvalSink.RequestAsync(req, ct).ConfigureAwait(false);
                return ok ? PermissionDecision.ApproveOnce() : PermissionDecision.Reject(feedback: null);
            },
            OnEvent = eventRenderer.Forward,
        };
    }

    private static async Task<ProviderConfig> BuildBuiltinProviderConfigAsync(
        BuiltinProviderOptions opts,
        IAuthService auth,
        CancellationToken ct)
    {
        Core.Abstractions.AuthResult? authResult;
        try
        {
            authResult = await auth.AcquireTokenSilentlyAsync(
                scopes: [opts.Scope],
                tenantId: null,
                promptOnError: false,
                cancellationToken: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C during startup: let cancellation bubble up so the top-level
            // handler exits quietly (130), instead of masquerading as an auth failure.
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                BuildBuiltinAuthErrorMessage() +
                $"{Environment.NewLine}{Environment.NewLine}Underlying cause: {ex.GetType().Name}: {ex.Message}",
                ex);
        }

        if (authResult is null || string.IsNullOrEmpty(authResult.AccessToken))
        {
            throw new InvalidOperationException(BuildBuiltinAuthErrorMessage());
        }

        return new ProviderConfig
        {
            Type = "azure",
            WireApi = "responses",
            BaseUrl = opts.Endpoint,
            BearerToken = authResult.AccessToken,
            ModelId = opts.ModelId,
            WireModel = opts.ModelId,
            Azure = new AzureOptions { ApiVersion = opts.ApiVersion },
        };
    }

    private static string BuildBuiltinAuthErrorMessage() =>
        """
        Agent's 'builtin' provider requires an Azure login to call the managed GPT model.
        No active Azure account was found.

        To fix this, run:
            az login --subscription-id <your-subscription-id>

        Alternative: switch to the 'github' provider (uses your own GitHub Copilot subscription,
        billed to your GitHub account):
            az config set --key agent.provider --value github
        """;

    private static string FormatProviderForError(string? provider)
    {
        if (provider is null) return "(null)";
        if (provider.Length == 0) return "(empty)";
        if (string.IsNullOrWhiteSpace(provider)) return $"(whitespace, length={provider.Length})";
        return $"'{provider}'";
    }

    public async Task SendAsync(string prompt, CancellationToken ct)
    {
        LastError = null;
        Volatile.Write(ref _sendCalledClosingTool, 0);
        if (_state is not null) _state.SubmittedThisTurn = false;
        var turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var oldCts = Interlocked.Exchange(ref _activeTurnCts, turnCts);
        oldCts?.Dispose();
        try
        {
            await _session.SendAndWaitAsync(
                new MessageOptions { Prompt = prompt },
                timeout: TurnTimeout,
                cancellationToken: turnCts.Token).ConfigureAwait(false);
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

    // The OnPreToolUse gate: validates/renders submit_response (the terminator)
    // and propose_plan (a mid-turn artifact), enforces the plan-before-execute gate
    // on bash/powershell, and rewrites bare az to the absolute exe path.
    // internal (not private) so PlanGateTests can drive it with a real SessionState.
    internal static async Task<PreToolUseHookOutput?> HandlePreToolUseAsync(
        PreToolUseHookInput input,
        string azExePath,
        ConsoleApprovalPrompt approvalSink,
        SessionState state,
        CancellationToken ct)
    {
        // Post-terminator gate: once submit_response has rendered this turn, the
        // turn is logically over — deny any further tool call instead of cancelling
        // the turn (cancellation would surface as OperationCanceledException, which
        // the host misreads as Ctrl+C). Trailing prose is suppressed renderer-side.
        if (state.SubmittedThisTurn)
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "This turn already ended with submit_response.",
            };

        // submit_response is the one and only session terminator (unified mind).
        // No user approval — the LLM already decided to end. Validate args; on
        // failure deny + feed the reason back as additionalContext so the LLM
        // self-corrects. On success render the final outcome and allow: the host
        // recognizes this turn (via SubmitRendered) as the clean close.
        if (string.Equals(input.ToolName, "submit_response", StringComparison.OrdinalIgnoreCase))
        {
            var submitError = PlanArgsParser.ValidateSubmitResponse(input.ToolArgs);
            if (submitError is not null)
                return new PreToolUseHookOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = $"Invalid submit_response arguments: {submitError}",
                    AdditionalContext = $"Your submit_response call was rejected: {submitError}. Call it again with corrected arguments.",
                };

            var status = ArgsNormalizer.TryGetString(input.ToolArgs, "status") ?? string.Empty;
            var message = ArgsNormalizer.TryGetString(input.ToolArgs, "message");
            // submit_response is the unified renderer: it shows the plan the agent
            // proposed earlier this turn (if any) as content blocks, then the status.
            // interactive will keep looping; one-shot exits.
            approvalSink.ShowFinalOutcome(
                status,
                message,
                state.LastIssues,
                state.LastPlanSteps,
                state.LastNeedsUserInput,
                state.LastDefaults,
                state.IsOneShot);
            state.SubmitRendered = true;
            // Mark the turn as ended so the post-terminator gate (top of this hook)
            // denies any further tool call. Tell the renderer the final outcome is on
            // screen so it swallows trailing prose the model may still stream. We do
            // NOT cancel the turn — cancellation surfaces as OperationCanceledException
            // and is misread as Ctrl+C; the gate + renderer suppression are enough.
            state.SubmittedThisTurn = true;
            state.OnFinalRendered?.Invoke();
            // Consume the plan + needs so the next turn (interactive only) starts
            // fresh — if the agent proposes again, state will be repopulated; if it
            // does not, the next submit_response won't redundantly re-render them.
            state.LastIssues = null;
            state.LastPlanSteps = null;
            state.LastNeedsUserInput = null;
            state.LastDefaults = null;
            return new PreToolUseHookOutput { PermissionDecision = "allow" };
        }

        // propose_plan is a mid-turn artifact — it renders a plan and never ends
        // the session (submit_response does). Validate args (deny + correction on
        // failure), then render. oneShot shows the plan and allows; interactive
        // asks y/n and, on yes, unblocks execution. Either way the agent continues
        // and must call submit_response to close.
        if (string.Equals(input.ToolName, "propose_plan", StringComparison.OrdinalIgnoreCase))
        {
            var planError = PlanArgsParser.ValidateProposePlan(input.ToolArgs);
            if (planError is not null)
                return new PreToolUseHookOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = $"Invalid propose_plan arguments: {planError}",
                    AdditionalContext = $"Your propose_plan call was rejected: {planError}. Call it again with corrected arguments.",
                };

            var issues = PlanArgsParser.ExtractIssues(input.ToolArgs);
            var planSteps = PlanArgsParser.ExtractPlanSteps(input.ToolArgs);
            var placeholders = PlanArgsParser.ExtractUserInputNeeds(input.ToolArgs, "placeholders");
            var defaults = PlanArgsParser.ExtractChosenDefaults(input.ToolArgs);
            state.LastIssues = issues;
            state.LastPlanSteps = planSteps;
            state.LastNeedsUserInput = placeholders;
            state.LastDefaults = defaults;
            state.PlanShown = true;

            // TODO (deferred from batch B): host-side required-parameter validation.
            // A RequiredParameterValidator (mirror of ExampleSyntaxValidator, driven
            // by CommandMetadata.IsRequired) would deny + force a re-plan when a step
            // omits a required flag. Deferred: it needs a command resolver
            // (IExtensionIndex) threaded into this static hook plus an Agent ->
            // Azclips.Help.Tool reference that does not exist today; wiring both here
            // is out of scope for this batch. Draft lives in the v3 review doc.

            // Unified mind: propose_plan does NOT render. submit_response is the
            // sole renderer — it shows the plan's content blocks together, then
            // closes. In interactive mode the host loop keeps going so the user
            // can answer the needs in their next message.
            if (state.IsOneShot)
                return new PreToolUseHookOutput { PermissionDecision = "allow" };

            var planApproved = await approvalSink.RequestPlanAsync(issues, planSteps, placeholders, defaults, ct).ConfigureAwait(false);
            if (planApproved)
            {
                // Session-level gate: once any plan is approved, execution is
                // unblocked for the rest of the session (stage-2 per-command
                // approval still applies).
                state.PlanApproved = true;
                return new PreToolUseHookOutput { PermissionDecision = "allow" };
            }

            // Rejected plan = the user wants a different plan, not a bare-prose
            // correction. Roll the gate back so the agent must re-propose, and tell
            // it to ask what to change (interactive can wait for the answer; in
            // one-shot the turn simply ends with no approved plan).
            state.PlanApproved = false;
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "User rejected the plan.",
                AdditionalContext = "The user rejected this plan. Ask them what to change, then propose a revised plan — do not run any command and do not reply with prose only.",
            };
        }

        // Plan-before-execute gate: the agent must get a plan approved before it
        // runs any shell command. Without it, deny and tell the LLM to call
        // propose_plan first (round-trips via additionalContext).
        if ((string.Equals(input.ToolName, "bash",       StringComparison.OrdinalIgnoreCase) ||
             string.Equals(input.ToolName, "powershell", StringComparison.OrdinalIgnoreCase)) &&
            !state.PlanApproved)
        {
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "Execution requires an approved plan first.",
                AdditionalContext = "You must call propose_plan and get the user's approval before running any command.",
            };
        }

        // Both built-in shell tools take a "command" arg and may contain a bare
        // "az" that PowerShell would resolve via PATH. Rewrite to the absolute
        // az.exe path so the agent always invokes the azclips binary that
        // launched it, regardless of what else is on PATH.
        if (!string.Equals(input.ToolName, "bash",       StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(input.ToolName, "powershell", StringComparison.OrdinalIgnoreCase))
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

        var rewritten = RewriteAz(command, azExePath);
        if (ReferenceEquals(rewritten, command))
            return null;

        // SDK semantics: "allow" honors ModifiedArgs but skips OnPermissionRequest;
        // "ask" prompts via OnPermissionRequest but discards ModifiedArgs. To get
        // both rewrite AND user approval, take the approval here, then "allow".
        var approved = await approvalSink.RequestRewriteAsync(input.ToolName!, rewritten, ct).ConfigureAwait(false);
        if (!approved)
        {
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "User rejected the rewritten az command.",
            };
        }

        return new PreToolUseHookOutput
        {
            PermissionDecision = "allow",
            ModifiedArgs = MergeCommand(input.ToolArgs, rewritten),
        };
    }

    // ModifiedArgs replaces the tool args wholesale, so preserve every original
    // field (e.g. powershell's required "description") and overwrite only "command".
    private static Dictionary<string, object?> MergeCommand(object? originalArgs, string rewrittenCommand)
    {
        var merged = new Dictionary<string, object?>();
        if (originalArgs is JsonElement je)
        {
            JsonDocument? parsedDoc = null;
            try
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    try { parsedDoc = JsonDocument.Parse(je.GetString()!); je = parsedDoc.RootElement; }
                    catch { /* fall through with empty merged */ }
                }
                if (je.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in je.EnumerateObject())
                        merged[prop.Name] = prop.Value.Clone();
                }
            }
            finally
            {
                parsedDoc?.Dispose();
            }
        }
        else if (originalArgs is IDictionary<string, object?> dict)
        {
            foreach (var kv in dict) merged[kv.Key] = kv.Value;
        }
        merged["command"] = rewrittenCommand;
        return merged;
    }
    // A turn-closing tool satisfies the act-through-tools contract: propose_plan
    // (renders a plan) or submit_response (ends the session). A turn that only
    // queried (get_command/bash/find_*) then replied in prose is still a violation.
    //
    // In one-shot mode propose_plan does NOT count as closing: the agent must go on
    // to call submit_response to render the plan, so a propose-only turn should be
    // nudged forward, not let through (otherwise the plan is never shown — it lives
    // in LastPlanMarkdown with nobody to render it). In interactive mode propose_plan
    // DOES count (it stops for y/n approval, which is the expected pause).
    // internal for unit testing (see ClosingToolPredicateTests).
    internal static bool IsClosingTool(string? toolName, bool isOneShot = false) =>
        IsSessionTerminator(toolName) ||
        (!isOneShot && string.Equals(toolName, "propose_plan", StringComparison.OrdinalIgnoreCase));

    // The session terminator under the unified-mind model: only submit_response
    // ends a session. propose_plan is a mid-turn artifact and is NOT a terminator.
    // internal for unit testing (see ClosingToolPredicateTests).
    internal static bool IsSessionTerminator(string? toolName) =>
        string.Equals(toolName, "submit_response", StringComparison.OrdinalIgnoreCase);
    private static string? ExtractBashCommand(object? args)
    {
        if (args is null) return null;

        // SDK may hand us the args as either a JSON object (normal case) or a
        // JSON-encoded string of an object (observed for the built-in
        // powershell tool). Handle both shapes uniformly.
        if (args is JsonElement je)
        {
            JsonDocument? parsedDoc = null;
            try
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    try { parsedDoc = JsonDocument.Parse(je.GetString()!); je = parsedDoc.RootElement; }
                    catch { return null; }
                }
                if (je.ValueKind == JsonValueKind.Object &&
                    je.TryGetProperty("command", out var cmd) && cmd.ValueKind == JsonValueKind.String)
                    return cmd.GetString();
                return null;
            }
            finally
            {
                parsedDoc?.Dispose();
            }
        }
        if (args is IDictionary<string, object?> dict && dict.TryGetValue("command", out var v))
            return v?.ToString();
        return null;
    }

    private static string RewriteAz(string command, string azExePath)
    {
        var trimmed = command.TrimStart();
        if (!trimmed.StartsWith("az ", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Equals("az", StringComparison.OrdinalIgnoreCase))
            return command;

        var rest = trimmed.Length > 2 ? trimmed[3..] : string.Empty;
        var quoted = azExePath.Contains(' ') ? $"\"{azExePath}\"" : azExePath;
        return string.IsNullOrEmpty(rest) ? quoted : $"{quoted} {rest}";
    }

    private static bool IsReadOnlyCacheTool(string toolName) =>
        toolName.StartsWith("find_", StringComparison.OrdinalIgnoreCase) ||
        toolName.StartsWith("get_",  StringComparison.OrdinalIgnoreCase) ||
        toolName.StartsWith("list_", StringComparison.OrdinalIgnoreCase);

    private static readonly string[] ValidEntrypoints = ["oneshot", "inline", "tui"];

    private static string LoadSystemPrompt(string flavor, string entrypoint)
    {
        if (!ValidEntrypoints.Contains(entrypoint, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"entrypoint must be one of {string.Join(" | ", ValidEntrypoints)} — refusing to guess which system prompt to load. Got: '{entrypoint}'.",
                nameof(entrypoint));

        var path = Path.Combine(AppContext.BaseDirectory, "prompts", $"{entrypoint.ToLowerInvariant()}.md");
        var content = File.ReadAllText(path);
        return content.Replace("{{flavor}}", flavor, StringComparison.OrdinalIgnoreCase);
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
