// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq.Expressions;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.ParameterSets;

/// <summary>
/// A named group of parameters that are meant to be used together. All <see cref="Mandatory"/>
/// parameters must be supplied for the set to apply; <see cref="Optional"/> parameters may be
/// supplied. Supplying any parameter that belongs to neither group disqualifies the set.
/// </summary>
/// <typeparam name="TCommand">The command type whose properties back the parameters.</typeparam>
internal sealed class ParameterSet<TCommand>
{
    private readonly List<ParameterRef<TCommand>> _mandatory = new();
    private readonly List<ParameterRef<TCommand>> _optional = new();

    internal ParameterSet(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Parameter set name must be non-empty.", nameof(name));
        }

        Name = name;
    }

    /// <summary>The unique name of this parameter set.</summary>
    public string Name { get; }

    /// <summary>Parameters that must all be supplied for this set to apply.</summary>
    public IReadOnlyList<ParameterRef<TCommand>> Mandatory => _mandatory;

    /// <summary>Parameters that may optionally be supplied within this set.</summary>
    public IReadOnlyList<ParameterRef<TCommand>> Optional => _optional;

    /// <summary>All parameter names allowed in this set (mandatory and optional).</summary>
    public IReadOnlySet<string> AllowedNames =>
        _mandatory.Concat(_optional).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Declares a parameter that must be supplied for this set to apply. The default "supplied" rule
    /// (<see cref="ParameterRef{TCommand}.DefaultIsSupplied"/>) treats <c>null</c>, empty
    /// strings, and <c>false</c> as not supplied.
    /// </summary>
    /// <param name="selector">A simple property access, for example <c>c =&gt; c.ClientId</c>.</param>
    public ParameterSet<TCommand> WithMandatory<TValue>(Expression<Func<TCommand, TValue>> selector)
        => Add(_mandatory, selector, isSupplied: null);

    /// <summary>
    /// Declares a mandatory parameter with a custom rule for when it counts as supplied.
    /// </summary>
    /// <param name="selector">A simple property access, for example <c>c =&gt; c.Count</c>.</param>
    /// <param name="isSupplied">Returns <c>true</c> when the value should be treated as supplied.</param>
    public ParameterSet<TCommand> WithMandatory<TValue>(
        Expression<Func<TCommand, TValue>> selector,
        Func<TValue, bool> isSupplied)
        => Add(_mandatory, selector, isSupplied ?? throw new ArgumentNullException(nameof(isSupplied)));

    /// <summary>Declares a parameter that may optionally be supplied within this set.</summary>
    /// <param name="selector">A simple property access, for example <c>c =&gt; c.Endpoint</c>.</param>
    public ParameterSet<TCommand> WithOptional<TValue>(Expression<Func<TCommand, TValue>> selector)
        => Add(_optional, selector, isSupplied: null);

    /// <summary>Declares an optional parameter with a custom rule for when it counts as supplied.</summary>
    /// <param name="selector">A simple property access, for example <c>c =&gt; c.Endpoint</c>.</param>
    /// <param name="isSupplied">Returns <c>true</c> when the value should be treated as supplied.</param>
    public ParameterSet<TCommand> WithOptional<TValue>(
        Expression<Func<TCommand, TValue>> selector,
        Func<TValue, bool> isSupplied)
        => Add(_optional, selector, isSupplied ?? throw new ArgumentNullException(nameof(isSupplied)));

    private ParameterSet<TCommand> Add<TValue>(
        List<ParameterRef<TCommand>> target,
        Expression<Func<TCommand, TValue>> selector,
        Func<TValue, bool>? isSupplied)
    {
        ArgumentNullException.ThrowIfNull(selector);

        string name = GetPropertyName(selector);
        if (AllowedNames.Contains(name))
        {
            throw new InvalidParameterSetDefinitionException(
                $"Parameter '{name}' is declared more than once in parameter set '{Name}'.");
        }

        Func<TCommand, TValue> compiled = selector.Compile();
        Func<object?, bool> supplied = isSupplied is null
            ? ParameterRef<TCommand>.DefaultIsSupplied
            : value => isSupplied((TValue)value!);

        target.Add(new ParameterRef<TCommand>(name, c => compiled(c), supplied));
        return this;
    }

    private static string GetPropertyName<TValue>(Expression<Func<TCommand, TValue>> selector)
    {
        // Unwrap a boxing conversion the compiler may insert, for example (object)command.Flag.
        Expression body = selector.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression member)
        {
            return member.Member.Name;
        }

        throw new ArgumentException(
            "Parameter selector must be a simple property access, for example c => c.ClientId.",
            nameof(selector));
    }
}
