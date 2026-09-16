// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Telemetry;
using GitHub.Copilot;

namespace Core.Agent.Telemetry;

internal sealed record AgentModelObservation(string? Model, string Source, bool IsSelectionChange = false);

internal sealed class AgentModelTelemetry
{
    private static readonly TimeSpan ResolutionTimeout = TimeSpan.FromSeconds(5);
    private readonly Action<string> _reportWarning;
    private AgentModelObservation _selection;

    internal AgentModelTelemetry(AgentOptions options, Action<string> reportWarning)
    {
        IsBuiltin = string.Equals(options.Provider, "builtin", StringComparison.OrdinalIgnoreCase);
        var configuredModel = RecoveryTelemetry.GetConfiguredModel(options);
        _selection = new(configuredModel,
            configuredModel is null ? "unavailable" : RecoveryTelemetry.ConfigurationSource);
        _reportWarning = reportWarning;
    }

    internal bool IsBuiltin { get; }
    internal AgentModelObservation Current => Volatile.Read(ref _selection);

    internal void Observe(SessionEvent sessionEvent)
    {
        if (IsBuiltin || !string.IsNullOrEmpty(sessionEvent.AgentId))
            return;

        var model = sessionEvent switch
        {
            SessionStartEvent started => started.Data?.SelectedModel,
            SessionResumeEvent resumed => resumed.Data?.SelectedModel,
            SessionModelChangeEvent changed => changed.Data?.NewModel,
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(model))
        {
            var observation = new AgentModelObservation(model, "sdk", sessionEvent is SessionModelChangeEvent);
            if (observation.IsSelectionChange)
            {
                Interlocked.Exchange(ref _selection, observation);
            }
            else
            {
                // Startup events may be delivered after the more recent RPC response.
                var current = Current;
                if (current.Model is null)
                    Interlocked.CompareExchange(ref _selection, observation, current);
            }
        }
    }

    internal async Task InitializeAsync(CopilotSession session, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var beforeQuery = Current;
        if (IsBuiltin || beforeQuery.Model is not null)
            return;

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(ResolutionTimeout);
        try
        {
#pragma warning disable GHCP001 // SDK 1.0.0 marks this read-only metadata API experimental.
            var selected = await session.Rpc.Model.GetCurrentAsync(deadline.Token).ConfigureAwait(false);
            var model = selected?.ModelId;
#pragma warning restore GHCP001
            if (!string.IsNullOrWhiteSpace(model))
            {
                var current = Current;
                while (!current.IsSelectionChange)
                {
                    var previous = Interlocked.CompareExchange(ref _selection,
                        new AgentModelObservation(model, "sdk"), current);
                    if (ReferenceEquals(previous, current))
                        break;
                    current = previous;
                }
            }
            else
            {
                ReportUnavailable("the SDK did not return a selected model");
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            ReportUnavailable("the startup model query timed out");
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // A cancelled caller tears the transport down, so the failure it produces arrives here
            // as an IOException rather than an OperationCanceledException. Re-raise it as
            // cancellation instead of reporting a model lookup that merely failed, which would let
            // InitializeAsync return normally and swallow the caller's stop.
            ct.ThrowIfCancellationRequested();
            ReportUnavailable($"the startup model query failed ({ex.GetType().Name})");
        }
    }

    internal void RecordTo(Activity? activity)
    {
        var model = Current;
        RecoveryTelemetry.RecordModel(activity, model.Model, model.Source);
    }

    private void ReportUnavailable(string reason)
    {
        if (Current.Model is null)
            _reportWarning($"GitHub model metadata is unavailable: {reason}. Recovery will continue; usage events may identify the model.");
    }
}

