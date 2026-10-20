// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Cli.Help;
using Core.Cli.Output;
using Core.Cli.Parser;
using Core.Configuration.Options;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Telemetry;

namespace Core.Cli.Recovery;

internal static class TroubleshootCommand
{
    internal static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var invokedAi = false;
        try
        {
            var globals = GlobalArgParser.Parse(args);
            var config = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
            var settings = SessionPolicyResolver.Resolve(
                config.GetConfiguration<CoreOptions>(), config.GetConfiguration<ClientsOptions>(), globals);
            ColorPolicy.Initialize(settings.Color);

            if (globals.Help is { } help)
            {
                var renderer = new RuntimeHelpRenderer(ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected));
                foreach (var line in renderer.RenderCommand(IntrinsicVerbs.CreateHelp(help.Path), (uint?)TerminalWidthProvider.Get()))
                    Console.Out.WriteLine(line);
                return 0;
            }

            var request = TroubleshootRequest.Parse(globals);
            if (globals.Query is not null)
                JmesPathEvaluator.Validate(globals.Query);

            AgentOptions? options = null;
            if (!request.IsLocalView)
            {
                options = config.GetConfiguration<AgentOptions>();
                RequireAnalysis(options, request.Style,
                    Environment.GetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE") == "1",
                    Console.IsInputRedirected, Console.IsOutputRedirected);
            }

