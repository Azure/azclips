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
    /// Canonical string form, suitable for dictionary keys. Mirrors the TypeScript
    /// <c>serializeId</c>: an interface scope uses <c>::</c> so it cannot be confused with a
    /// namespace segment. Not safe as a filesystem path.
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
}
