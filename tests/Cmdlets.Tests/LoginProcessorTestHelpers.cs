// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;

namespace Cmdlets.Tests;

internal static class LoginProcessorTestHelpers
{
    private static object? InvokeUnwrapped(this MethodInfo method, object target, params object?[] args)
    {
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw; // unreachable
        }
    }

    /// <summary>
    /// Initializes the parameter-set resolver (normally done by <c>Init</c>), validates individual
    /// values as <c>ProcessAsync</c> does, and resolves the active set for the processor's
    /// current arguments. Throws the processor's usage error when none/several match.
    /// </summary>
    internal static string ResolveParameterSet(LoginProcessor processor)
    {
        typeof(LoginProcessorBase)
            .GetMethod("ConfigureParameterSets", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(processor, null);

        typeof(LoginProcessor)
            .GetMethod("ValidateParameterValues", BindingFlags.NonPublic | BindingFlags.Instance)!
            .InvokeUnwrapped(processor);

        var parameterSet = (string)typeof(LoginProcessor)
            .GetMethod("ResolveParameterSet", BindingFlags.NonPublic | BindingFlags.Instance)!
            .InvokeUnwrapped(processor);

        typeof(LoginProcessor)
            .GetMethod("ValidateParameterSet", BindingFlags.NonPublic | BindingFlags.Instance)!
            .InvokeUnwrapped(processor, parameterSet);

        return parameterSet;
    }
}
