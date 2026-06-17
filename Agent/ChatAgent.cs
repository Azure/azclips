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
    private int _llmTurnCount;

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

    // True once a oneShot plan has been rendered. In oneShot the plan IS the
    // terminal output, so the host loop exits immediately after the turn that
    // sets this — the agent does not get to continue or emit a submit_response.
    public bool OneShotPlanShown => _state?.OneShotPlanShown ?? false;

    // Raised after each completed LLM turn with the running turn count for this
    // session. The host uses it to drive the interactive escape valve.
    public event Action<int>? LlmTurnCompletedCount;

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
                if (ev is LlmTurnCompleted && self is not null)
                {
                    var n = ++self._llmTurnCount;
                    self.LlmTurnCompletedCount?.Invoke(n);
                }
                if (ev is ToolStarted ts && self is not null)
                {
                    var isSubmit = string.Equals(ts.Name, "submit_response", StringComparison.OrdinalIgnoreCase);
                    var isPlan   = string.Equals(ts.Name, "propose_plan",    StringComparison.OrdinalIgnoreCase);
                    if (isSubmit || isPlan)
                        Volatile.Write(ref self._sendCalledClosingTool, 1);
                    if (isSubmit)
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
            IsOneShot = !string.Equals(options.SessionStyle, "interactive", StringComparison.OrdinalIgnoreCase),
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
        // oneShot plan is terminal — let the hook cancel the in-flight turn the
        // moment it renders the plan, so the agent can't continue to submit_response
        // or a prose summary.
        state.AbortTurn = () => { try { agent._activeTurnCts?.Cancel(); } catch { } };
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

    private static async Task<PreToolUseHookOutput?> HandlePreToolUseAsync(
        PreToolUseHookInput input,
        string azv2ExePath,
        ConsoleApprovalPrompt approvalSink,
        SessionState state,
        CancellationToken ct)
    {
        // submit_response is the agent's own session-termination signal — no user
        // approval needed (the LLM already decided to end). Validate its args here
        // and, on failure, deny + feed the reason back as additionalContext so the
        // LLM self-corrects on the next turn (verified to round-trip with the
        // current SDK). Host-side final rendering hooks in here in a later change.
        if (string.Equals(input.ToolName, "submit_response", StringComparison.OrdinalIgnoreCase))
        {
            if (ValidateSubmitResponse(input.ToolArgs, out var submitError))
                return new PreToolUseHookOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = $"Invalid submit_response arguments: {submitError}",
                    AdditionalContext = $"Your submit_response call was rejected: {submitError}. Call it again with corrected arguments.",
                };

            return new PreToolUseHookOutput { PermissionDecision = "allow" };
        }

        // propose_plan is the stage-1 plan. Validate args first (deny + correction
        // on failure). Then render the plan. In oneShot the plan is show-and-exit:
        // render it, mark the session done, and deny so the agent stops (no
        // execution, no submit_response — the plan IS the final output). In
        // interactive, ask for y/n approval; on yes, unblock execution.
        if (string.Equals(input.ToolName, "propose_plan", StringComparison.OrdinalIgnoreCase))
        {
            if (ValidateProposePlan(input.ToolArgs, out var planError))
                return new PreToolUseHookOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = $"Invalid propose_plan arguments: {planError}",
                    AdditionalContext = $"Your propose_plan call was rejected: {planError}. Call it again with corrected arguments.",
                };

            var planMd = TryGetStringArg(input.ToolArgs, "planMd") ?? string.Empty;
            var planSteps = ExtractPlanSteps(input.ToolArgs);

            if (state.IsOneShot)
            {
                approvalSink.ShowPlan(planMd, planSteps);
                state.OneShotPlanShown = true;
                // The plan is the terminal output in oneShot — cancel the turn now
                // so the agent doesn't continue to submit_response or a prose
                // summary. The deny is a backstop in case cancellation races.
                state.AbortTurn?.Invoke();
                return new PreToolUseHookOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = "Plan shown to the user (one-shot mode).",
                };
            }

            var planApproved = await approvalSink.RequestPlanAsync(planMd, planSteps, ct).ConfigureAwait(false);
            if (planApproved)
            {
                // Session-level gate: once any plan is approved, execution is
                // unblocked for the rest of the session. Re-planning does not
                // re-arm the gate (stage-2 per-command approval still applies).
                // REVIEW: v2 may scope approval per-plan via an ApprovedPlanId
                // instead of this single bool (see schema doc decision 7 TODO).
                state.PlanApproved = true;
                return new PreToolUseHookOutput { PermissionDecision = "allow" };
            }
            return new PreToolUseHookOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "User did not approve the plan.",
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

    // Returns true when propose_plan args are INVALID and sets `error`. Same
    // hook-not-body validation rationale as submit_response. internal for tests.
    internal static bool ValidateProposePlan(object? args, out string error)
    {
        var planMd = TryGetStringArg(args, "planMd");
        if (string.IsNullOrWhiteSpace(planMd))
        {
            error = "planMd is empty";
            return true;
        }

        if (!TryGetArrayArg(args, "steps", out var steps) || steps.GetArrayLength() == 0)
        {
            error = "steps must have at least one entry";
            return true;
        }

        int i = 0;
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object)
            {
                error = $"step {i} is not an object";
                return true;
            }
            if (!HasStringProp(step, "label"))
            {
                error = $"step {i} missing required field 'label'";
                return true;
            }
            if (!HasStringProp(step, "cmd"))
            {
                error = $"step {i} missing required field 'cmd'";
                return true;
            }
            i++;
        }

        error = string.Empty;
        return false;
    }

    private static bool HasStringProp(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(v.GetString());

    // Parses the (already-validated) steps array into the host's PlanStep view
    // for rendering. Missing/odd fields degrade gracefully — validation has
    // already guaranteed label+cmd are present for a plan that reaches approval.
    // internal for unit testing.
    internal static IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? ExtractPlanSteps(object? args)
    {
        if (!TryGetArrayArg(args, "steps", out var steps)) return null;

        var list = new List<ConsoleApprovalPrompt.PlanStep>();
        int fallbackId = 1;
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object) { fallbackId++; continue; }
            var id = step.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var parsed) ? parsed : fallbackId;
            var label = step.TryGetProperty("label", out var l) ? l.GetString() ?? string.Empty : string.Empty;
            var cmd = step.TryGetProperty("cmd", out var c) ? c.GetString() ?? string.Empty : string.Empty;
            list.Add(new ConsoleApprovalPrompt.PlanStep(id, label, cmd));
            fallbackId++;
        }
        return list.Count > 0 ? list : null;
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

    // Returns true when submit_response args are INVALID and sets `error`. Keeps
    // validation in the hook (not the tool body) so a bad call becomes a deny +
    // additionalContext correction prompt instead of a framework exception.
    // internal for direct unit testing (see SubmitResponseValidationTests).
    internal static bool ValidateSubmitResponse(object? args, out string error)
    {
        var status = TryGetStringArg(args, "status");
        var message = TryGetStringArg(args, "message");

        if (string.IsNullOrWhiteSpace(status))
        {
            error = "missing required field 'status' (expected completed | rejected | failed)";
            return true;
        }
        if (!ValidSubmitStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            error = $"unknown status '{status}'; valid: completed | rejected | failed";
            return true;
        }

        var isReject = string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase);
        var isFail   = string.Equals(status, "failed",   StringComparison.OrdinalIgnoreCase);
        if ((isReject || isFail) && string.IsNullOrWhiteSpace(message))
        {
            error = $"'message' is required when status='{status}'";
            return true;
        }
        if (isReject && !message!.StartsWith("OUT_OF_SCOPE_", StringComparison.Ordinal))
        {
            error = "rejected 'message' must start with OUT_OF_SCOPE_";
            return true;
        }

        error = string.Empty;
        return false;
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
/// Per-session mutable state shared with the OnPreToolUse hook. PP-stage this is
/// just the plan gate; it is session-scoped (one instance per ChatAgent session)
/// and not thread-safe by design — the SDK invokes the hook serially per session.
/// </summary>
internal sealed class SessionState
{
    // True once the user has approved any plan this session. Gates bash/powershell.
    public bool PlanApproved { get; set; }

    // oneShot: plan is show-and-exit (no per-plan approval, no execution). The
    // hook renders the plan then denies + tells the agent to stop. interactive:
    // plan is approved via y/n and execution proceeds.
    public bool IsOneShot { get; set; }

    // Set by the hook when a oneShot plan was rendered — the host reads this
    // (via ChatAgent.OneShotPlanShown) to exit right after the turn.
    public bool OneShotPlanShown { get; set; }

    // Cancels the in-flight turn. The oneShot plan is the terminal output, so the
    // hook calls this right after rendering it — otherwise the agent would keep
    // going (call submit_response, emit a prose summary) before SendAsync returns.
    public Action? AbortTurn { get; set; }
}
