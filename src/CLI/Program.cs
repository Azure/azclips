// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Abstractions.Runtime.Handler;
using Core.Cli.Completions;
using Core.Cli.Exceptions;
using Core.Cli.Help;
using Core.Cli.Output;
using Core.Cli.Parser;
using Core.Cli.Recovery;
using Core.Cli.Telemetry;
using Core.Cli.Version;
using Core.Configuration.Options;
using Core.Extensions;
using Core.Runtime;
using Core.Runtime.Recovery;
using Core.Telemetry;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli;

public class Program
{
    private const string FirstPartyApplicationId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";
    private static readonly ClientType _clientType = ClientType.CLI;
    private static ILoggingService? _logger;
    private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private static ITelemetryService? _telemetryService = null;
    private static IConfigurationService? _configurationService = null;

    private static OutputStreams streams = BuildStreams(LogLevel.Info, OutputFormat.Json, OutputFormat.Tsv);

    public static async Task<int> Main(string[] args)
    {
        var performance = new PerformanceTimeline("AzureCLI.Startup");
        bool isBareInvocation = args.Length == 0;

        if (args.Length > 0 && args[0] == "--upload-telemetry")
        {
            new TelemetryUpload(new Core.Runtime.Persistence.FilePersistenceStore()).RunPending();
            return 0;
        }

        // Hidden UI-print probe. Renders a sample block for triaging terminal
        // wrap / encoding / alignment. Runs before DI so it stays independent.
        int? printProbeExit = PrintProbe.TryHandle(args, Console.Out);
        if (printProbeExit.HasValue)
        {
            return printProbeExit.Value;
        }

        using (performance.Measure("AzureCLI.Startup.Container"))
        {
            AzclipsServiceProvider.PrependRegistration(services =>
            {
                services.AddSingleton<IExtensionLoader, BundledExtensionLoader>();
                services.AddSingleton<IExtensionLoader, InstalledExtensionLoader>();
                services.AddSingleton<PropertyParser>();
                services.AddCliExtensionDispatch();
            });
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IAssemblyLoadingProvider, CLIAssemblyLoadingProvider>();
                services.AddSingleton<IArgumentParsingProvider, CLIArgumentParsingProvider>();
                services.AddSingleton<ITelemetrySender, FileTelemetrySender>();
                services.AddSingleton<ITelemetryProvider, CLITelemetryProvider>();
                services.AddSingleton<ITelemetryLayerScopeFactory, TelemetryLayerScopeFactory>();
                services.AddSingleton<ICommandTelemetryContext, CommandTelemetryContext>();
                services.AddSingleton<IUserAgentHandler, CLIUserAgentHandler>();
            });
            AzclipsServiceProvider.Initialize(new ClientMetadata(_clientType, FirstPartyApplicationId));
        }

        int? historyWorkerExit = await FailureHistoryWorker.TryHandleAsync(
            args, () => AzclipsServiceProvider.GetRequiredService<IFailureHistoryStore>());
        if (historyWorkerExit.HasValue)
        {
            AzclipsServiceProvider.Dispose();
            return historyWorkerExit.Value;
        }

        // Post-DI utility modes: completion generation needs the container to resolve
        // IEngine, but still returns before the root activity starts — no telemetry.
        int? completionExit = await CompletionGenerator.TryHandleAsync(args, _clientType);
        if (completionExit.HasValue)
            return completionExit.Value;

        int? candidatesExit = await CompletionGenerator.TryHandleCandidatesAsync(args, _clientType);
        if (candidatesExit.HasValue)
            return candidatesExit.Value;

        using (performance.Measure("AzureCLI.Startup.Services"))
        {
            _logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
            _configurationService = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
            _telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        }

        Activity? activity = null;
        var uploadReservation = AzclipsServiceProvider.GetRequiredService<TelemetryUploadReservation>();
        var uploaderStartInfo = BuildTelemetryUploaderStartInfo(
            Environment.ProcessPath, AzclipsServiceProvider.GetRequiredService<IPersistenceStore>().RootDirectory);
        var invocationTelemetryEnabled = false;
        string? uploadReservationId = null;
        IDisposable? startupLoggingScope = null;

        var errorHelpAllowed = ErrorHelpPolicy.IsHumanReadableErrorStream(Console.IsErrorRedirected);
        var positional = args;
        CommandInvocation? invocation = null;

        GlobalArgs globalArgs;
        EffectiveSessionSettings sessionSettings;

        try
        {
            using (performance.Measure("AzureCLI.ParseGlobalArgs"))
            {
                globalArgs = GlobalArgParser.Parse(args);
            }

            // Disposed explicitly in the finally below — it must stop before the container
            // is disposed, since that takes the TracerProvider (and the exporter) with it.
            using (performance.Measure("AzureCLI.Startup.Telemetry"))
            {
                invocationTelemetryEnabled = _configurationService.GetConfiguration<TelemetryOptions>().Enabled;
                var coreOptions = _configurationService.GetConfiguration<CoreOptions>();
                var clientsOptions = _configurationService.GetConfiguration<ClientsOptions>();
                sessionSettings = SessionPolicyResolver.Resolve(coreOptions, clientsOptions, globalArgs);
                ColorPolicy.Initialize(sessionSettings.Color);
                streams = BuildStreams(sessionSettings.LogLevel, sessionSettings.Format, sessionSettings.SimpleFormat, globalArgs.Query);

                // Startup/profile reads happen before the session logger is set in ExecutionSession.
                startupLoggingScope = ScopedLoggingService.UseScope(new OutputStreamAdapter(streams, _cancellationTokenSource.Token));

                activity = _telemetryService.StartRootActivity(performance.StartedAt);
                uploadReservationId = BeginCommandUploadReservation(activity, invocationTelemetryEnabled, uploadReservation);
                AzclipsServiceProvider.GetRequiredService<ICommandTelemetryContext>().BindCommand(activity);

                // Tag routing before replayed startup spans export on disposal.
                // ExecutionSession resolves again from the bound profile for command spans.
                var (routedTenantId, uploadRegion, routedSubscriptionId, routedAuthType) = await TelemetryRoutingResolver.ResolveRoutingAsync(
                    AzclipsServiceProvider.GetRequiredService<DataBoundaryResolver>(),
                    AzclipsServiceProvider.GetRequiredService<IProfileService>(),
                    AzclipsServiceProvider.GetRequiredService<IAuthService>(),
                    TryGetProfileOverride(args),
                    _cancellationTokenSource.Token,
                    loggingService: AzclipsServiceProvider.GetService<ILoggingService>());
                if (routedTenantId is not null) activity?.SetTag(TelemetryTags.TenantId, routedTenantId);
                activity?.SetTag(TelemetryTags.DataBoundary, uploadRegion.ToDataBoundary());
                if (routedSubscriptionId is not null) activity?.SetTag(TelemetryTags.SubscriptionId, routedSubscriptionId);
                if (routedAuthType is not null) activity?.SetTag(TelemetryTags.AuthType, routedAuthType);

                performance.Replay(_telemetryService);
            }

            using (var _ = _telemetryService.StartActivity("AzureCLI.HandleGlobalArgs"))
            {
                if (globalArgs.NoCache)
                {
                    // AssemblyLoadingOptions.BypassExtensionCache reads this via IConfigurationService.
                    Environment.SetEnvironmentVariable("AZCLIPS_NO_EXTENSION_CACHE", "true");
                }

                positional = globalArgs.Positional as string[] ?? globalArgs.Positional.ToArray();

                if (globalArgs.Query is not null)
                {
                    JmesPathEvaluator.Validate(globalArgs.Query);
                }

                if (globalArgs.Version is not null)
                {
                    return DispatchVersion(globalArgs.Version, globalArgs.Query, activity);
                }

                if (globalArgs.Help is not null)
                {
                    return DispatchHelp(globalArgs.Help, isBareInvocation, streams, activity);
                }
            }

            using (var _ = _telemetryService.StartActivity("AzureCLI.ParseCommandInvocation"))
            {
                var dispatcher = AzclipsServiceProvider.GetRequiredService<ExtensionDispatcher>();
                invocation = dispatcher.Parse(positional);
                _telemetryService.TagInvocation(invocation);
            }

            using (var _ = _telemetryService.StartActivity("AzureCLI.ProcessCommand"))
            {
                var engine = AzclipsServiceProvider.GetRequiredService<IEngine>() ??
                             throw new InvalidOperationException("Engine service not found.");
                using ISession session =
                    engine.OpenExecutionSession(invocation, streams, _cancellationTokenSource.Token);
                await session.RunAsync();

                activity?.SetStatus(ActivityStatusCode.Ok);
                return 0;
            }
        }
        catch (HttpRequestException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            // The runtime already wrote this failure to stderr; only save it here.
            var failure = await CaptureFailureAsync(args, positional, ex, 1, invocation, activity);
            EmitRecoveryDiagnostic(failure);
            if (failure.Canceled)
                return 130;

            return (await StopRootThenFallbackAsync(
                activity, invocationTelemetryEnabled,
                context => TryAgentFallbackAsync(args, ex.Message, context, failure.Entry?.Id, 1),
                uploadReservationId)) ?? 1;
        }
        // Filtered rather than checked in the body so other shapes still reach the known-error catch below.
        catch (CommandNotFoundException ex) when (ErrorHelpPolicy.ShouldShowGroupHelp(ex, positional))
        {
            try
            {
                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested (empty command group)");
                return DispatchHelp(new HelpRequest([ex.Verb]), isBareInvocation: false, streams, activity);
            }
            catch (Exception helpEx)
            {
                // Throwing from a catch body skips the catches below, so degrade to the original error here.
                _logger?.Debug($"group help unavailable: {helpEx.Message}");
                EmitKnownError(ex, invocation);
                var failure = await CaptureFailureAsync(args, positional, ex, ex.ExitCode, invocation, activity);
                EmitRecoveryDiagnostic(failure);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return failure.Canceled ? 130 : ex.ExitCode;
            }
        }
        // Typed errors flagged as agent-eligible go to the agent first. If the agent can't run, the typed error is printed.
        catch (AzclipsErrorException ex) when (ex.AgentFallbackEligible)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            // The original error is always the first line, before any recovery output.
            EmitKnownError(ex, invocation);
            var failure = await CaptureFailureAsync(args, positional, ex, ex.ExitCode, invocation, activity);
            EmitRecoveryDiagnostic(failure);
            if (failure.Canceled)
                return 130;

            var agentExitCode = await StopRootThenFallbackAsync(
                activity, invocationTelemetryEnabled,
                context => TryAgentFallbackAsync(args, ex.Message, context, failure.Entry?.Id, ex.ExitCode),
                uploadReservationId);
            return agentExitCode ?? ex.ExitCode;
        }
        catch (AzclipsErrorException ex)
        {
            if (ErrorHelpPolicy.ShouldShowCommandHelp(ex, invocation, errorHelpAllowed))
            {
                EmitKnownError(ex, invocation);
                TryRenderHelpAfterError(ErrorHelpPolicy.CommandHelpRequest(invocation), streams, activity);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                var helpFailure = await CaptureFailureAsync(args, positional, ex, ex.ExitCode, invocation, activity);
                EmitRecoveryDiagnostic(helpFailure);
                return helpFailure.Canceled ? 130 : ex.ExitCode;
            }

            // No AI fallback — the failure is well-known and the user's intent is unambiguous.
            EmitKnownError(ex, invocation);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            var failure = await CaptureFailureAsync(args, positional, ex, ex.ExitCode, invocation, activity);
            EmitRecoveryDiagnostic(failure);
            return failure.Canceled ? 130 : ex.ExitCode;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex.Message);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            var failure = await CaptureFailureAsync(args, positional, ex, 1, invocation, activity);
            EmitRecoveryDiagnostic(failure);
            if (failure.Canceled)
                return 130;

            return (await StopRootThenFallbackAsync(
                activity, invocationTelemetryEnabled,
                context => TryAgentFallbackAsync(args, ex.Message, context, failure.Entry?.Id, 1),
                uploadReservationId)) ?? 1;
        }
        finally
        {
            startupLoggingScope?.Dispose();

            // Stop the root span while the exporter is still alive — AzclipsServiceProvider.Dispose()
            // below disposes the TracerProvider, after which nothing can be exported.
            activity?.Dispose();

            // Route through streams.WriteDebug (level-gated) so the aggregate table stays
            // silent under warn/error and only surfaces under --debug. Routing through
            // _logger would fall back to ConsoleLoggingService here (session's UseScope
            // was already disposed) and leak the table regardless of level.
            ExtensionLoadTracker.EmitSummary(msg => streams.WriteDebug(msg, default).GetAwaiter().GetResult());
            AzclipsServiceProvider.Dispose();
            if (uploadReservationId is not null)
            {
                try { uploadReservation.MarkReady(uploadReservationId); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger?.Error($"telemetry upload reservation could not be released: {ex.Message}");
                }
            }
            SpawnTelemetryUploader(uploaderStartInfo);
        }
    }

    private static OutputStreams BuildStreams(LogLevel level, OutputFormat format, OutputFormat simpleFormat, string? query = null)
    {
        var colorPolicyAllowsColor = ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected);
        return new OutputStreams(
            WriteOutput: (value, ct) =>
            {
                var terminalWidth = format == OutputFormat.Table || simpleFormat == OutputFormat.Table
                    ? TerminalWidthProvider.Get()
                    : null;
                var output = CliOutputFormatter.FormatValue(value, format, simpleFormat, colorPolicyAllowsColor, query, out var selectedFormat, terminalWidth);
                _telemetryService?.AddTag(
                    TelemetryTags.CommandOutputType,
                    selectedFormat.ToString().ToLowerInvariant());

                Console.WriteLine(output);
                return Task.CompletedTask;
            },
            WriteError: (value, ct) =>
            {
                ConsoleSink.Error(value?.ToString() ?? string.Empty);
                return Task.CompletedTask;
            },
            WriteVerbose: (value, ct) =>
            {
                if (LoggingPolicy.IsEnabled(level, LogSeverity.Verbose))
                {
                    ConsoleSink.Verbose(value?.ToString() ?? string.Empty);
                }
                return Task.CompletedTask;
            },
            WriteDebug: (value, ct) =>
            {
                if (LoggingPolicy.IsEnabled(level, LogSeverity.Debug))
                {
                    ConsoleSink.Debug(value?.ToString() ?? string.Empty);
                }
                return Task.CompletedTask;
            },
            WriteWarning: (value, ct) =>
            {
                if (LoggingPolicy.IsEnabled(level, LogSeverity.Warn))
                {
                    ConsoleSink.Warn(value?.ToString() ?? string.Empty);
                }
                return Task.CompletedTask;
            },
            WriteInformation: (value, ct) =>
            {
                if (LoggingPolicy.IsEnabled(level, LogSeverity.Info))
                {
                    ConsoleSink.Info(value?.ToString() ?? string.Empty);
                }
                return Task.CompletedTask;
            }
        );
    }

    internal static string? TryGetProfileOverride(string[] args)
    {
        // Normalized the same way ExtensionParser does (TrimStart('-')): the real parser
        // treats any leading-dash form as an option marker, so `-profile`/`--profile`/
        // `---profile` are all equivalent. Matching only "--profile" here would miss a
        // valid `-profile eu-profile` invocation, causing this early best-effort pass to
        // resolve the active profile instead of the explicit override — routing startup
        // telemetry (and anything replayed before ExecutionSession's later, authoritative
        // resolution) to the wrong tenant/boundary.
        string? profileName = null;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("-", StringComparison.Ordinal))
                continue;

            var option = arg.TrimStart('-');
            if (option.StartsWith("profile=", StringComparison.OrdinalIgnoreCase))
                profileName = option["profile=".Length..];

            if (string.Equals(option, "profile", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                profileName = args[++i];
        }

        return profileName?.Trim();
    }

    // Cold-path host command: rendered from local data, no engine session or extension load.
    private static int DispatchVersion(VersionRequest version, string? query, Activity? activity)
    {
        using (var _ = _telemetryService?.StartActivity("AzureCLI.DispatchVersion"))
        {
            var exit = new RuntimeVersionDispatcher(
                _configurationService!, Console.Out, AzclipsServiceProvider.GetRequiredService<IVersionProvider>())
                .Render(version.Format, query);
            activity?.SetStatus(ActivityStatusCode.Ok, "Version requested");
            return exit;
        }
    }

    /// <summary>
    /// Renders help beneath an error that was already reported. Routed to stderr so stdout stays
    /// reserved for command results, and best-effort so a help failure cannot replace the error.
    /// </summary>
    private static void TryRenderHelpAfterError(HelpRequest help, OutputStreams streams, Activity? activity)
    {
        try
        {
            DispatchHelp(help, isBareInvocation: false, streams, activity, Console.Error);
        }
        catch (Exception ex)
        {
            _logger?.Debug($"command help unavailable: {ex.Message}");
        }
    }

    private static int DispatchHelp(HelpRequest help, bool isBareInvocation, OutputStreams streams, Activity? activity, TextWriter? output = null)
    {
        using (var _ = _telemetryService?.StartActivity("AzureCLI.DispatchHelp"))
        {
            var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
            var renderer = new RuntimeHelpDispatcher(engine, streams, output)
            {
                SelectedPackageIds = () => CliExtensionServiceRegistration.GetRoutingSelectedPackageIds(
                    AzclipsServiceProvider.GetRequiredService<ExtensionDispatcher>(),
                    AzclipsServiceProvider.GetRequiredService<SelectedExtensionLocations>()),
            };
            if (help.Path.Count == 0)
            {
                // Banner only for truly bare `az`; user-typed help forms stay grep-friendly.
                if (isBareInvocation)
                {
                    WelcomeBanner.Write(Console.Out);
                }
                var rootExit = renderer.RenderRoot();
                activity?.SetStatus(ActivityStatusCode.Ok, "Help requested");
                return rootExit;
            }
            // Render extension help; known errors are handled by Main.
            var extExit = renderer.RenderExtensionHelp(help.Path);
            activity?.SetStatus(ActivityStatusCode.Ok, "Extension help requested");
            return extExit;
        }
    }

    private static void EmitKnownError(AzclipsErrorException ex, CommandInvocation? invocation)
    {
        var message = invocation is not null && ex is UsageErrorException
            ? $"{ex.Message} (az {invocation.CommandName} --help)"
            : ex.Message;

        ConsoleSink.Error($"[{ex.Code}] {message}");
    }

    private static AgentOptions? _agentOptions;

    // Capture and automatic fallback share one read of the Agent configuration per process.
    private static AgentOptions GetAgentOptionsOnce() =>
        _agentOptions ??= AzclipsServiceProvider.GetRequiredService<IConfigurationService>().GetConfiguration<AgentOptions>();

    private static async Task<FailureCaptureResult> CaptureFailureAsync(
        string[] args, string[] positional, Exception error, int exitCode,
        CommandInvocation? invocation, Activity? activity)
    {
        var inAgentMode = Environment.GetEnvironmentVariable(AgentLauncher.AgentModeEnvVar) == "1";
        var result = await FailureCapture.SaveAsync(
            args, positional, AgentLauncher.JoinShellQuoted(args), error, exitCode, invocation?.CommandName, activity,
            GetAgentOptionsOnce,
            () => AzclipsServiceProvider.GetRequiredService<IFailureHistoryStore>(),
            inAgentMode,
            _cancellationTokenSource.Token,
            persist: FailureHistoryWorker.SaveAsync);
        if (!result.Canceled && FailureCapture.ShouldCapture(args, positional, error, inAgentMode))
            await RecoveryOnboarding.TryRunAsync(
                AzclipsServiceProvider.GetRequiredService<IConfigurationService>(), Console.Error,
                RecoveryOnboarding.GetStatePath(), _cancellationTokenSource.Token);
        return result;
    }

    internal static void EmitRecoveryDiagnostic(FailureCaptureResult result)
    {
        if (result.Warning is not null)
            ConsoleSink.Warn(result.Warning);
        if (result.ShowHint && result.Entry is not null)
        {
            Console.Error.WriteLine($"To troubleshoot this error with AI, run: az troubleshoot recover --id {result.Entry.Id}");
            Console.Error.WriteLine(FailureCaptureResult.DefaultHint);
        }
    }

    /// <summary>
    /// Stops the command root span, then runs <paramref name="fallback"/> with the stopped
    /// root's invocation context.
    /// </summary>
    /// <remarks>
    /// The ordering is load-bearing: AI error recovery blocks on azclips-agent.exe for as
    /// long as the model takes, and anything still inside the root span counts as command
    /// execution time.
    /// </remarks>
    internal static async Task<T> StopRootThenFallbackAsync<T>(
        Activity? root,
        bool telemetryEnabled,
        Func<TelemetryInvocationContext, Task<T>> fallback,
        string? invocationId = null)
    {
        var context = TelemetryInvocationContext.Capture(root, telemetryEnabled);
        if (invocationId is not null)
            context = context with { InvocationId = invocationId };
        root?.Dispose();
        return await fallback(context);
    }

    internal static string? BeginCommandUploadReservation(
        Activity? root, bool telemetryEnabled, TelemetryUploadReservation reservation)
    {
        // Startup and command children can export before the command fails.
        var context = TelemetryInvocationContext.Capture(root, telemetryEnabled);
        try
        {
            return reservation.Begin(context) ? context.InvocationId : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.Error($"telemetry upload reservation could not be created: {ex.Message}");
            return null;
        }
    }

    private static Task<int?> TryAgentFallbackAsync(
        string[] args, string? errorText, TelemetryInvocationContext context, string? failureId, int originalExitCode) =>
        TryAgentFallbackAsync(
            GetAgentOptionsOnce, _telemetryService, args, errorText, context, failureId, originalExitCode);

    /// <summary>
    /// Runs the agent. Returns null when the agent was not launched, so the caller can show the original error.
    /// </summary>
    internal static async Task<int?> TryAgentFallbackAsync(
        Func<AgentOptions> getAgentOptions, ITelemetryService? telemetryService,
        string[] args, string? errorText, TelemetryInvocationContext context,
        string? failureId = null, int originalExitCode = 1,
        Func<RecoveryRequest, TelemetryInvocationContext, CancellationToken, Task<int>>? runAgent = null,
        Func<string?>? findAgent = null)
    {
        var commandName = string.IsNullOrEmpty(context.Command) ? GetCommandGroupFromArgs(args) : context.Command;
        context = context with { Command = string.IsNullOrEmpty(commandName) ? null : commandName };
        // Automatic recovery runs only with agent.recovery.trigger=auto. Fail-safe: if config
        // can't be read, keep the original error rather than spawning on an unknown state.
        AgentOptions agentOptions;
        try
        {
            agentOptions = getAgentOptions();
            if (!agentOptions.Recovery.IsAutomatic)
            {
                // A separate root preserves InstallationId without counting an execution.
                using var skippedScope = telemetryService?.StartIndependentRootActivity("CLI.Activity.AiRecoverySkipped");
                var skipped = skippedScope?.Activity;
                skipped?.SetTag(TelemetryTags.RecoverySkipReason, "disabled");
                RecoveryTelemetry.TagRecoveryContext(skipped, context);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger?.Debug($"agent fallback skipped: agent.recovery.trigger unavailable ({ex.Message})");
            return null;
        }

        // Interactive sessions need a terminal; a nested agent must never spawn another one.
        if (!AutomaticRecoveryPolicy.ShouldStart(
                agentOptions, Environment.GetEnvironmentVariable(AgentLauncher.AgentModeEnvVar) == "1",
                Console.IsInputRedirected, Console.IsOutputRedirected))
        {
            return null;
        }

        var agentExe = (findAgent ?? AgentLauncher.FindAgentExe)();
        if (agentExe is null)
        {
            _logger?.Error("agent mode unavailable: azclips-agent.exe not found alongside az");
            return null;
        }

        using var recoveryScope = context.TryGetCommandParentContext(out var commandParent)
            ? telemetryService?.StartActivityWithParent("CLI.Activity.AiRecovery", commandParent)
            : telemetryService?.StartIndependentRootActivity("CLI.Activity.AiRecovery");
        var recovery = recoveryScope?.Activity;
        context = context.WithRecoveryParent(recovery);
        RecoveryTelemetry.TagRecoveryContext(recovery, context);
        RecoveryTelemetry.RecordConfiguration(recovery, agentOptions);
        recovery?.SetTag(TelemetryTags.RecoveryTrigger, "automatic");

        try
        {
            var request = new RecoveryRequest
            {
                Trigger = RecoveryTrigger.Automatic,
                FailedCommand = AgentLauncher.JoinShellQuoted(args),
                ErrorText = errorText ?? string.Empty,
                SessionStyle = RecoverySessionSettings.Resolve(agentOptions, RecoveryTrigger.Automatic).SessionStyle,
                FailureId = failureId,
                ModelProfileName = TryGetProfileOverride(args),
            };
            var exit = await (runAgent ?? ((agentRequest, agentContext, token) =>
                AgentProcessRunner.RunAsync(agentExe, agentRequest, agentContext, agentContext.InvocationId, token)))(
                request, context, _cancellationTokenSource.Token);
            recovery?.SetTag(TelemetryTags.RecoveryExitCode, exit);
            recovery?.SetStatus(exit == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            return AutomaticRecoveryExitCode(agentOptions.Recovery.IsInteractive, originalExitCode, exit);
        }
        catch (Exception ex)
        {
            _logger?.Error($"agent mode subprocess failed: {ex.Message}");
            recovery?.SetStatus(ActivityStatusCode.Error);
            return null;
        }
    }

    /// <summary>
    /// Oneshot only proposes a fix, so the original exit code is kept and CI sees the same result as with
    /// recovery off. Interactive can run commands, so its own exit code reports the last interaction.
    /// </summary>
    internal static int AutomaticRecoveryExitCode(bool interactive, int originalExitCode, int agentExitCode) =>
        agentExitCode == 130 ? 130 : interactive ? agentExitCode : originalExitCode;

    // Fallback for when the failure happened during parsing, so no command was resolved.
    // Emits the command group only: positional values bind after the matched command path
    // (ExtensionDispatcher skips the verb plus matched tokens before collecting them), so
    // args[0] is always a command group and never a parameter value.
    internal static string GetCommandGroupFromArgs(string[] args) =>
        args.Length == 0 || args[0].StartsWith('-') ? string.Empty : args[0];


    /// <summary>
    /// Spawns a detached background process to upload buffered telemetry to Azure Monitor.
    /// Fire-and-forget: the CLI exits immediately without waiting for the upload.
    /// </summary>
    private static void SpawnTelemetryUploader(ProcessStartInfo? startInfo)
    {
        try
        {
            if (startInfo is null) return;

            // Windows process guard, must be run before process creation.
            DetachedStdio.SuppressStandardHandleInheritance();

            using var uploader = Process.Start(startInfo);
            // Drop our ends of the three pipes immediately.
            CloseRedirectedStreams(uploader);
        }
        catch
        {
            // Best effort. If spawn fails, traces remain on disk
            // and will be uploaded on the next CLI invocation.
        }
    }

    /// <summary>
    /// Builds the start info for the detached <c>--upload-telemetry</c> process, or
    /// <see langword="null"/> when this process must not spawn one.
    /// </summary>
    internal static ProcessStartInfo? BuildTelemetryUploaderStartInfo(string? exePath, string? persistenceRoot = null)
    {
        if (string.IsNullOrEmpty(exePath)) return null;

        // In-process benchmark mode: do not attempt to flush telemetry as in benchmark execution process
        var exeName = Path.GetFileName(exePath);
        if (!exeName.Equals("az", StringComparison.OrdinalIgnoreCase) &&
            !exeName.Equals("az.exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--upload-telemetry",
            UseShellExecute = false,
            CreateNoWindow = true,
            // The uploader outlives us, so it must not inherit our stdio: an
            // inherited pipe keeps the caller's reader waiting on the upload
            // rather than on `az`.
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (persistenceRoot is not null)
            startInfo.Environment[HomePaths.HomeOverrideVariable] = persistenceRoot;
        return startInfo;
    }

    /// <summary>Closes the parent-side ends of a redirected child's stdio pipes.</summary>
    private static void CloseRedirectedStreams(Process? process)
    {
        if (process is null) return;

        try { process.StandardInput.Close(); } catch { /* already closed */ }
        try { process.StandardOutput.Close(); } catch { /* already closed */ }
        try { process.StandardError.Close(); } catch { /* already closed */ }
    }
}
