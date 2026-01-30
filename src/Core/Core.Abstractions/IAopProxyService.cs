using System;
using System.Reflection;

namespace Core.Abstractions;

/// <summary>
/// Creates proxies that can intercept interface method calls.
/// </summary>
public interface IAopProxyService
{
    /// <summary>
    /// Creates a proxy that intercepts calls to the interface type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="target">The target implementation to wrap.</param>
    /// <param name="interceptor">Interceptor invoked on method enter/exit.</param>
    /// <typeparam name="T">Interface type to proxy.</typeparam>
    T Create<T>(T target, IInvocationInterceptor interceptor) where T : class;
}

/// <summary>
/// Intercepts method invocations with optional state.
/// </summary>
public interface IInvocationInterceptor
{
    /// <summary>
    /// Called before the target method is invoked.
    /// </summary>
    /// <param name="method">Resolved implementation method when available.</param>
    /// <param name="target">Target instance being invoked.</param>
    /// <param name="args">Invocation arguments.</param>
    /// <returns>Optional state passed to <see cref="OnExit"/>.</returns>
    object? OnEnter(MethodInfo method, object target, object?[]? args);

    /// <summary>
    /// Called after the target method completes or throws.
    /// </summary>
    /// <param name="method">Resolved implementation method when available.</param>
    /// <param name="target">Target instance being invoked.</param>
    /// <param name="args">Invocation arguments.</param>
    /// <param name="result">Return value when successful; null for void or failed.</param>
    /// <param name="exception">Exception when failed; otherwise null.</param>
    /// <param name="state">State returned by <see cref="OnEnter"/>.</param>
    void OnExit(MethodInfo method, object target, object?[]? args, object? result, Exception? exception, object? state);
}

/// <summary>
/// Binds an attribute to a delegate-based interceptor.
/// </summary>
public sealed class AttributeDelegateInterceptor<TAttribute> : IInvocationInterceptor where TAttribute : Attribute
{
    private readonly Func<MethodInfo, object, object?[]?, TAttribute, object?> _onEnter;
    private readonly Action<MethodInfo, object, object?[]?, object?, Exception?, TAttribute, object?>? _onExit;
    private readonly Func<MethodInfo, TAttribute?> _attributeSelector;

    /// <summary>
    /// Creates an interceptor that only runs when <typeparamref name="TAttribute"/> is present.
    /// </summary>
    /// <param name="onEnter">Delegate invoked on method entry.</param>
    /// <param name="onExit">Optional delegate invoked on method exit.</param>
    /// <param name="attributeSelector">Optional selector for retrieving the attribute.</param>
    public AttributeDelegateInterceptor(
        Func<MethodInfo, object, object?[]?, TAttribute, object?> onEnter,
        Action<MethodInfo, object, object?[]?, object?, Exception?, TAttribute, object?>? onExit = null,
        Func<MethodInfo, TAttribute?>? attributeSelector = null)
    {
        _onEnter = onEnter ?? throw new ArgumentNullException(nameof(onEnter));
        _onExit = onExit;
        _attributeSelector = attributeSelector ?? (m => m.GetCustomAttribute<TAttribute>());
    }

    /// <inheritdoc />
    public object? OnEnter(MethodInfo method, object target, object?[]? args)
    {
        var attribute = _attributeSelector(method);
        if (attribute == null)
        {
            return null;
        }

        var state = _onEnter(method, target, args, attribute);
        return new AttributeState<TAttribute>(attribute, state);
    }

    /// <inheritdoc />
    public void OnExit(MethodInfo method, object target, object?[]? args, object? result, Exception? exception, object? state)
    {
        if (_onExit == null || state is not AttributeState<TAttribute> attrState)
        {
            return;
        }

        _onExit(method, target, args, result, exception, attrState.Attribute, attrState.State);
    }

    private sealed record AttributeState<T>(T Attribute, object? State) where T : Attribute;
}
