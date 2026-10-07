// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using GitHub.Copilot;
using Microsoft.Extensions.AI;

namespace Core.Agent.Telemetry;

/// <summary>
/// One holder per agent. SDK callbacks can retain Create's ExecutionContext, so the
/// current operation is read at invocation time, never captured in the tool delegate.
/// </summary>
internal sealed class AgentInvocationContext(
    Activity? session, ITelemetryService telemetry, ITelemetryLayerScope? scope = null,
    CancellationToken lifetimeToken = default) : IDisposable
{
    private readonly object _gate = new();
    private readonly AsyncLocal<CallbackBinding?> _callback = new();
    private Activity? _session = session;
    private Operation? _operation;
    private bool _closing;
    private bool _disposed;
    private int _teardown;

    internal CancellationToken StoppingToken => scope?.StoppingToken ?? lifetimeToken;
    internal CancellationToken CallbackCancellationToken => _callback.Value?.CancellationToken ?? lifetimeToken;
    internal ITelemetryService Telemetry => telemetry;
    internal IDisposable? StartToolActivity(string name) => telemetry.StartActivity(name);

    internal IDisposable? EnterProducer()
    {
        var producer = scope?.TryEnterProducer();
        if (scope is not null && producer is null)
            throw new OperationCanceledException("The Agent layer is draining.", StoppingToken);
        return producer;
    }

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

            var producer = scope?.TryEnterProducer();
            if (scope is not null && producer is null)
                throw new OperationCanceledException("The Agent layer is draining.", StoppingToken);
            try
            {
                var parent = IsWithin(Activity.Current, _session) ? Activity.Current : LiveSession();
                var activityScope = telemetry.StartActivityWithParent(name, parent?.Context ?? default);
                return _operation = new Operation(this, activityScope, turnCts, cancellationToken, producer);
            }
            catch
            {
                producer?.Dispose();
                throw;
            }
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
            config.OnEvent = ev => HandleEvent(() => onEvent(ev));
    }

    internal void HandleEvent(Action callback)
    {
        try
        {
            InvokeCallbackAsync(_ =>
            {
                callback();
                return ValueTask.FromResult(true);
            }).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (StoppingToken.IsCancellationRequested)
        {
            // Late events after the layer fence are not a second session failure.
        }
        catch (ObjectDisposedException) when (StoppingToken.IsCancellationRequested)
        {
        }
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
            var enclosing = _callback.Value is { IsActive: true } active ? active : null;
            var nested = enclosing is not null;
            ObjectDisposedException.ThrowIf(_disposed && !nested, this);
            if (_closing && _teardown == 0 && !nested)
                throw new OperationCanceledException("The Agent callback admission is closed.", StoppingToken);
            var producer = scope?.TryEnterProducer(teardown: nested || _teardown != 0);
            if (scope is not null && producer is null)
                throw new OperationCanceledException("The Agent layer is draining.", StoppingToken);
            var parent = enclosing?.Parent ?? _operation?.Activity ?? LiveSession();
            if (IsWithin(Activity.Current, parent, allowStoppedAncestor: enclosing is not null))
                parent = Activity.Current;
            if (parent is { IsStopped: true } && !ReferenceEquals(Activity.Current, parent))
                parent = LiveSession();
            var binding = new CallbackBinding(parent,
                enclosing?.CancellationToken ?? _operation?.CancellationToken ?? default,
                cancellationToken, lifetimeToken, StoppingToken, producer);
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

    internal IDisposable EnterTeardown()
    {
        lock (_gate)
        {
            _closing = true;
            _teardown++;
        }
        return new TeardownLease(this);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _closing = true;
            _disposed = true;
            _operation = null;
            _session = null;
        }
        // The actual send/callback owns its activity and producer lease. At a deadline the layer
        // writes an immutable abandoned record; this thread must not Stop another thread's span.
    }

    internal sealed class Operation(
        AgentInvocationContext owner, TelemetryActivityScope? activityScope,
        CancellationTokenSource? turnCts, CancellationToken cancellationToken,
        IDisposable? producer) : IDisposable
    {
        private int _disposed;
        internal Activity? Activity => activityScope?.Activity;

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
            try
            {
                activityScope?.Dispose();
            }
            finally
            {
                lock (owner._gate)
                {
                    if (ReferenceEquals(owner._operation, this))
                        owner._operation = null;
                }
                try { Disposed?.Invoke(); } catch { }
                finally { producer?.Dispose(); }
            }
        }
    }

    private sealed class CallbackBinding : IDisposable
    {
        private readonly CancellationTokenSource? _linked;
        private readonly IDisposable? _producer;
        private int _active = 1;

        internal CallbackBinding(
            Activity? parent, CancellationToken operationToken, CancellationToken callbackToken,
            CancellationToken lifetimeToken, CancellationToken stoppingToken, IDisposable? producer)
        {
            var tokens = new[] { operationToken, callbackToken, lifetimeToken, stoppingToken }
                .Where(token => token.CanBeCanceled).Distinct().ToArray();
            if (tokens.Length > 0)
                _linked = CancellationTokenSource.CreateLinkedTokenSource(tokens);
            CancellationToken = _linked?.Token ?? default;
            Parent = parent;
            _producer = producer;
        }

        internal Activity? Parent { get; }
        internal CancellationToken CancellationToken { get; }
        internal bool IsActive => Volatile.Read(ref _active) != 0;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _active, 0) == 0)
                return;
            _linked?.Dispose();
            _producer?.Dispose();
        }
    }

    private sealed class TeardownLease(AgentInvocationContext owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                lock (owner._gate) owner._teardown--;
            }
        }
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
