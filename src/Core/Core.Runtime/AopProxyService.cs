using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Core.Abstractions;

namespace Core.Runtime;

/// <summary>
/// DispatchProxy-based AOP service for interface interception.
/// </summary>
public sealed class AopProxyService : IAopProxyService
{
    /// <summary>
    /// Creates a proxy that intercepts interface calls and routes them to the interceptor.
    /// </summary>
    /// <typeparam name="T">Interface type to proxy.</typeparam>
    /// <param name="target">Target implementation to wrap.</param>
    /// <param name="interceptor">Interceptor for enter/exit hooks.</param>
    public T Create<T>(T target, IInvocationInterceptor interceptor) where T : class
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (interceptor == null) throw new ArgumentNullException(nameof(interceptor));

        var proxy = DispatchProxy.Create<T, AopProxyImpl<T>>();
        var impl = (AopProxyImpl<T>)(object)proxy!;
        impl.Target = target;
        impl.Interceptor = interceptor;
        return proxy!;
    }

    private class AopProxyImpl<T> : DispatchProxy where T : class
    {
        public required T Target { get; set; }
        public required IInvocationInterceptor Interceptor { get; set; }

        /// <summary>
        /// Invokes the target method with interception and async-aware exit handling.
        /// </summary>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null) throw new ArgumentNullException(nameof(targetMethod));

            var implMethod = ResolveImplementationMethod(targetMethod) ?? targetMethod;
            var state = Interceptor.OnEnter(implMethod, Target, args);

            try
            {
                var result = InvokeTarget(targetMethod, args);

                if (result is Task task)
                {
                    return InterceptTask(task, implMethod, Target, args, state);
                }

                Interceptor.OnExit(implMethod, Target, args, result, null, state);
                return result;
            }
            catch (Exception ex)
            {
                Interceptor.OnExit(implMethod, Target, args, null, ex, state);
                throw;
            }
        }

        /// <summary>
        /// Resolves the concrete implementation method on the target type.
        /// </summary>
        private MethodInfo? ResolveImplementationMethod(MethodInfo interfaceMethod)
        {
            var parameterTypes = interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            return Target.GetType().GetMethod(
                interfaceMethod.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
        }

        /// <summary>
        /// Invokes the target method and unwraps any TargetInvocationException.
        /// </summary>
        private object? InvokeTarget(MethodInfo targetMethod, object?[]? args)
        {
            try
            {
                return targetMethod.Invoke(Target, args);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        /// <summary>
        /// Wraps a Task or Task&lt;TResult&gt; to ensure exit hooks run after completion.
        /// </summary>
        private object InterceptTask(Task task, MethodInfo method, object target, object?[]? args, object? state)
        {
            var taskType = task.GetType();
            if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = taskType.GetGenericArguments()[0];
                var helper = GetType().GetMethod(nameof(InterceptGenericTask), BindingFlags.Instance | BindingFlags.NonPublic)!;
                var generic = helper.MakeGenericMethod(resultType);
                return generic.Invoke(this, new object?[] { task, method, target, args, state })!;
            }

            return InterceptVoidTask(task, method, target, args, state);
        }

        /// <summary>
        /// Awaits a Task and invokes exit hooks on completion or failure.
        /// </summary>
        private async Task InterceptVoidTask(Task task, MethodInfo method, object target, object?[]? args, object? state)
        {
            try
            {
                await task.ConfigureAwait(false);
                Interceptor.OnExit(method, target, args, null, null, state);
            }
            catch (Exception ex)
            {
                Interceptor.OnExit(method, target, args, null, ex, state);
                throw;
            }
        }

        /// <summary>
        /// Awaits a Task&lt;TResult&gt; and invokes exit hooks on completion or failure.
        /// </summary>
        private async Task<TResult> InterceptGenericTask<TResult>(Task<TResult> task, MethodInfo method, object target, object?[]? args, object? state)
        {
            try
            {
                var result = await task.ConfigureAwait(false);
                Interceptor.OnExit(method, target, args, result, null, state);
                return result;
            }
            catch (Exception ex)
            {
                Interceptor.OnExit(method, target, args, null, ex, state);
                throw;
            }
        }
    }
}
