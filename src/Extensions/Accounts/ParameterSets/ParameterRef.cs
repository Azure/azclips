// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Accounts.ParameterSets;

/// <summary>
/// A single parameter within a command: its user-facing name plus a way to read the current value
/// from a command instance and decide whether the user actually supplied it.
/// </summary>
/// <remarks>
/// <para>
/// The model mirrors how processors are written: a command is a class, a parameter is a property, and
/// a parameter is "supplied" when the user passed a value. Because the host binds unsupplied arguments
/// to type defaults (<c>null</c>, <c>""</c>, <c>false</c>) rather than leaving them unset,
/// <see cref="DefaultIsSupplied"/> treats those defaults as "not supplied". Callers whose semantics
/// differ can pass an explicit predicate when declaring the parameter.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The command type whose properties back the parameters.</typeparam>
internal sealed class ParameterRef<TCommand>
{
    private readonly Func<TCommand, object?> _accessor;
    private readonly Func<object?, bool> _isSupplied;

    internal ParameterRef(string name, Func<TCommand, object?> accessor, Func<object?, bool> isSupplied)
    {
        Name = name;
        _accessor = accessor;
        _isSupplied = isSupplied;
    }

    /// <summary>The parameter name, typically the backing property name (for example, <c>nameof(ClientId)</c>).</summary>
    public string Name { get; }

    /// <summary>Whether the user supplied this parameter on the given command instance.</summary>
    public bool IsSupplied(TCommand command) => _isSupplied(_accessor(command));

    /// <summary>
    /// The default "supplied" rule, aligned with how the host binds absent arguments:
    /// <list type="bullet">
    /// <item><c>null</c> is never supplied.</item>
    /// <item>A string is supplied when it is non-empty, including whitespace. Parameter-specific
    /// validation and normalization must happen before resolution.</item>
    /// <item>A <see cref="bool"/> is supplied only when <c>true</c> (a switch the user turned on).</item>
    /// <item>Any other non-null value is supplied.</item>
    /// </list>
    /// </summary>
    public static bool DefaultIsSupplied(object? value) => value switch
    {
        null => false,
        string s => s.Length > 0,
        bool b => b,
        _ => true,
    };
}