            var store = AzclipsServiceProvider.GetRequiredService<IFailureHistoryStore>();
            var records = await store.ReadRecordsAsync(cancellationToken);
            if (request.List)
            {
                Console.Out.WriteLine(CliOutputFormatter.FormatValue(
                    TroubleshootOutput.List(records), settings.Format, settings.SimpleFormat,
                    ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected), globals.Query, TerminalWidthProvider.Get()));
                return 0;
            }

            var selected = TroubleshootSelection.Select(
                records, request.Id, TerminalSession.GetParentProcessId, ConsoleSink.Warn);
            if (request.Show)
            {
                Console.Out.WriteLine(TroubleshootOutput.FormatDetail(
                    selected, settings.Format, settings.SimpleFormat,
                    ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected), globals.Query, TerminalWidthProvider.Get()));
                return 0;
            }

            var agentExe = Program.FindAgentExe()
                ?? throw new AzclipsErrorException("AgentUnavailable", 1, "AI recovery is unavailable: azclips-agent was not found alongside az.");
            invokedAi = true;
            return await RunRecoveryAsync(selected, options!, agentExe, request.Style, cancellationToken);
        }
        catch (AzclipsErrorException ex)
        {
            Program.EmitKnownError(ex, null);
            return ex.ExitCode;
        }
        catch (FailureHistoryException ex)
        {
            Program.EmitKnownError(new(ex.Code, 1, ex.Message), null);
            return 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }
        catch (System.Text.Json.JsonException)
        {
            Program.EmitKnownError(new("InvalidConfiguration", 1, "The local configuration is not valid JSON."), null);
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or
            InvalidOperationException or ArgumentException or FormatException or OperationCanceledException)
        {
            Program.EmitKnownError(new("TroubleshootFailed", 1, ex.Message), null);
            return 1;
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
            if (invokedAi)
                Program.SpawnTelemetryUploader();
        }
    }

    internal static void RequireAnalysis(
        AgentOptions options, string? styleOverride, bool inAgentMode, bool inputRedirected, bool outputRedirected)
    {
        if (!options.Recovery.IsEnabled)
            throw new AzclipsErrorException("AgentDisabled", 1,
                "AI recovery is off. Run az config set --key agent.recovery --value manual before troubleshooting.");
        if (inAgentMode)
            throw new AzclipsErrorException("RecursiveRecovery", 1, "An agent-internal command cannot start another recovery.");
        if (RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, styleOverride).IsInteractive &&
            (inputRedirected || outputRedirected))
            throw AzclipsErrorException.UsageError(
                "Interactive troubleshooting requires terminal input and output. Use --style oneshot when redirecting input or output.");
    }

    private static async Task<int> RunRecoveryAsync(
        FailureRecord failure, AgentOptions options, string agentExe, string? styleOverride, CancellationToken cancellationToken)
    {
        var telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        var config = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
        (string? TenantId, string? Boundary) routing = (null, null);
        if (config.GetConfiguration<TelemetryOptions>().Enabled)
        {
            var resolved = await TelemetryRoutingResolver.ResolveAsync(
                AzclipsServiceProvider.GetRequiredService<IProfileService>(),
                AzclipsServiceProvider.GetRequiredService<IAuthService>(),
                profileNameOverride: null, cancellationToken);
            routing = (resolved.TenantId, resolved.Boundary);
        }

        return await RunAttemptAsync(failure, options, telemetry, routing,
            (request, traceparent, token) => AgentProcessRunner.RunAsync(agentExe, request, traceparent, token),
            cancellationToken, styleOverride);
    }

    internal static async Task<int> RunAttemptAsync(
        FailureRecord failure,
        AgentOptions options,
        ITelemetryService telemetry,
        (string? TenantId, string? Boundary) routing,
        Func<RecoveryRequest, string?, CancellationToken, Task<int>> runAgent,
        CancellationToken cancellationToken,
        string? styleOverride = null)
    {
        var session = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Manual, styleOverride);
        string? requestTraceId;
        string? requestSpanId;
        using (var activity = telemetry.StartRootActivity())
        {
            activity?.SetTag(TelemetryTags.Command, IntrinsicVerbs.Troubleshoot);
            activity?.SetTag(TelemetryTags.TenantId, routing.TenantId);
            activity?.SetTag(TelemetryTags.DataBoundary, routing.Boundary);
            activity?.SetStatus(ActivityStatusCode.Ok);
            requestTraceId = activity?.TraceId.ToHexString();
            requestSpanId = activity?.SpanId.ToHexString();
        }

        using var recovery = telemetry.StartRootActivity(name: "CLI.Activity.AiRecovery");
        recovery?.SetTag(TelemetryTags.TenantId, routing.TenantId);
        recovery?.SetTag(TelemetryTags.DataBoundary, routing.Boundary);
        recovery?.SetTag(TelemetryTags.CommandTraceId, ValidTraceIdentifier(failure.CommandTraceId, 32));
        recovery?.SetTag(TelemetryTags.CommandSpanId, ValidTraceIdentifier(failure.CommandSpanId, 16));
        recovery?.SetTag(TelemetryTags.RecoveryRequestTraceId, requestTraceId);
        recovery?.SetTag(TelemetryTags.RecoveryRequestSpanId, requestSpanId);
        recovery?.SetTag(TelemetryTags.RecoveryTrigger, "manual");
        recovery?.SetTag(TelemetryTags.RecoverySessionStyle, session.SessionStyle);
        recovery?.SetTag(TelemetryTags.RecoveryProvider, options.Provider);
        var request = new RecoveryRequest
        {
            Trigger = session.Trigger,
            FailedCommand = failure.FailedCommand,
            ErrorText = failure.ErrorText,
            SessionStyle = session.SessionStyle,
            FailureId = failure.Id,
            CommandTraceId = ValidTraceIdentifier(failure.CommandTraceId, 32),
            CommandSpanId = ValidTraceIdentifier(failure.CommandSpanId, 16),
            RequestTraceId = requestTraceId,
            RequestSpanId = requestSpanId,
            TenantId = routing.TenantId,
            DataBoundary = routing.Boundary,
        };

        try
        {
            // Explicit troubleshoot returns the agent exit code. Oneshot exits 0 only after it shows a plan
            // or final response and nonzero otherwise; interactive reports its last interaction.
            var exit = await runAgent(request, recovery?.Id, cancellationToken);
            recovery?.SetTag(TelemetryTags.RecoveryExitCode, exit);
            recovery?.SetStatus(exit == 0 ? ActivityStatusCode.Ok :
                exit == 130 ? ActivityStatusCode.Unset : ActivityStatusCode.Error);
            return exit;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            recovery?.SetTag(TelemetryTags.RecoveryExitCode, 130);
            recovery?.SetStatus(ActivityStatusCode.Unset);
            throw;
        }
        catch
        {
            recovery?.SetTag(TelemetryTags.RecoveryExitCode, 1);
            recovery?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    internal static string? ValidTraceIdentifier(string? value, int length) =>
        value is not null && value.Length == length && value.All(char.IsAsciiHexDigit) && value.Any(character => character != '0')
            ? value.ToLowerInvariant()
            : null;
}
