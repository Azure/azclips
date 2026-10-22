// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.ParameterSets;

/// <summary>
/// Holds the parameter-set definitions for a command and provides the core utilities:
/// <list type="bullet">
/// <item><see cref="Validate"/> — proves the definitions are mutually exclusive, so no input can ever
/// satisfy two sets at once.</item>
/// <item><see cref="Resolve"/> / <see cref="TryResolve"/> — map a command's supplied parameters to the
/// single parameter set they correspond to, with an actionable message when none or several match.</item>
/// </list>
/// </summary>
/// <remarks>
/// Define the sets once (typically a <c>static readonly</c> field), call <see cref="Validate"/> in a
/// test to guarantee the definition is well-formed, then call <see cref="Resolve"/> per invocation.
/// </remarks>
/// <typeparam name="TCommand">The command type whose properties back the parameters.</typeparam>
internal sealed class ParameterSetResolver<TCommand>
{
    private readonly List<ParameterSet<TCommand>> _sets = new();

    /// <summary>The defined parameter sets, in declaration order.</summary>
    public IReadOnlyList<ParameterSet<TCommand>> Sets => _sets;

    /// <summary>Defines a new parameter set and returns it for fluent configuration.</summary>
    /// <param name="name">A unique, user-meaningful set name (for example, "ServicePrincipal").</param>
    public ParameterSet<TCommand> DefineSet(string name)
    {
        if (_sets.Any(s => string.Equals(s.Name, name, StringComparison.Ordinal)))
        {
            throw new InvalidParameterSetDefinitionException($"Duplicate parameter set name '{name}'.");
        }

        var set = new ParameterSet<TCommand>(name);
        _sets.Add(set);
        return set;
    }

    /// <summary>
    /// Verifies that every pair of parameter sets is mutually exclusive: no combination of supplied
    /// parameters could ever satisfy two sets at once.
    /// </summary>
    /// <remarks>
    /// Two sets <c>A</c> and <c>B</c> can both match some input exactly when every mandatory parameter
    /// of <c>A</c> is allowed in <c>B</c> <b>and</b> every mandatory parameter of <c>B</c> is allowed in
    /// <c>A</c>. This method requires the negation for every pair: at least one of the two sets must own
    /// a mandatory parameter the other does not allow (a discriminator). It is enough for one side to
    /// discriminate — for example a mandatory switch present in only one set keeps the pair exclusive
    /// even though the other set's mandatory parameters are a subset. That condition is both necessary
    /// and sufficient, so a passing definition is provably unambiguous.
    /// </remarks>
    /// <remarks>
    /// Defining no parameter sets is allowed: a command may impose no parameter-set constraints, in
    /// which case validation is a no-op.
    /// </remarks>
    /// <exception cref="InvalidParameterSetDefinitionException">A set has no mandatory parameter, or a
    /// pair of sets is non-exclusive.</exception>
    public void Validate()
    {
        foreach (ParameterSet<TCommand> set in _sets)
        {
            if (set.Mandatory.Count == 0)
            {
                throw new InvalidParameterSetDefinitionException(
                    $"Parameter set '{set.Name}' has no mandatory parameter, so it can never be distinguished from another set.");
            }
        }

        for (int i = 0; i < _sets.Count; i++)
        {
            for (int j = i + 1; j < _sets.Count; j++)
            {
                ParameterSet<TCommand> a = _sets[i];
                ParameterSet<TCommand> b = _sets[j];

                bool aHasDiscriminator = a.Mandatory.Any(p => !b.AllowedNames.Contains(p.Name));
                bool bHasDiscriminator = b.Mandatory.Any(p => !a.AllowedNames.Contains(p.Name));

                // Ambiguous only when NEITHER set has a mandatory parameter the other disallows.
                if (!aHasDiscriminator && !bHasDiscriminator)
                {
                    throw new InvalidParameterSetDefinitionException(
                        $"Parameter sets '{a.Name}' and '{b.Name}' are not mutually exclusive: " +
                        $"neither declares a mandatory parameter the other disallows, so some input satisfies both. " +
                        $"Add a distinguishing mandatory parameter to at least one of them. " +
                        $"'{a.Name}' mandatory=[{Names(a.Mandatory)}] allowed=[{string.Join(", ", a.AllowedNames)}]; " +
                        $"'{b.Name}' mandatory=[{Names(b.Mandatory)}] allowed=[{string.Join(", ", b.AllowedNames)}].");
                }
            }
        }
    }

