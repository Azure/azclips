using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Ace.Generator;

/// <summary>C# identifier helpers shared by the artifact generators.</summary>
internal static class Naming
{
    private static readonly char[] Separators = ['-', '_', '.', ' ', '/'];

    /// <summary>
    /// PascalCase a value, splitting on separators
    /// (e.g. <c>restoreVolume</c> → <c>RestoreVolume</c>, <c>x-ms-force</c> → <c>XMsForce</c>).
    /// </summary>
    public static string Pascal(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "_";
        }

        string[] parts = value.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder(value.Length);
        foreach (string part in parts)
        {
            sb.Append(char.ToUpperInvariant(part[0])).Append(part, 1, part.Length - 1);
        }

        return sb.Length == 0 ? "_" : sb.ToString();
    }

    /// <summary>
    /// Turn an arbitrary string into a safe C# identifier: PascalCase, replace
    /// illegal characters, avoid leading digits and reserved keywords.
    /// </summary>
    public static string Identifier(string value)
    {
        string pascal = Pascal(value);
        var sb = new StringBuilder(pascal.Length);
        foreach (char ch in pascal)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        string result = sb.Length == 0 ? "_" : sb.ToString();
        if (!char.IsLetter(result[0]) && result[0] != '_')
        {
            result = "_" + result;
        }

        return SyntaxFacts.GetKeywordKind(result) != SyntaxKind.None ? "@" + result : result;
    }
}
