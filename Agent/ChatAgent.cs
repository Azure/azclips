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

    // True once submit_response rendered its final outcome — the clean session
    // terminus. The host loop ends the session when it sees this.
    public bool SessionClosed => (_state?.SubmitRendered ?? false) || LastSendCalledSubmit;

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
                renderer, options, entrypoint, spinner, ct).ConfigureAwait(false);
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
                    if (IsClosingTool(ts.Name))
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
        var sessionConfig = BuildBaseSessionConfig(
            systemPrompt, cacheTools, helpTools, skuTools, submitTool, planTool, prober,
            azv2ExePath, approvalSink, eventRenderer, state, ct);

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

            case "custom":
                throw new NotImplementedException(
                    "agent.provider='custom' is reserved for GA; use 'builtin' or 'github'.");

            default:
                throw new ArgumentException(
                    $"Unknown agent.provider value: {FormatProviderForError(options.Provider)}. Expected: builtin | github | custom.",
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
        var turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var oldCts = Interlocked.Exchange(ref _activeTurnCts, turnCts);
        if (_state is not null) _state.ActiveTurnCts = turnCts;
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
            if (_state is not null && ReferenceEquals(_state.ActiveTurnCts, done)) _state.ActiveTurnCts = null;
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
            // In one-shot a completed response has no separate summary — the plan
            // the agent just showed IS the deliverable, so echo it as the summary.
            var planSummary = state.IsOneShot ? state.LastPlanMarkdown : null;
            approvalSink.ShowFinalOutcome(status, message, planSummary);
            state.SubmitRendered = true;
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

            var planMd = TryGetStringArg(input.ToolArgs, "planMd") ?? string.Empty;
            var planSteps = ExtractPlanSteps(input.ToolArgs);
            state.LastPlanMarkdown = planMd;
            state.PlanShown = true;

            if (state.IsOneShot)
            {
                // Render the plan and allow — the agent closes with submit_response,
                // whose completed summary echoes this plan back to the user.
                approvalSink.ShowPlan(planMd, planSteps);
                return new PreToolUseHookOutput { PermissionDecision = "allow" };
            }

            var planApproved = await approvalSink.RequestPlanAsync(planMd, planSteps, ct).ConfigureAwait(false);
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
    // internal for unit testing (see ClosingToolPredicateTests).
    internal static bool IsClosingTool(string? toolName) =>
        IsSessionTerminator(toolName) ||
        string.Equals(toolName, "propose_plan", StringComparison.OrdinalIgnoreCase);

    // The session terminator under the unified-mind model: only submit_response
    // ends a session. propose_plan is a mid-turn artifact and is NOT a terminator.
    // internal for unit testing (see ClosingToolPredicateTests).
    internal static bool IsSessionTerminator(string? toolName) =>
        string.Equals(toolName, "submit_response", StringComparison.OrdinalIgnoreCase);

    // Returns null when propose_plan args are valid, else an error message. Same
    // hook-not-body validation rationale as submit_response. internal for tests.
    internal static string? ValidateProposePlan(object? args)
    {
        var planMd = TryGetStringArg(args, "planMd");
        if (string.IsNullOrWhiteSpace(planMd))
            return "planMd is empty";

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
            var modelDestructive = step.TryGetProperty("destructive", out var d)
                && (d.ValueKind == JsonValueKind.True
                    || (d.ValueKind == JsonValueKind.String && bool.TryParse(d.GetString(), out var db) && db));
            var rationale = step.TryGetProperty("rationale", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString() : null;
            var destructive = modelDestructive || HasDangerVerb(cmd);
            list.Add(new ConsoleApprovalPrompt.PlanStep(id, label, cmd, destructive, rationale));
            fallbackId++;
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

    // The markdown of the most recently shown plan. submit_response(completed) in
    // oneShot reuses it as the success summary so the plan is echoed as the outcome.
    public string? LastPlanMarkdown { get; set; }

    // Set once the host has rendered a submit_response outcome. The host reads it
    // to recognize the session terminated cleanly via submit_response.
    public bool SubmitRendered { get; set; }

    // The current turn's linked CTS, assigned by SendAsync. AbortTurn cancels it
    // without capturing the agent, so its wiring no longer depends on the order in
    // which the agent is constructed relative to the session.
    public CancellationTokenSource? ActiveTurnCts { get; set; }

    // Cancels the in-flight turn (Ctrl+C / abort). After unification the plan no
    // longer self-terminates, so this fires only on a real abort, not on plan render.
    public void AbortTurn()
    {
        try { ActiveTurnCts?.Cancel(); } catch { }
    }
}
