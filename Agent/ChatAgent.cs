using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Hooks;
using Core.Agent.Hosting;
using Core.Agent.Probes;
using Core.Agent.Tools;
using Core.Agent.Tools.Cache;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Sku;
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
    private int _sendCalledSubmit;

    public string Model => _model;

    public SessionErrored? LastError { get; private set; }

    // True when the most recent SendAsync called a turn-closing tool (propose_plan
    // or submit_response). A turn that ends without one — even if it called
    // get_command/bash — replied in prose and broke the act-through-tools contract.
    public bool LastSendCalledClosingTool => Volatile.Read(ref _sendCalledClosingTool) != 0;

    // True when the most recent SendAsync called submit_response (session-ending).
    public bool LastSendCalledSubmit => Volatile.Read(ref _sendCalledSubmit) != 0;

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
        (_state?.IsOneShot ?? true) && ((_state?.SubmitRendered ?? false) || LastSendCalledSubmit);

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
        IAuthContextService authContext,
        string azv2ExePath,
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
                client, flavor, auth, authContext, azv2ExePath, approvalSink,
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
        IAuthContextService authContext,
        string azv2ExePath,
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
        var skuTools = new AvailableLocationsTool(auth, authContext);
        var submitTool = new SubmitResponseTool();
        var planTool = new ProposePlanTool();
        var prober = new EnvironmentProber(authContext, flavor);

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
                    if (IsSessionTerminator(ts.Name))
                        Volatile.Write(ref self._sendCalledSubmit, 1);
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
        var failedHelp = BuildFailedCommandContext(helpTools, failedCommand);
        var sessionConfig = BuildBaseSessionConfig(
            systemPrompt, cacheTools, helpTools, skuTools, submitTool, planTool, prober,
            azv2ExePath, approvalSink, eventRenderer, state, failedHelp, ct);

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
        string azv2ExePath,
        ConsoleApprovalPrompt approvalSink,
        EventRenderer eventRenderer,
        SessionState state,
        string? failedHelp,
        CancellationToken ct)
    {
        return new SessionConfig
        {
            SessionId = Guid.NewGuid().ToString(),
            ConfigDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cirrus"),
            Streaming = true,
            SystemMessage = new SystemMessageConfig { Content = systemPrompt },
            Tools = cacheTools.All.Concat(helpTools.All).Concat(skuTools.All)
                .Concat(submitTool.All).Concat(planTool.All).Cast<AIFunctionDeclaration>().ToList(),
            // All built-in tools available (bash, view, edit, grep, glob,
            // web_fetch, web_search, todo, ...) plus our custom cache + help tools.
            AvailableTools = null,
            Hooks = new SessionHooks
            {
                OnPreToolUse = (input, _) => HandlePreToolUseAsync(input, azv2ExePath, approvalSink, state, ct),
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
            azv2 login --subscription-id <your-subscription-id>

        Alternative: switch to the 'github' provider (uses your own GitHub Copilot subscription,
        billed to your GitHub account):
            azv2 config set --key agent.provider --value github
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
        Volatile.Write(ref _sendCalledSubmit, 0);
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
    // on bash/powershell, and rewrites bare azv2 to the absolute exe path.
    // internal (not private) so PlanGateTests can drive it with a real SessionState.
    internal static async Task<PreToolUseHookOutput?> HandlePreToolUseAsync(
        PreToolUseHookInput input,
        string azv2ExePath,
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
        // recognizes this turn (via LastSendCalledSubmit) as the clean close.
        if (string.Equals(input.ToolName, "submit_response", StringComparison.OrdinalIgnoreCase))
        {
            var submitError = ValidateSubmitResponse(input.ToolArgs);
            if (submitError is not null)
                return new PreToolUseHookOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = $"Invalid submit_response arguments: {submitError}",
                    AdditionalContext = $"Your submit_response call was rejected: {submitError}. Call it again with corrected arguments.",
                };

            var status = TryGetStringArg(input.ToolArgs, "status") ?? string.Empty;
            var message = TryGetStringArg(input.ToolArgs, "message");
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
            var planError = ValidateProposePlan(input.ToolArgs);
            if (planError is not null)
                return new PreToolUseHookOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = $"Invalid propose_plan arguments: {planError}",
                    AdditionalContext = $"Your propose_plan call was rejected: {planError}. Call it again with corrected arguments.",
                };

            var issues = ExtractIssues(input.ToolArgs);
            var planSteps = ExtractPlanSteps(input.ToolArgs);
            var placeholders = ExtractUserInputNeeds(input.ToolArgs, "placeholders");
            var defaults = ExtractChosenDefaults(input.ToolArgs);
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
            // Cirrus.Help.Tool reference that does not exist today; wiring both here
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
        // "azv2" that PowerShell would resolve via PATH. Rewrite to the absolute
        // azv2.exe path so the agent always invokes the cirrus binary that
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

        var rewritten = RewriteAzv2(command, azv2ExePath);
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
                PermissionDecisionReason = "User rejected the rewritten azv2 command.",
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

    private static readonly string[] ValidSubmitStatuses = ["completed", "rejected", "failed"];

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

    // Returns null when propose_plan args are valid, else an error message. Same
    // hook-not-body validation rationale as submit_response. internal for tests.
    internal static string? ValidateProposePlan(object? args)
    {
        if (!TryGetArrayArg(args, "issues", out var issues) || issues.GetArrayLength() == 0)
            return "issues must have at least one point";
        foreach (var issue in issues.EnumerateArray())
        {
            if (issue.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(issue.GetString()))
                return "each issue must be a non-empty string";
        }

        if (!TryGetArrayArg(args, "steps", out var steps) || steps.GetArrayLength() == 0)
            return "steps must have at least one entry";

        int i = 0;
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object)
                return $"step {i} is not an object";
            if (!HasStringProp(step, "label"))
                return $"step {i} missing required field 'label'";
            if (!HasStringProp(step, "cmd"))
                return $"step {i} missing required field 'cmd'";
            i++;
        }

        return null;
    }

    private static bool HasStringProp(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(v.GetString());

    // Parses the (already-validated) steps array into the host's PlanStep view
    // for rendering. Missing/odd fields degrade gracefully — validation has
    // already guaranteed label+cmd are present for a plan that reaches approval.
    // A step is destructive when the model said so OR its command contains a
    // danger verb (host-side double-safety). internal for unit testing.
    internal static IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? ExtractPlanSteps(object? args)
    {
        if (!TryGetArrayArg(args, "steps", out var steps)) return null;

        var list = new List<ConsoleApprovalPrompt.PlanStep>();
        int fallbackId = 1;
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object) { fallbackId++; continue; }
            var id = step.TryGetProperty("id", out var idEl)
                && idEl.ValueKind == JsonValueKind.Number
                && idEl.TryGetInt32(out var parsed) ? parsed : fallbackId;
            var label = step.TryGetProperty("label", out var l) ? l.GetString() ?? string.Empty : string.Empty;
            var cmd = step.TryGetProperty("cmd", out var c) ? c.GetString() ?? string.Empty : string.Empty;
            // destructive is a schema boolean — trust it. A danger verb in the
            // command is an independent safety backstop, not a format fallback.
            var modelDestructive = step.TryGetProperty("destructive", out var d)
                && d.ValueKind == JsonValueKind.True;
            var destructive = modelDestructive || HasDangerVerb(cmd);
            list.Add(new ConsoleApprovalPrompt.PlanStep(id, label, cmd, destructive));
            fallbackId++;
        }
        return list.Count > 0 ? list : null;
    }

    // Builds the "here's the help for the command you failed on" context block the
    // host injects at session start, so the agent doesn't spend a turn looking up
    // the command it was triggered by.
    //
    // Conservative by design — only fires when the failed input actually looks like
    // an azv2 command invocation. Natural-language triggers ("create me a VM") or
    // odd input fall through to null (no injection), and the agent just uses its
    // tools as before — never worse than today.
    //
    // Goes through HelpTools.LookupCommand, the same path the get_command tool uses,
    // so when the help source switches to the extension index this block changes
    // with it automatically (no separate code path to keep in sync).
    internal static string? BuildFailedCommandContext(HelpTools helpTools, string? failedCommand)
    {
        var name = ExtractCommandName(failedCommand);
        if (name is null) return null;

        try
        {
            // Same JSON the LLM's get_command returns. On a hit it's the parameter
            // table — inject it. On a miss it's not_found: the verb we extracted
            // wasn't a real command (e.g. a multi-word command whose trailing
            // positional arg got swept in), so injecting "not_found" would just
            // mislead. Skip injection and let the agent look it up with its tools.
            var help = helpTools.LookupCommand(name);
            if (IsNotFound(help))
                return null;
            return $"Help for the command you were triggered by (`{name}`), pre-fetched so you don't need to call get_command for it:\n{help}";
        }
        catch
        {
            // Pre-fetch is best-effort: if the lookup throws (today it can't, but a
            // future index-backed resolver might), fall back to no injection rather
            // than killing agent startup. Never worse than not pre-fetching.
            return null;
        }
    }

    // True when the get_command JSON is a not_found result. Parses the JSON and
    // checks the "error" field, instead of a raw substring match — so a reformatted
    // payload or the literal "not_found" appearing inside help text can't fool it.
    private static bool IsNotFound(string helpJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(helpJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("error", out var err)
                && err.ValueKind == JsonValueKind.String
                && string.Equals(err.GetString(), "not_found", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Not parseable as the expected JSON — treat as "not a not_found result"
            // (a real help payload is always valid JSON; if it's malformed, don't
            // suppress it on a guess).
            return false;
        }
    }

    // Pulls the command verb out of a failed azv2 invocation: the leading run of
    // command-name tokens (lowercase letters/digits/hyphens) up to the first flag.
    // Returns null when the input doesn't look like a command (natural language,
    // empty, starts with a flag) — the caller then skips pre-fetch entirely.
    internal static string? ExtractCommandName(string? failedCommand)
    {
        if (string.IsNullOrWhiteSpace(failedCommand)) return null;

        var verb = new List<string>();
        foreach (var token in failedCommand.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith('-')) break;                 // first flag ends the verb
            if (!IsCommandToken(token)) return null;          // non-command token (e.g. CJK / punctuation) → not a command
            verb.Add(token);
            if (verb.Count >= 4) break;                       // cirrus verbs are short; cap to avoid eating args
        }
        return verb.Count > 0 ? string.Join(' ', verb) : null;
    }

    // A command-name token is lowercase ascii letters/digits/hyphens (e.g. "vm",
    // "create", "create-combined"). Anything else (uppercase value, path, CJK,
    // quotes) means the input isn't a clean command invocation.
    private static bool IsCommandToken(string token) =>
        token.Length > 0 && token.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-');

    // Parses an array of {name, description} (placeholders) into the host's
    // UserInputNeed view. Returns null when absent or empty. internal for tests.
    internal static IReadOnlyList<ConsoleApprovalPrompt.UserInputNeed>? ExtractUserInputNeeds(object? args, string field)
    {
        if (!TryGetArrayArg(args, field, out var needs)) return null;

        var list = new List<ConsoleApprovalPrompt.UserInputNeed>();
        foreach (var need in needs.EnumerateArray())
        {
            if (need.ValueKind != JsonValueKind.Object) continue;
            var name = need.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() : null;
            var description = need.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(name)) continue;
            list.Add(new ConsoleApprovalPrompt.UserInputNeed(name!, description));
        }
        return list.Count > 0 ? list : null;
    }

    // Parses the propose_plan issues array (what-went-wrong points) into a string
    // list. Skips non-string / blank entries. Returns null when absent or empty.
    // internal for unit testing.
    internal static IReadOnlyList<string>? ExtractIssues(object? args)
    {
        if (!TryGetArrayArg(args, "issues", out var issues)) return null;

        var list = new List<string>();
        foreach (var issue in issues.EnumerateArray())
        {
            if (issue.ValueKind == JsonValueKind.String && issue.GetString() is { Length: > 0 } s
                && !string.IsNullOrWhiteSpace(s))
                list.Add(s);
        }
        return list.Count > 0 ? list : null;
    }

    // Parses the propose_plan defaults array ({name, value, note}) into the host's
    // ChosenDefault view — values the agent chose/derived for the user. Returns
    // null when absent or empty. internal for unit testing.
    internal static IReadOnlyList<ConsoleApprovalPrompt.ChosenDefault>? ExtractChosenDefaults(object? args)
    {
        if (!TryGetArrayArg(args, "defaults", out var defaults)) return null;

        var list = new List<ConsoleApprovalPrompt.ChosenDefault>();
        foreach (var d in defaults.EnumerateArray())
        {
            if (d.ValueKind != JsonValueKind.Object) continue;
            var name = d.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            var value = d.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
            var note = d.TryGetProperty("note", out var nt) && nt.ValueKind == JsonValueKind.String ? nt.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(name)) continue;
            list.Add(new ConsoleApprovalPrompt.ChosenDefault(name!, value, note));
        }
        return list.Count > 0 ? list : null;
    }

    // Danger verbs that mark a command destructive even when the model didn't.
    // Matched as whole words so "undelete" or a resource named "removexyz" in a
    // path don't trip it; case-insensitive.
    private static readonly string[] DangerVerbs =
        ["delete", "remove", "purge", "destroy", "rm", "drop", "wipe", "erase", "format", "uninstall"];

    private static bool HasDangerVerb(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return false;
        foreach (var token in cmd.Split([' ', '\t', '\n', '\r', '-', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var verb in DangerVerbs)
                if (string.Equals(token, verb, StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        return false;
    }

    private static bool TryGetArrayArg(object? args, string field, out JsonElement array)
    {
        array = default;
        if (args is not JsonElement je) return false;

        JsonDocument? parsedDoc = null;
        try
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                try { parsedDoc = JsonDocument.Parse(je.GetString()!); je = parsedDoc.RootElement; }
                catch { return false; }
            }
            if (je.ValueKind == JsonValueKind.Object &&
                je.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.Array)
            {
                // Clone so the value outlives the parsed document we dispose below.
                array = v.Clone();
                return true;
            }
            return false;
        }
        finally
        {
            parsedDoc?.Dispose();
        }
    }

    // Returns null when submit_response args are valid, else an error message.
    // Keeps validation in the hook (not the tool body) so a bad call becomes a
    // deny + additionalContext correction prompt instead of a framework exception.
    // internal for direct unit testing (see SubmitResponseValidationTests).
    internal static string? ValidateSubmitResponse(object? args)
    {
        var status = TryGetStringArg(args, "status");
        var message = TryGetStringArg(args, "message");

        if (string.IsNullOrWhiteSpace(status))
            return "missing required field 'status' (expected completed | rejected | failed)";
        if (!ValidSubmitStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            return $"unknown status '{status}'; valid: completed | rejected | failed";

        var isReject = string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase);
        var isFail   = string.Equals(status, "failed",   StringComparison.OrdinalIgnoreCase);
        if ((isReject || isFail) && string.IsNullOrWhiteSpace(message))
            return $"'message' is required when status='{status}'";
        if (isReject && !message!.StartsWith("OUT_OF_SCOPE_", StringComparison.OrdinalIgnoreCase))
            return "rejected 'message' must start with OUT_OF_SCOPE_";

        return null;
    }

    // Reads one string field from the SDK's ToolArgs, which arrive either as a
    // JSON object or a JSON-encoded string of an object (same dual shape as
    // ExtractBashCommand handles). Returns null when absent or not a string.
    private static string? TryGetStringArg(object? args, string field)
    {
        if (args is not JsonElement je) return null;

        JsonDocument? parsedDoc = null;
        try
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                try { parsedDoc = JsonDocument.Parse(je.GetString()!); je = parsedDoc.RootElement; }
                catch { return null; }
            }
            if (je.ValueKind == JsonValueKind.Object &&
                je.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
            return null;
        }
        finally
        {
            parsedDoc?.Dispose();
        }
    }

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

/// <summary>
/// Per-session mutable state shared with the OnPreToolUse hook. Holds the plan
/// gate and the turn-scoped cancellation source; it is session-scoped (one
/// instance per ChatAgent session) and not thread-safe by design — the SDK
/// invokes the hook serially per session.
/// </summary>
internal sealed class SessionState
{
    // True once the user has approved any plan this session. Gates bash/powershell.
    public bool PlanApproved { get; set; }

    // oneShot: single turn, plan then submit_response closes. interactive: plan is
    // approved via y/n and execution proceeds, then submit_response closes.
    public bool IsOneShot { get; set; }

    // Set when any plan was rendered this session. Informational only —
    // submit_response is the sole terminator, so this no longer ends the turn.
    public bool PlanShown { get; set; }

    // The plan content from the most recent propose_plan, attached to the next
    // submit_response render so the plan's content blocks show together.
    // (one-shot also re-shows these on exit as a fallback if submit never ran.)
    public IReadOnlyList<string>? LastIssues { get; set; }
    public IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? LastPlanSteps { get; set; }
    public IReadOnlyList<ConsoleApprovalPrompt.UserInputNeed>? LastNeedsUserInput { get; set; }
    public IReadOnlyList<ConsoleApprovalPrompt.ChosenDefault>? LastDefaults { get; set; }

    // Invoked by the submit_response hook right after the final outcome is
    // rendered. Lets the renderer (TraceAgentRenderer) flip into "suppress
    // trailing prose" mode so the model can't slip a stray paragraph past the
    // terminator. Null = no suppression wired (non-trace renderers).
    public Action? OnFinalRendered { get; set; }

    // Set once the host has rendered a submit_response outcome. The host reads it
    // to recognize the session terminated cleanly via submit_response.
    public bool SubmitRendered { get; set; }

    // True once submit_response has rendered THIS turn. Reset at the start of every
    // SendAsync. The pre-tool-use hook denies any further tool call once it's set,
    // so the model can't keep acting after the terminator — without cancelling the
    // turn (cancellation surfaces as OperationCanceledException, misread as Ctrl+C).
    public bool SubmittedThisTurn { get; set; }
}
