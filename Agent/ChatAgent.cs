using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Agent.Common;
using Core.Agent.Hooks;
using Core.Agent.Hosting;
using Core.Agent.Plan;
using Core.Agent.AgentMode;
using Core.Agent.Providers;
using Core.Agent.Telemetry;
using Core.Agent.Tools;
using Core.Agent.Tools.Env;
using Core.Agent.Tools.Resource.Cache;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Sku;
using Core.Agent.Tools.Shell;
using Core.Telemetry;
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

    private readonly CopilotSession _session;
    private readonly AgentModelTelemetry _modelTelemetry;
    private readonly SessionState? _state;
    private readonly AgentInvocationContext _invocationContext;
    private readonly AgentSdkLifetime _sdkLifetime;

    // Tracks, for the in-flight SendAsync, whether the agent called a turn-closing
    // tool — propose_plan (asks for plan approval) or submit_response (ends the
    // session). Diagnostic/help tools like get_command DON'T count: a turn that
    // only queried then replied in prose is still a protocol violation. The host
    // reads this after SendAsync to force a correction on a bare-prose turn.
    private int _sendCalledClosingTool;

    public string Model => _modelTelemetry.Current.Model ?? string.Empty;
    internal ITelemetryService Telemetry => _invocationContext.Telemetry;

    internal void RecordModel(Activity? activity) => _modelTelemetry.RecordServedTo(activity);

    private SessionErrored? _lastError;
    private SessionErrored? _startupError;
    private int _hasSent;

    /// <summary>The terminal SDK error for the current turn, or null.</summary>
    /// <remarks>
    /// Only errors received while a send is active are kept.
    /// SDK error events carry no turn ID, so an earlier turn's error can still
    /// land during a later send.
    /// </remarks>
    public SessionErrored? LastError => Volatile.Read(ref _lastError);

    /// <summary>A session error received before the first turn.</summary>
    /// <remarks>
    /// Kept separate because each send clears <see cref="LastError"/>.
    /// Diagnostic only; the final outcome depends on the last turn,
    /// not a recoverable startup error.
    /// </remarks>
    public SessionErrored? StartupError => Volatile.Read(ref _startupError);

    // The gateway this session calls and why; null for the github provider.
    internal BuiltinEndpoint? Endpoint { get; private set; }

    // True when the most recent SendAsync called a turn-closing tool (propose_plan
    // or submit_response). A turn that ends without one — even if it called
    // get_command/bash — replied in prose and broke the act-through-tools contract.
    public bool LastSendCalledClosingTool => Volatile.Read(ref _sendCalledClosingTool) != 0;

    // True once a plan was rendered this session. Informational only.
    public bool PlanShown => _state?.PlanShown ?? false;

    // True once the final outcome has rendered for this session (a one-shot plan or
    // a submit_response).
    public bool SubmitRendered => _state?.SubmitRendered ?? false;

    // The plan the agent proposed but nothing rendered (an interactive turn that
    // stopped after propose_plan without submitting). The one-shot host reads these
    // on exit to render it as a fallback, so the plan is never lost.
    public bool HasPendingPlan => _state?.LastIssues is { Count: > 0 } || _state?.LastPlanSteps is { Count: > 0 };
    public IReadOnlyList<string>? PendingIssues => _state?.LastIssues;
    public IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? PendingPlanSteps => _state?.LastPlanSteps;
    public IReadOnlyList<ConsoleApprovalPrompt.UserInputNeed>? PendingNeeds => _state?.LastNeedsUserInput;
    public IReadOnlyList<ConsoleApprovalPrompt.ChosenDefault>? PendingDefaults => _state?.LastDefaults;

    // True once the final outcome rendered AND the mode wants the session to end on
    // it. one-shot ends on its first valid plan or on submit_response (single
    // response, then exit); inline does NOT — it keeps the conversation open so the
    // user can reply to the plan/questions, and ends only when the user types exit/quit.
    public bool SessionClosed =>
        (_state?.IsOneShot ?? true) && (_state?.SubmitRendered ?? false);

    // Hook for the host renderer to learn when the final outcome has rendered, so
    // it can suppress any trailing model prose. Forwards to SessionState — null is
    // fine (no-op when no suppressor is wired).
    public void SetOnFinalRendered(Action? callback)
    {
        if (_state is not null) _state.OnFinalRendered = callback;
    }

    // Receives the plan steps exactly as shown with the final outcome (null when the
    // outcome has no plan), for example to record them in the diagnostic trace.
    public void SetOnOutcomeShown(Action<IReadOnlyList<ConsoleApprovalPrompt.PlanStep>?>? callback)
    {
        if (_state is not null) _state.OnOutcomeShown = callback;
    }

    internal ChatAgent(
        CopilotClient client, CopilotSession session, AgentModelTelemetry modelTelemetry,
        SessionState? state, AgentInvocationContext invocationContext, AgentSdkLifetime? sdkLifetime = null)
    {
        _session = session;
        _modelTelemetry = modelTelemetry;
        _state = state;
        _invocationContext = invocationContext;
        _sdkLifetime = sdkLifetime ?? new AgentSdkLifetime(invocationContext);
        _sdkLifetime.SetClient(client);
        _sdkLifetime.SetSession(session);
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
        IConfigurationService configurationService,
        DataBoundaryResolver boundaryResolver,
        string azExePath,
        ConsoleApprovalPrompt approvalSink,
        IAgentRenderer renderer,
        AgentOptions options,
        string entrypoint,
        AgentRuntimeLifetime lifetime,
        SpinnerService? spinner = null,
        ILogger? logger = null,
        string? failedCommand = null,
        CancellationToken ct = default,
        // Appended after ct rather than placed with the other optional parameters: inserting it
        // ahead of ct would rebind the final positional argument of any existing call site from
        // CancellationToken to AuthResult.
        AuthResult? preAcquiredAuth = null,
        Func<CopilotClient>? clientFactory = null,
        RecoverySessionSettings? recoverySettings = null,
        // The data boundary the host resolved with preAcquiredAuth; null when it is not known.
        string? dataBoundary = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var invocationContext = new AgentInvocationContext(
            Activity.Current, lifetime.Telemetry, lifetime, ct);
        using var producer = invocationContext.EnterProducer();
        using var create = invocationContext.StartOperation("Agent.Create", ct);
        RecoveryTelemetry.RecordConfiguration(create.Activity, options, recoverySettings?.SessionStyle);
        var modelTelemetry = new AgentModelTelemetry(options, new RendererLoggingService(renderer).Warn);
        var sdkLogger = logger ?? NullLogger.Instance;
        // BYOM (builtin provider) authenticates the LLM with an Azure token, not a
        // GitHub credential — so the daemon's default auto-login, which reads the
        // GitHub keychain and prompts on macOS, touches a credential this path never
        // uses. Disable it for builtin only; the github provider keeps auto-login so
        // subscription users' stored/gh-CLI credentials still work.
        var useBuiltin = options.IsBuiltinProvider;
        var sdkLifetime = new AgentSdkLifetime(invocationContext);
        lifetime.RegisterCleanup(sdkLifetime.DisposeAsync);
        try
        {
            var client = clientFactory?.Invoke() ?? new CopilotClient(new CopilotClientOptions
            {
                Logger = sdkLogger,
                Connection = RuntimeConnection.ForStdio(path: BundledDaemonResolver.Resolve()),
                UseLoggedInUser = useBuiltin ? false : null,
                // Builtin only: its endpoint can reject replayed encrypted reasoning with
                // 400 invalid_encrypted_content when a request reaches another region.
                // The handler removes the reasoning items before each request. Setting a
                // handler moves model HTTP traffic from the runtime to .NET HttpClient.
#pragma warning disable GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                RequestHandler = modelTelemetry.ServedModel is { } servedModel ? new EncryptedReasoningRequestHandler(servedModel) : null,
#pragma warning restore GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            });
            sdkLifetime.SetClient(client);
            return await CreateCoreAsync(
                client, invocationContext, lifetime, sdkLifetime, flavor, auth, profileService, configurationService, boundaryResolver, azExePath, approvalSink,
                renderer, options, entrypoint, spinner, failedCommand, modelTelemetry, preAcquiredAuth, dataBoundary, ct, recoverySettings).ConfigureAwait(false);
        }
        finally
        {
            modelTelemetry.RecordTo(create.Activity);
        }
    }

    private static async Task<ChatAgent> CreateCoreAsync(
        CopilotClient client,
        AgentInvocationContext invocationContext,
        AgentRuntimeLifetime lifetime,
        AgentSdkLifetime sdkLifetime,
        string flavor,
        IAuthService auth,
        IProfileService profileService,
        IConfigurationService configurationService,
        DataBoundaryResolver boundaryResolver,
        string azExePath,
        ConsoleApprovalPrompt approvalSink,
        IAgentRenderer renderer,
        AgentOptions options,
        string entrypoint,
        SpinnerService? spinner,
        string? failedCommand,
        AgentModelTelemetry modelTelemetry,
        AuthResult? preAcquiredAuth,
        string? dataBoundary,
        CancellationToken ct,
        RecoverySessionSettings? recoverySettings)
    {
        var systemPrompt = LoadSystemPrompt(flavor, entrypoint);
        var armDetails = new ArmDetailsClient(auth);
        var cacheTools = new CachedResourceTools(armDetails, profileService, invocationContext.StartToolActivity);
        var refresher = ResourceCache.GetOrCreateRefresher(() => new ArmCacheRefresher(auth, profileService));
        // The resource-reading tools push the active profile's tenant into
        // ResourceCache on every invocation (see CachedResourceTools.SyncTenantAsync),
        // so a mid-session `az account set` is reflected on the next query.
        // Nothing to pin at startup; wire only the background refresher here.
        refresher.AttachHostLifetime(lifetime);
        lifetime.RegisterCleanup(refresher.StopAndDrainAsync);
        var helpTools = new HelpTools(azExePath, configurationService, invocationContext.StartToolActivity);
        var skuTools = new AvailableLocationsTool(auth, profileService, activityCreator: invocationContext.StartToolActivity);
        var oneShot = !(recoverySettings?.IsInteractive ?? options.Recovery.IsInteractive);
        // One-shot ends on the first valid plan (or on submit_response when there is
        // no plan), so both tools end the agent turn there; see PreToolUseHook.
        var submitTool = new SubmitResponseTool(endsTurn: oneShot, invocationContext.StartToolActivity);
        var planTool = new ProposePlanTool(endsTurn: oneShot, invocationContext.StartToolActivity);

        ChatAgent? self = null;
        SessionErrored? earlyError = null;

        var eventRenderer = new EventRenderer
        {

            OnEvent = ev => HandleAgentEvent(ev, self, err => earlyError = err, spinner, renderer),
            OnText  = renderer.OnText,
            ServedModel = () => modelTelemetry.ServedModel?.Response,
        };

        var state = new SessionState
        {
            IsOneShot = oneShot,
            IsCliFlavor = AgentFlavor.IsAzureCli(flavor),
            ReadCommandCatalog = helpTools.ReadCatalogAsync,
            FailedCommandFiles = FailedCommandFiles.ForCaller(failedCommand),
        };
        // Pre-fetch the failed command's help so the agent doesn't burn a turn
        // looking up the command it was triggered by. Same lookup the LLM's
        // get_command uses — on a hit it's the parameter table. On a miss (any
        // error payload, including not_found) FailedCommandHelp skips injection
        // entirely rather than seeding the session with an error blob.
        // AgentProcessHelpTests relies on this prefetch running before the session configuration is validated.
        var failedHelp = await FailedCommandHelp.BuildContextAsync(helpTools, failedCommand, ct).ConfigureAwait(false);
        // A host that already acquired the Azure token (AgentModeEntry does) passes the boundary it
        // resolved with it; otherwise acquire one token here for both the lookup and the provider.
        BuiltinEndpoint? builtinEndpoint = null;
        if (options.IsBuiltinProvider)
        {
            (builtinEndpoint, preAcquiredAuth) = await AgentStartupBoundary.ResolveBuiltinEndpointAsync(
                options, auth, profileService, preAcquiredAuth, dataBoundary,
                (tenant, token, cancellationToken) => AgentStartupBoundary.LookupDataBoundary(
                    boundaryResolver, tenant, token, cancellationToken),
                ct, new RendererLoggingService(renderer).Warn).ConfigureAwait(false);
        }

        var sessionConfig = await BuildSessionConfigAsync(
            options, auth, systemPrompt, cacheTools, helpTools, skuTools, submitTool, planTool,
            azExePath, profileService, approvalSink, eventRenderer, state, failedHelp, entrypoint, invocationContext, ct, preAcquiredAuth,
            builtinEndpoint?.Url).ConfigureAwait(false);

        sessionConfig.OnEvent = ev =>
        {
            modelTelemetry.Observe(ev);
            eventRenderer.Forward(ev);
        };

        var agent = await ConnectAsync(
            client, sessionConfig, modelTelemetry, state, invocationContext, ct, sdkLifetime).ConfigureAwait(false);
        agent.Endpoint = builtinEndpoint;
        self = agent;
        if (earlyError is not null) Volatile.Write(ref agent._startupError, earlyError);
        await modelTelemetry.InitializeAsync(agent._session, ct).ConfigureAwait(false);
        return agent;
    }

    internal static async Task<ChatAgent> ConnectAsync(
        CopilotClient client, SessionConfig sessionConfig, string model, SessionState? state,
        AgentInvocationContext invocationContext, CancellationToken ct = default)
    {
        var metadata = new AgentModelTelemetry(
            new AgentOptions { Provider = "builtin", Builtin = new() { ModelId = model } },
            message => Trace.TraceWarning(message));
        return await ConnectAsync(client, sessionConfig, metadata, state, invocationContext, ct).ConfigureAwait(false);
    }

    internal static async Task<ChatAgent> ConnectAsync(
        CopilotClient client, SessionConfig sessionConfig, AgentModelTelemetry modelTelemetry, SessionState? state,
        AgentInvocationContext invocationContext, CancellationToken ct = default, AgentSdkLifetime? sdkLifetime = null)
    {
        invocationContext.Bind(sessionConfig);
        var session = await client.CreateSessionAsync(sessionConfig, ct).ConfigureAwait(false);
        return new ChatAgent(client, session, modelTelemetry, state, invocationContext, sdkLifetime);
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

    internal static async Task<SessionConfig> BuildSessionConfigAsync(
        AgentOptions options,
        IAuthService auth,
        string systemPrompt,
        CachedResourceTools cacheTools,
        HelpTools helpTools,
        AvailableLocationsTool skuTools,
        SubmitResponseTool submitTool,
        ProposePlanTool planTool,
        string azExePath,
        IProfileService profileService,
        ConsoleApprovalPrompt approvalSink,
        EventRenderer eventRenderer,
        SessionState state,
        string? failedHelp,
        string entrypoint,
        AgentInvocationContext invocationContext,
        CancellationToken ct,
        AuthResult? preAcquiredAuth = null,
        // The resolved gateway URL; null uses the West Europe gateway (unknown boundary).
        string? builtinBaseUrl = null)
    {
        var reasoningEffort = options.ReasoningEffort?.ToLowerInvariant();
        if (reasoningEffort is not ("low" or "medium" or "high"))
        {
            throw new ArgumentException(
                $"Value '{options.ReasoningEffort}' is not allowed for 'agent.reasoningEffort'. Expected: low | medium | high.",
                nameof(options));
        }

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

        var sessionConfig = new SessionConfig
        {
            SessionId = Guid.NewGuid().ToString(),
            ConfigDirectory = HomePaths.GetDefaultAgentCopilotDirectory(),
            Streaming = true,
            ReasoningEffort = reasoningEffort,
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
                OnPreToolUse = (input, _) => PreToolUseHook.HandleAsync(
                    input, azExePath, approvalSink, state, invocationContext.CallbackCancellationToken),
                OnSessionStart = async (_, _) =>
                {
                    var ct = invocationContext.CallbackCancellationToken;
                    var envJson = await EnvSnapshotProvider.GetJsonAsync(profileService, azExePath, ct).ConfigureAwait(false);
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
                    var ct = invocationContext.CallbackCancellationToken;
                    var envJson = await EnvSnapshotProvider.GetJsonAsync(profileService, azExePath, ct).ConfigureAwait(false);
                    return new UserPromptSubmittedHookOutput { AdditionalContext = EnvSnapshotFormatter.Format(envJson) };
                },
            },
            OnPermissionRequest = async (req, invocation) =>
            {
                var toolName = (req as PermissionRequestCustomTool)?.ToolName;
                // Cache tools are read-only — silently approve. Bash + every
                // other built-in mutator goes through the user prompt.
                if (toolName is not null && PreToolUseHook.IsReadOnlyCacheTool(toolName))
#pragma warning disable GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    return PermissionDecision.ApproveOnce();
#pragma warning restore GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                // A file that the failed command names is the user's own input, and the
                // cause of the failure is often inside it, so reading it needs no prompt.
                if (state.FailedCommandFiles?.Allows(req) == true)
#pragma warning disable GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    return PermissionDecision.ApproveOnce();
#pragma warning restore GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                var ok = await approvalSink.RequestAsync(req, invocationContext.CallbackCancellationToken).ConfigureAwait(false);
#pragma warning disable GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                return ok ? PermissionDecision.ApproveOnce() : PermissionDecision.Reject(feedback: null);
#pragma warning restore GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            },
            OnEvent = eventRenderer.Forward,
        };

        switch (options.Provider?.ToLowerInvariant())
        {
            case "builtin":
                sessionConfig.Model = options.Builtin.ModelId;
                // A host that already acquired the Azure token (AgentModeEntry does, so the same one
                // covers the ARM data boundary lookup) passes it in; otherwise acquire it here.
                var baseUrl = builtinBaseUrl
                    ?? BuiltinEndpointResolver.Choose(boundary: null, options.Builtin, message => Trace.TraceWarning(message)).Url;
                sessionConfig.Provider = preAcquiredAuth is not null
                    ? BuiltinProviderConfig.Build(options.Builtin, baseUrl, preAcquiredAuth.AccessToken)
                    : await BuiltinProviderConfig.BuildAsync(options.Builtin, baseUrl, auth, ct).ConfigureAwait(false);
                break;

            case "github":
                break;

            default:
                throw new ArgumentException(
                    $"Unknown agent.provider value: {BuiltinProviderConfig.FormatProviderForError(options.Provider)}. Expected: builtin | github.",
                    nameof(options));
        }

        return sessionConfig;
    }
    /// <summary>Stores startup errors, or records a turn error and cancels the active operation.</summary>
    /// <remarks>
    /// SDK callbacks may race with send completion; use the operation slot's cancellation source.
    /// </remarks>
    internal void OnSessionErrored(SessionErrored error)
    {
        if (Volatile.Read(ref _hasSent) == 0)
        {
            Volatile.Write(ref _startupError, error);
            return;
        }
        // Drops errors that arrive after the send finished.
        if (_invocationContext.CurrentTurnCts is null) return;
        Volatile.Write(ref _lastError, error);
        _invocationContext.CancelCurrentOperation();
    }

    /// <summary>Updates Agent state and spinner output, then renders an SDK event.</summary>
    /// <remarks>
    /// Before construction completes, send session errors to <paramref name="onEarlyError"/>.
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
        using var producer = _invocationContext.EnterProducer();
        // Reserve the operation before changing shared turn state.
        // Dispose the operation before turnCts, so the slot is cleared before its source is disposed.
        // The outer using also releases turnCts if StartOperation throws.
        var turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _invocationContext.StoppingToken);
        using (turnCts)
        using (var send = _invocationContext.StartOperation("Agent.Send", turnCts))
        {
            Volatile.Write(ref _hasSent, 1);
            Volatile.Write(ref _lastError, null);
            Volatile.Write(ref _sendCalledClosingTool, 0);
            if (_state is not null) _state.SubmittedThisTurn = false;
            _modelTelemetry.RecordTo(send.Activity);
            _modelTelemetry.ServedModel?.BeginTurn();
            AgentModelObservation? observedModel = null;
            var inMainTurn = false;
            try
            {
                using var modelSubscription = send.Activity is not null && !_modelTelemetry.IsBuiltin
                    ? _session.On<SessionEvent>(ev => _invocationContext.HandleEvent(() =>
                    {
                        if (!string.IsNullOrEmpty(ev.AgentId))
                            return;
                        switch (ev)
                        {
                            case AssistantTurnStartEvent { Data.TurnId.Length: > 0 }:
                                inMainTurn = true;
                                break;
                            case AssistantTurnEndEvent:
                            case SessionIdleEvent:
                            case SessionErrorEvent:
                                inMainTurn = false;
                                break;
                            case AssistantUsageEvent { Data: { } usage } when inMainTurn:
                                if (usage.Initiator is null or "" or "user" &&
                                    RecoveryTelemetry.IsExportableModel(usage.Model))
                                {
                                    Volatile.Write(ref observedModel,
                                        new AgentModelObservation(usage.Model, AgentModelSource.Usage));
                                }
                                break;
                        }
                    }))
                    : null;
                await _session.SendAndWaitAsync(
                    new MessageOptions { Prompt = prompt },
                    timeout: TurnTimeout,
                    cancellationToken: turnCts.Token).ConfigureAwait(false);
                // Idle can complete the wait before the SDK observes a cancel that already happened.
                turnCts.Token.ThrowIfCancellationRequested();
            }
            finally
            {
                if (_modelTelemetry.ServedModel?.Turn is { } served)
                    RecoveryTelemetry.RecordModel(send.Activity, served, AgentModelSource.Response);
                else if (Volatile.Read(ref observedModel) is { } model)
                    RecoveryTelemetry.RecordModel(send.Activity, model.Model, model.Source);
                else
                    _modelTelemetry.RecordTo(send.Activity);
            }
        }
    }

    public async Task AbortAsync(CancellationToken ct = default)
    {
        _invocationContext.CancelCurrentOperation();
        try { await _session.AbortAsync(ct).ConfigureAwait(false); } catch { }
    }
    private static readonly string[] ValidEntrypoints = ["oneshot", "inline"];

    internal static string LoadSystemPrompt(string flavor, string entrypoint)
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
        content = content.Replace("{{flavor}}", flavor, StringComparison.OrdinalIgnoreCase);
        foreach (var (key, value) in AgentFlavor.PromptCommands(flavor))
            content = content.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        return content;
    }

    public ValueTask DisposeAsync() => new(_sdkLifetime.DisposeAsync());
}