    /// <summary>
    /// Determines which single parameter set the command's currently-supplied parameters correspond to.
    /// </summary>
    /// <exception cref="ParameterSetResolutionException">Zero or multiple sets match. The message lists
    /// each candidate set with the precise reason it did not apply, or names the colliding sets.</exception>
    /// <param name="command">The command instance to inspect.</param>
    /// <param name="formatName">Optional formatter applied to parameter names in error messages, for
    /// example to render a property name as the flag the user typed. Defaults to the raw name.</param>
    public ParameterSet<TCommand> Resolve(TCommand command, Func<string, string>? formatName = null)
    {
        if (!TryResolve(command, out ParameterSet<TCommand>? resolved, out string? error, formatName))
        {
            throw new ParameterSetResolutionException(error);
        }

        return resolved;
    }

    /// <summary>
    /// Attempts to resolve the active parameter set without throwing.
    /// </summary>
    /// <param name="command">The command instance to inspect.</param>
    /// <param name="resolved">The single matching set, when exactly one matches.</param>
    /// <param name="error">An actionable message describing why resolution failed, otherwise <c>null</c>.</param>
    /// <param name="formatName">Optional formatter applied to parameter names in the error message.</param>
    /// <returns><c>true</c> when exactly one set matches; otherwise <c>false</c>.</returns>
    public bool TryResolve(
        TCommand command,
        [NotNullWhen(true)] out ParameterSet<TCommand>? resolved,
        [NotNullWhen(false)] out string? error,
        Func<string, string>? formatName = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        Func<string, string> fmt = formatName ?? (n => n);
        string List(IEnumerable<string> names) => string.Join(", ", names.Select(fmt));

        // The universe of referenced parameters, and which of them the user supplied.
        var supplied = new HashSet<string>(StringComparer.Ordinal);
        foreach (ParameterSet<TCommand> set in _sets)
        {
            foreach (ParameterRef<TCommand> p in set.Mandatory.Concat(set.Optional))
            {
                if (p.IsSupplied(command))
                {
                    supplied.Add(p.Name);
                }
            }
        }

        var matches = new List<ParameterSet<TCommand>>();
        var diagnostics = new List<string>();

        foreach (ParameterSet<TCommand> set in _sets)
        {
            string[] missing = set.Mandatory
                .Where(p => !p.IsSupplied(command))
                .Select(p => p.Name)
                .ToArray();

            string[] notAllowed = supplied
                .Where(name => !set.AllowedNames.Contains(name))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            if (missing.Length == 0 && notAllowed.Length == 0)
            {
                matches.Add(set);
                continue;
            }

            var reasons = new List<string>();
            if (missing.Length > 0)
            {
                reasons.Add($"missing mandatory [{List(missing)}]");
            }

            if (notAllowed.Length > 0)
            {
                reasons.Add($"does not allow [{List(notAllowed)}]");
            }

            diagnostics.Add($"  - '{set.Name}': {string.Join("; ", reasons)}");
        }

        if (matches.Count == 1)
        {
            resolved = matches[0];
            error = null;
            return true;
        }

        resolved = null;
        string suppliedText = supplied.Count == 0
            ? "(none)"
            : List(supplied.OrderBy(n => n, StringComparer.Ordinal));

        if (matches.Count == 0)
        {
            error =
                $"The supplied parameters [{suppliedText}] do not match any parameter set.{Environment.NewLine}" +
                $"Candidate sets:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics)}";
            return false;
        }

        string matchNames = string.Join(", ", matches.Select(m => $"'{m.Name}'"));
        error =
            $"The supplied parameters [{suppliedText}] are ambiguous; they match multiple parameter sets: " +
            $"{matchNames}. Supply a parameter unique to the intended set.";
        return false;
    }

    private static string Names(IEnumerable<ParameterRef<TCommand>> refs) =>
        string.Join(", ", refs.Select(r => r.Name));
}
