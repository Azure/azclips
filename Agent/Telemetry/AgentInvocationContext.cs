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

    internal Operation StartOperation(string name, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closing || _disposed, this);
            if (_operation is not null)
                throw new InvalidOperationException("An agent session supports only one in-flight operation.");

            var parent = IsWithin(Activity.Current, _session) ? Activity.Current : LiveSession();
            var activity = TelemetryHierarchy.StartActivity(
                AgentTracing.Source, name, parentContext: parent?.Context ?? default,
                parentHierarchy: TelemetryHierarchy.Capture(parent));
            return _operation = new Operation(this, activity, cancellationToken);
        }
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
        lock (_gate)
        {
            _closing = true;
            _disposed = true;
            _operation = null;
            _session = null;
        }
    }

    internal sealed class Operation(
        AgentInvocationContext owner, Activity? activity, CancellationToken cancellationToken) : IDisposable
    {
        private int _disposed;
        internal Activity? Activity { get; } = activity;
        internal CancellationToken CancellationToken { get; } = cancellationToken;

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
