using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Ace.Generator;

internal static class EmitterExtensions
{
    /// <summary>
    /// Attach an <em>implementation</em> block (raw C# statements) to a structurally-built method
    /// signature. The body may be given with or without the enclosing braces. This is the house
    /// style for the generators: the <em>signature</em> is composed with <see cref="SyntaxFactory"/>
    /// (typed, validated — a safeguard for contributors); only the opaque <em>implementation</em> is
    /// authored as a code block. <c>NormalizeWhitespace()</c> owns the final layout.
    /// </summary>
    internal static TMethod WithRawBody<TMethod>(this TMethod method, string body)
        where TMethod : BaseMethodDeclarationSyntax
    {
        string braced = body.TrimStart().StartsWith('{') ? body : "{" + body + "}";
        return (TMethod)method.WithBody((BlockSyntax)ParseStatement(braced));
    }

    /// <summary>
    /// Append verbatim comment trivia (parsed from <paramref name="commentText"/> — line <c>//</c>,
    /// block <c>/* */</c>, or doc <c>///</c>) after the node's existing leading trivia, so it lands
    /// directly above the declaration (below any XML doc already attached). Used to carry
    /// hand-authored comments from a reference source into generated output.
    /// </summary>
    internal static T WithLeadingComment<T>(this T node, string commentText)
        where T : SyntaxNode =>
        node.WithLeadingTrivia(node.GetLeadingTrivia().AddRange(ParseLeadingTrivia(commentText)));

    /// <summary>
    /// Attach a single-line XML <c>&lt;summary&gt;</c> doc comment as leading trivia,
    /// if <paramref name="doc"/> is non-empty. Applied last in each builder so it lands
    /// on the declaration's first token (above any attribute list).
    /// </summary>
    internal static T WithLeadingXmlDoc<T>(this T node, string? doc)
        where T : SyntaxNode
    {
        if (string.IsNullOrWhiteSpace(doc))
        {
            return node;
        }

        string oneLine = doc.Replace("\r", " ").Replace("\n", " ").Trim();

        // DocumentationComment/XmlSummaryElement build the structured `///` trivia
        // (exterior markers, `<summary>` element) so it survives NormalizeWhitespace;
        // XmlText escapes `<`, `>`, `&` for us. A leading space yields the conventional
        // `/// <summary>` (the exterior alone renders `///<summary>`); the trailing
        // newline terminates the comment.
        DocumentationCommentTriviaSyntax comment = DocumentationComment(
            XmlText(" "),
            XmlSummaryElement(XmlText(oneLine)),
            XmlText(XmlTextNewLine("\n", continueXmlDocumentationComment: false)));

        return node.WithLeadingTrivia(TriviaList(Trivia(comment)));
    }

    /// <summary>
    /// Normalize <paramref name="path"/> to a clean, <c>/</c>-separated relative
    /// path. Fails (with <paramref name="error"/> set) if the path is empty,
    /// absolute, or escapes the base directory via <c>..</c>.
    /// </summary>
    internal static bool TryNormalize(this string path, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "the path is empty";
            return false;
        }

        string unified = path.Replace('\\', '/');
        if (unified.StartsWith('/') || System.IO.Path.IsPathRooted(unified))
        {
            error = $"'{path}' is absolute; output paths must be relative to the base directory";
            return false;
        }

        var segments = new List<string>();
        foreach (string segment in unified.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                error = $"'{path}' escapes the base directory ('..' is not allowed)";
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            error = $"'{path}' resolves to an empty relative path";
            return false;
        }

        normalized = string.Join('/', segments);
        return true;
    }

    private static readonly char[] Separators = ['-', '_', '.', ' ', '/'];

    /// <summary>
    /// PascalCase a value, splitting on separators
    /// (e.g. <c>restoreVolume</c> → <c>RestoreVolume</c>, <c>x-ms-force</c> → <c>XMsForce</c>).
    /// </summary>
    internal static string Pascal(this string value)
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
    internal static string Identifier(this string value)
    {
        string pascal = value.Pascal();
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