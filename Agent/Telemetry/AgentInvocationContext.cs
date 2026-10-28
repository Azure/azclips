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
internal sealed class AgentInvocationContext(
    Activity? session, ITelemetryService telemetry, AgentRuntimeLifetime? lifetime = null,
    CancellationToken lifetimeToken = default) : IDisposable
{
    private readonly object _gate = new();
    private readonly AsyncLocal<CallbackBinding?> _callback = new();
    private Activity? _session = session;
    private Operation? _operation;
    private bool _closing;
    private bool _disposed;
    private int _teardown;

    internal CancellationToken StoppingToken => lifetime?.StoppingToken ?? lifetimeToken;
    internal CancellationToken CallbackCancellationToken => _callback.Value?.CancellationToken ?? lifetimeToken;
    internal ITelemetryService Telemetry => telemetry;
    internal IDisposable? StartToolActivity(string name) => telemetry.StartActivity(name);

    internal IDisposable? EnterProducer()
    {
        var producer = lifetime?.TryEnterProducer();
        if (lifetime is not null && producer is null)
            throw new OperationCanceledException("The Agent is draining.", StoppingToken);
        return producer;
    }

    internal Operation StartOperation(string name, CancellationToken cancellationToken = default) =>
        StartOperation(name, turnCts: null, cancellationToken);

    /// <summary>Reserves the operation and its cancellation source together.</summary>
    /// <remarks>The caller owns turnCts and disposes it after this operation leaves the slot.</remarks>
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

            var producer = lifetime?.TryEnterProducer();
            if (lifetime is not null && producer is null)
                throw new OperationCanceledException("The Agent is draining.", StoppingToken);
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

    /// <summary>The currently reserved operation.</summary>
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
        // Cancel outside _gate because registered callbacks can re-enter this context.
        // Keep cancellation best-effort if source disposal races or a callback throws.
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
            var producer = lifetime?.TryEnterProducer(teardown: nested || _teardown != 0);
            if (lifetime is not null && producer is null)
                throw new OperationCanceledException("The Agent is draining.", StoppingToken);
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
        // Timed-out producers keep the file unpublished; never Stop another thread's span.
    }

    internal sealed class Operation(
        AgentInvocationContext owner, TelemetryActivityScope? activityScope,
        CancellationTokenSource? turnCts, CancellationToken cancellationToken,
        IDisposable? producer) : IDisposable
    {
        private int _disposed;
        internal Activity? Activity => activityScope?.Activity;

        /// <summary>The borrowed cancellation source; the caller disposes it after the operation leaves the slot.</summary>
        internal CancellationTokenSource? TurnCts { get; } = turnCts;

        internal CancellationToken CancellationToken { get; } = turnCts?.Token ?? cancellationToken;

        /// <summary>Raised once after the operation leaves its slot.</summary>
        /// <remarks>
        /// Concurrent teardown may already have disposed <see cref="TurnCts"/>.
        /// Subscribers must not assume that the source is still usable.
        /// </remarks>
        internal event Action? Disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            // Stop before clearing the slot so the next operation cannot inherit an ending span.
            // Stop outside _gate because telemetry processors run synchronously.
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
