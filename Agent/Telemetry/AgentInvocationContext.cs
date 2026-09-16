// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using GitHub.Copilot;
using Microsoft.Extensions.AI;

namespace Core.Agent.Telemetry;

/// <summary>
/// One holder per agent. SDK callbacks can retain Create's ExecutionContext, so the
/// current operation is read at invocation time, never captured in the tool delegate.
/// </summary>
internal sealed class AgentInvocationContext(Activity? session) : IDisposable
{
    private readonly object _gate = new();
    private readonly AsyncLocal<CallbackBinding?> _callback = new();
    private Activity? _session = session;
    private Operation? _operation;
    private bool _closing;
    private bool _disposed;

    internal Operation StartOperation(string name, CancellationToken cancellationToken = default) =>
        StartOperation(name, turnCts: null, cancellationToken);

    /// <summary>
    /// Reserves the slot for a turn that owns a <see cref="CancellationTokenSource"/>, so the
    /// handle an abort has to cancel and the slot it has to find are the same reservation rather
    /// than two fields updated separately. The source is borrowed, never owned: the caller's
    /// <c>using</c> disposes it, and must do so only after this operation has left the slot.
    /// </summary>
    internal Operation StartOperation(string name, CancellationTokenSource turnCts)
    {
        ArgumentNullException.ThrowIfNull(turnCts);
        return StartOperation(name, turnCts, cancellationToken: default);
    }

    private Operation StartOperation(string name, CancellationTokenSource? turnCts, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closing || _disposed, this);
            // Reject before writing anything: a caller that loses the race for the slot must leave
            // nothing of itself behind, or it disarms the caller that won.
            if (_operation is not null)
                throw new InvalidOperationException("An agent session supports only one in-flight operation.");

