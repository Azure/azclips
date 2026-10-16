using System.Text;
using Ace.Generator.Model.Ir;

namespace Ace.Generator.Generators;

/// <summary>
/// Structural identity for <see cref="IRId"/>. Ids are computed at emit time from the
/// declaration site's original names and are invariant under renaming transforms, so they
/// are the stable way to compare and index IR nodes — never the display name.
/// </summary>
internal static class IrIds
{
    /// <summary>Structural equality, including namespace segments and recursive type arguments.</summary>
    public static bool Equal(IRId? left, IRId? right)
    {
        if (left is null || right is null ||
            !string.Equals(left.Module, right.Module, StringComparison.Ordinal) ||
            !string.Equals(left.Interface, right.Interface, StringComparison.Ordinal) ||
            !string.Equals(left.Name, right.Name, StringComparison.Ordinal) ||
            !left.Namespace.SequenceEqual(right.Namespace, StringComparer.Ordinal))
        {
            return false;
        }

        if (left.TypeArgs is null || right.TypeArgs is null)
        {
            return left.TypeArgs is null && right.TypeArgs is null;
        }

        return left.TypeArgs.Count == right.TypeArgs.Count &&
            left.TypeArgs.Zip(right.TypeArgs).All(pair => Equal(pair.First, pair.Second));
    }

    /// <summary>
    /// Canonical string form, suitable for display. **Not** a lookup key: namespace segments
    /// are joined with <c>.</c>, so <c>["A","B"]</c> and <c>["A.B"]</c> render identically even
    /// though <see cref="Equal"/> distinguishes them. Use <see cref="Key"/> to index. Mirrors
    /// the TypeScript <c>serializeId</c>. Not safe as a filesystem path.
    /// </summary>
    public static string Serialize(IRId id)
    {
        var scope = new StringBuilder(string.Join(".", id.Namespace));
        if (!string.IsNullOrEmpty(id.Interface))
        {
            scope.Append("::").Append(id.Interface);
        }

        var text = new StringBuilder("[").Append(id.Module).Append("] ");
        if (scope.Length > 0)
        {
            text.Append(scope).Append('.');
        }

        text.Append(id.Name);
        if (id.TypeArgs is { Count: > 0 } typeArgs)
        {
            text.Append('<').Append(string.Join(", ", typeArgs.Select(Serialize))).Append('>');
        }

        return text.ToString();
    }

    /// <summary>
    /// Key for identity-based lookup tables, agreeing with <see cref="Equal"/> exactly.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="Serialize"/>: that joins namespace segments with <c>.</c>
    /// for display, so <c>["A","B"]</c> and <c>["A.B"]</c> produce the same string even though
    /// <see cref="Equal"/> treats them as different ids. Every part is length- or
    /// delimiter-framed here so two ids share a key only when they are structurally equal.
    /// </remarks>
    public static string Key(IRId id)
    {
        var text = new StringBuilder();
        WriteKey(text, id);
        return text.ToString();
    }

    private static void WriteKey(StringBuilder text, IRId id)
    {
        IReadOnlyList<string> segments = id.Namespace ?? [];
        text.Append('(').Append(Escape(id.Module ?? string.Empty)).Append('|').Append(segments.Count);
        foreach (string segment in segments)
        {
            text.Append(':').Append(Escape(segment));
        }

        // Interface is non-empty when present, so `~` is unambiguously "absent".
        text.Append('|').Append(id.Interface is null ? "~" : Escape(id.Interface));
        text.Append('|').Append(Escape(id.Name ?? string.Empty)).Append('|');

        // An absent TypeArgs is not the same id as an empty one, matching Equal.
        if (id.TypeArgs is null)
        {
            text.Append('~');
        }
        else
        {
            text.Append(id.TypeArgs.Count);
            foreach (IRId arg in id.TypeArgs)
            {
                WriteKey(text, arg);
            }
        }

        text.Append(')');
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace(":", "\\:", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal);
}
