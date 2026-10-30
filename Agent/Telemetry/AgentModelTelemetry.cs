// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Agent.Providers;
using Core.Telemetry;
using GitHub.Copilot;

namespace Core.Agent.Telemetry;

internal sealed record AgentModelObservation(string? Model, AgentModelSource Source, bool IsSelectionChange = false);

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
            configuredModel is null ? AgentModelSource.Unavailable : AgentModelSource.Configuration);
        _reportWarning = reportWarning;
        if (IsBuiltin)
            ServedModel = new ServedModel(reportWarning);
    }

    internal bool IsBuiltin { get; }

    /// <summary>The builtin gateway's served model for this session; null for other providers.</summary>
    internal ServedModel? ServedModel { get; }
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
        if (RecoveryTelemetry.IsExportableModel(model))
        {
            var observation = new AgentModelObservation(model, AgentModelSource.Sdk, sessionEvent is SessionModelChangeEvent);
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
        string? unavailable = null;
        try
        {
#pragma warning disable GHCP001 // The SDK marks this read-only metadata API experimental.
            var selected = await session.Rpc.Model.GetCurrentAsync(deadline.Token).ConfigureAwait(false);
            var model = selected?.ModelId;
#pragma warning restore GHCP001
            if (RecoveryTelemetry.IsExportableModel(model))
            {
                var current = Current;
                while (!current.IsSelectionChange)
                {
                    var previous = Interlocked.CompareExchange(ref _selection,
                        new AgentModelObservation(model, AgentModelSource.Sdk), current);
                    if (ReferenceEquals(previous, current))
                        break;
                    current = previous;
                }
            }
            else
            {
                // Computed here, reported after the try. Reporting inside it would put the warning
                // channel — a console renderer — inside the scope guarding the RPC, so a renderer
                // fault on the path where the query SUCCEEDED would be diagnosed as a query
                // failure, and the second ReportUnavailable in that catch would re-enter the same
                // broken channel and escape, ending the session this method exists not to end.
                unavailable = string.IsNullOrWhiteSpace(model)
                    ? "the SDK did not return a selected model"
                    : "the SDK returned a model name outside the recordable set";
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            unavailable = "the startup model query timed out";
        }
        catch (Exception ex)
        {
            // The RPC behind this query is experimental, so the set of types it can throw is not
            // fixed: a JSON-RPC fault, a transport type, or something a later SDK adds all land
            // here. None of them is a reason to end a recovery session the user is waiting on —
            // this is a read-only metadata query whose contract is already "recovery continues".
            // A cancelled caller tears the transport down, so the failure it produces can arrive
            // as a transport exception rather than an OperationCanceledException. Re-raise it as
            // cancellation instead of reporting a model lookup that merely failed, which would
            // let InitializeAsync return normally and swallow the caller's stop.
            ct.ThrowIfCancellationRequested();
            unavailable = $"the startup model query failed ({ex.GetType().Name})";
        }

        if (unavailable is not null)
            ReportUnavailable(unavailable);
    }

    internal void RecordTo(Activity? activity)
    {
        var model = Current;
        RecoveryTelemetry.RecordModel(activity, model.Model, model.Source);
    }

    /// <summary>
    /// Records the last served model of the session with source <c>response</c>, or the
    /// current selection when no served model was seen.
    /// </summary>
    internal void RecordServedTo(Activity? activity)
    {
        if (ServedModel?.Latest is { } served)
            RecoveryTelemetry.RecordModel(activity, served, AgentModelSource.Response);
        else
            RecordTo(activity);
    }

    private void ReportUnavailable(string reason)
    {
        if (Current.Model is not null)
            return;

        // Guarded because this is the last thing standing between a broken console renderer and a
        // dead recovery session. Everything else in this class exists so a metadata query cannot
        // end the session; announcing that it failed must not do it either.
        try
        {
            _reportWarning($"GitHub model metadata is unavailable: {reason}. Recovery will continue; usage events may identify the model.");
        }
        catch
        {
        }
    }
}