            var parent = IsWithin(Activity.Current, _session) ? Activity.Current : LiveSession();
            var activity = TelemetryHierarchy.StartActivity(
                AgentTracing.Source, name, parentContext: parent?.Context ?? default,
                parentHierarchy: TelemetryHierarchy.Capture(parent));
            return _operation = new Operation(this, activity, turnCts, cancellationToken);
        }
    }

    /// <summary>
    /// The reservation currently in the slot. The slot is private, so without this nothing --
    /// including a test -- can observe which operation, and therefore which cancellation handle,
    /// an abort would actually reach.
    /// </summary>
    internal Operation? CurrentOperation
    {
        get { lock (_gate) return _operation; }
    }

    /// <summary>The cancellation source carried by the reservation currently in the slot.</summary>
    internal CancellationTokenSource? CurrentTurnCts => CurrentOperation?.TurnCts;

    /// <summary>
    /// Cancels the turn currently holding the slot. A no-op when the slot is empty, or when its
    /// occupant was reserved without a source -- <c>Agent.Create</c> takes the token overload, so
    /// create is not cancellable through here.
    /// </summary>
    internal void CancelCurrentOperation()
    {
        CancellationTokenSource? cts;
        lock (_gate) { cts = _operation?.TurnCts; }
        // Outside the lock on purpose: Cancel runs registrations synchronously on this thread, and
        // those continuations can re-enter this type. Taking the reference under the lock is
        // enough -- the send that owns the source cannot dispose it until its Operation has left
        // the slot, so what is read here is either live or already unreachable.
        //
        // The catch is deliberately as broad as the three call sites it replaces, which all used a
        // bare `catch { }`. ObjectDisposedException is the expected case (the send finished between
        // the lock release and the Cancel), but Cancel also rethrows a registered callback's
        // exception wrapped in AggregateException; narrowing this would let an SDK callback's
        // failure escape into an abort path that today swallows everything.
        try { cts?.Cancel(); } catch { }
    }

    internal void Bind(SessionConfig config)
    {
        if (config.Tools is not null)
        {
            config.Tools = config.Tools.Select(tool =>
                tool.GetService<AIFunction>() is { } function
                    ? new BoundFunction(this, function)
                    : tool).ToList();
        }

        if (config.Hooks is { } hooks)
        {
            if (hooks.OnPreToolUse is { } preTool)
                hooks.OnPreToolUse = async (input, invocation) =>
                    await InvokeCallbackAsync(async _ => await preTool(input, invocation).ConfigureAwait(false))
                        .ConfigureAwait(false);
            if (hooks.OnSessionStart is { } sessionStart)
                hooks.OnSessionStart = async (input, invocation) =>
                    await InvokeCallbackAsync(async _ => await sessionStart(input, invocation).ConfigureAwait(false))
                        .ConfigureAwait(false);
            if (hooks.OnUserPromptSubmitted is { } promptSubmitted)
                hooks.OnUserPromptSubmitted = async (input, invocation) =>
                    await InvokeCallbackAsync(async _ => await promptSubmitted(input, invocation).ConfigureAwait(false))
                        .ConfigureAwait(false);
        }

        if (config.OnPermissionRequest is { } permission)
            config.OnPermissionRequest = async (request, invocation) =>
                await InvokeCallbackAsync(async _ => await permission(request, invocation).ConfigureAwait(false))
                    .ConfigureAwait(false);
        if (config.OnEvent is { } onEvent)
            config.OnEvent = ev => InvokeCallbackAsync(_ =>
                {
                    onEvent(ev);
                    return ValueTask.FromResult(true);
                }).GetAwaiter().GetResult();
    }

    private async ValueTask<T> InvokeCallbackAsync<T>(
        Func<CancellationToken, ValueTask<T>> callback, CancellationToken cancellationToken = default)
    {
        using var binding = EnterCallback(cancellationToken);
        // An async boundary restores the caller's ExecutionContext even for synchronous
        // callbacks. Assigning the old Activity.Current in Dispose is insufficient:
        // the public setter refuses the stopped Create retained by the SDK.
        _callback.Value = binding;
        return await callback(binding.CancellationToken).ConfigureAwait(false);
    }

    private CallbackBinding EnterCallback(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var enclosing = _callback.Value;
            var parent = enclosing?.Parent ?? _operation?.Activity ?? LiveSession();
            if (IsWithin(Activity.Current, parent, allowStoppedAncestor: enclosing is not null))
                parent = Activity.Current;
            if (parent is { IsStopped: true } && !ReferenceEquals(Activity.Current, parent))
                parent = LiveSession();
            var binding = new CallbackBinding(parent,
                enclosing?.CancellationToken ?? _operation?.CancellationToken ?? default, cancellationToken);
            // Bind while the operation lease is locked, before it can be cleared/stopped.
            if (!ReferenceEquals(Activity.Current, parent))
                Activity.Current = parent;
            return binding;
        }
    }

    private Activity? LiveSession() => _session is { IsStopped: false } ? _session : null;

    private static bool IsWithin(Activity? activity, Activity? ancestor, bool allowStoppedAncestor = false)
    {
        if (activity is null || activity.IsStopped || ancestor is null
            || (!allowStoppedAncestor && ancestor.IsStopped))
            return false;
        for (var current = activity; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }
        return false;
    }

    internal bool BeginDispose()
    {
        lock (_gate)
        {
            if (_closing)
                return false;
            _closing = true;
            return true;
        }
    }

    public void Dispose()
    {
        Operation? orphan;
        lock (_gate)
        {
            if (_disposed)
                return;
            _closing = true;
            _disposed = true;
            orphan = _operation;
            _operation = null;
            _session = null;
        }

        // An operation still in the slot means a send outlived the dispose timeout. Stopping its
        // activity here is what keeps it in the data: an Activity that is never stopped is never
        // exported, so the one turn that hung -- the turn worth seeing -- would be the one turn
        // missing. Its own Dispose is idempotent, so the late send closing afterwards is a no-op
        // rather than a double stop. Done outside the lock: stopping an activity runs exporter
        // callbacks, and this type's own callbacks take the same gate.
        orphan?.MarkAbandoned();
        orphan?.Dispose();
    }

    internal sealed class Operation(
        AgentInvocationContext owner, Activity? activity,
        CancellationTokenSource? turnCts, CancellationToken cancellationToken) : IDisposable
    {
        private int _disposed;
        internal Activity? Activity { get; } = activity;

        /// <summary>
        /// The turn's cancellation source, when the slot was reserved with one. Borrowed, not
        /// owned: this type never disposes it. Its owner outlives this operation on purpose, so a
        /// concurrent <see cref="CancelCurrentOperation"/> can only ever read a live source.
        /// </summary>
        internal CancellationTokenSource? TurnCts { get; } = turnCts;

        internal CancellationToken CancellationToken { get; } = turnCts?.Token ?? cancellationToken;

        /// <summary>
        /// Raised once, by whichever caller performed the disposal, after this operation has left
        /// the slot.
        /// </summary>
        /// <remarks>
        /// It does <b>not</b> promise that <see cref="TurnCts"/> is still usable when it fires.
        /// Disposal is idempotent and the loser of that race returns immediately, so a concurrent
        /// teardown can reach the raise after the send that owns the source has already disposed
        /// it. Subscribers must therefore treat the source as possibly dead — which is what the
        /// disposal-order test asserts on, rather than assuming.
        /// </remarks>
        internal event Action? Disposed;

        /// <summary>
        /// Records that the owning context was torn down while this operation was still running, so
        /// the exported span says the turn was abandoned rather than looking like a normal close.
        /// </summary>
        internal void MarkAbandoned()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            Activity?.SetStatus(ActivityStatusCode.Error, "Abandoned: the agent was disposed while this send was in flight.");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            // Stop the activity before the slot opens, not after. Between the two, StartOperation
            // and EnterCallback both look at Activity.Current to pick a parent, and IsWithin only
            // rejects an activity once it is stopped. Freeing the slot first let a new operation in
            // while this one still looked live, so it was parented under an ending sibling instead
            // of the session. Outside the lock deliberately: stopping an activity runs the
            // telemetry processors synchronously, which should not happen while holding _gate.
            Activity?.Dispose();

            lock (owner._gate)
            {
                if (ReferenceEquals(owner._operation, this))
                    owner._operation = null;
            }

            // After the slot is clear and outside the lock, so a subscriber sees the state it is
            // there to observe. Swallowing is deliberate: this runs inside SendAsync's using and
            // inside AgentInvocationContext.Dispose's tail, so an escaping subscriber exception
            // would replace whatever real failure was already unwinding.
            try { Disposed?.Invoke(); } catch { }
        }
    }

    private sealed class CallbackBinding : IDisposable
    {
        private readonly CancellationTokenSource? _linked;

        internal CallbackBinding(Activity? parent, CancellationToken operationToken, CancellationToken callbackToken)
        {
            if (operationToken.CanBeCanceled && callbackToken.CanBeCanceled && operationToken != callbackToken)
                _linked = CancellationTokenSource.CreateLinkedTokenSource(operationToken, callbackToken);
            CancellationToken = _linked?.Token
                ?? (callbackToken.CanBeCanceled ? callbackToken : operationToken);
            Parent = parent;
        }

        internal Activity? Parent { get; }
        internal CancellationToken CancellationToken { get; }

        public void Dispose() => _linked?.Dispose();
    }

    private sealed class BoundFunction(AgentInvocationContext owner, AIFunction inner) : DelegatingAIFunction(inner)
    {
        // The SDK resolves handlers with GetService<AIFunction>(); returning the inner
        // function here would silently bypass the execution-time binding.
        public override object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType == typeof(AIFunction)
                ? this
                : base.GetService(serviceType, serviceKey);

        protected override ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments, CancellationToken cancellationToken) =>
            owner.InvokeCallbackAsync(token => InnerFunction.InvokeAsync(arguments, token), cancellationToken);
    }
}
