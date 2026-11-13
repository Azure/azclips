// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Core.Abstractions;
using Core.Agent.Telemetry;
using GitHub.Copilot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Tests.Common.Utilities;

namespace Core.Agent.Tests;

/// <summary>A real <see cref="ChatAgent"/> and SDK client connected to an <see cref="OfflineCopilotServer"/>.</summary>
internal sealed class OfflineChatAgent : IAsyncDisposable
{
    internal const string SessionId = "offline-chat-agent-session";

    private OfflineChatAgent(ChatAgent agent, CopilotClient client, CopilotSession session,
        AgentModelTelemetry metadata, List<string> warnings, ConcurrentQueue<string> sdkErrors, AgentTraceCapture trace)
    {
        Agent = agent;
        Client = client;
        Session = session;
        Metadata = metadata;
        Warnings = warnings;
        SdkErrors = sdkErrors;
        Trace = trace;
    }

    internal ChatAgent Agent { get; }
    internal CopilotClient Client { get; }
    internal CopilotSession Session { get; }
    internal AgentModelTelemetry Metadata { get; }

    /// <summary>Warnings the model telemetry reported to the user.</summary>
    internal List<string> Warnings { get; }

    /// <summary>Warnings and errors the SDK logged.</summary>
    internal ConcurrentQueue<string> SdkErrors { get; }

    internal AgentTraceCapture Trace { get; }

    /// <summary>
    /// Connects a <see cref="ChatAgent"/> to <paramref name="server"/>, runs <paramref name="body"/>, then
    /// disposes the agent. When <paramref name="body"/> succeeds, a fault the server recorded fails the test.
    /// </summary>
    /// <param name="configuredModel">A builtin deployment name; empty selects the github provider.</param>
    /// <param name="initializeModel">Resolve the model before <paramref name="body"/> runs.</param>
    /// <param name="timeProvider">The clock for the model query deadline; by default a clock that never advances.</param>
    internal static async Task RunAsync(
        OfflineCopilotServer server, Func<OfflineChatAgent, Task> body, string configuredModel = "",
        bool initializeModel = true, bool telemetryEnabled = true, TimeProvider? timeProvider = null)
    {
        await using (var agent = await CreateAsync(server, configuredModel, initializeModel, telemetryEnabled, timeProvider))
            await body(agent);
        server.ThrowIfFaulted();
    }

    private static async Task<OfflineChatAgent> CreateAsync(
        OfflineCopilotServer server, string configuredModel, bool initializeModel, bool telemetryEnabled,
        TimeProvider? timeProvider)
    {
        var warnings = new List<string>();
        var options = new AgentOptions
        {
            Provider = string.IsNullOrEmpty(configuredModel) ? "github" : "builtin",
            Builtin = new BuiltinProviderOptions { ModelId = configuredModel },
        };
        var metadata = new AgentModelTelemetry(options, warnings.Add, timeProvider ?? new FakeTimeProvider());
        var sdkErrors = new ConcurrentQueue<string>();
        var trace = new AgentTraceCapture(telemetryEnabled, listen: false);
        var context = new AgentInvocationContext(null, trace.Telemetry);
        var client = new CopilotClient(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForUri(server.Url),
            Logger = new SdkErrorLogger(sdkErrors),
        });
        try
        {
            var config = new SessionConfig
            {
                SessionId = SessionId,
                Model = string.IsNullOrEmpty(configuredModel) ? null : configuredModel,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                OnEvent = metadata.Observe,
            };
            context.Bind(config);
            var session = await client.CreateSessionAsync(config).WaitAsync(AgentTestTimeouts.HangGuard);
            if (initializeModel)
                await metadata.InitializeAsync(session, CancellationToken.None).WaitAsync(AgentTestTimeouts.HangGuard);
            return new OfflineChatAgent(new ChatAgent(client, session, metadata, state: null, context),
                client, session, metadata, warnings, sdkErrors, trace);
        }
        catch
        {
            await client.DisposeAsync();
            context.Dispose();
            trace.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await Agent.DisposeAsync().AsTask().WaitAsync(AgentTestTimeouts.HangGuard); }
        finally { Trace.Dispose(); }
    }

    private sealed class SdkErrorLogger(ConcurrentQueue<string> errors) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
                errors.Enqueue(formatter(state, exception));
        }
    }
}
