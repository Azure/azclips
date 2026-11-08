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
    /// Undo <c>NormalizeWhitespace()</c>'s spacing of the null-forgiving operator: it renders
    /// <c>null!</c> as <c>null !</c>. Valid C#, but not how anyone writes it. Apply after normalizing.
    /// </summary>
    internal static T WithTightNullForgiving<T>(this T node)
        where T : SyntaxNode =>
        node.ReplaceNodes(
            node.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>()
                .Where(expression => expression.IsKind(SyntaxKind.SuppressNullableWarningExpression)),
            (_, rewritten) => rewritten
                .WithOperand(rewritten.Operand.WithoutTrailingTrivia())
                .WithOperatorToken(rewritten.OperatorToken.WithLeadingTrivia()));

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
    /// Attach an XML doc comment as leading trivia: a single-line <c>&lt;summary&gt;</c> when
    /// <paramref name="doc"/> is non-empty, then <paramref name="remarks"/> when given. Applied
    /// last in each builder so it lands on the declaration's first token (above any attribute list).
    /// </summary>
    internal static T WithLeadingXmlDoc<T>(this T node, string? doc, XmlDocRemarks? remarks = null)
        where T : SyntaxNode
    {
        var elements = new List<XmlNodeSyntax>();
        if (!string.IsNullOrWhiteSpace(doc))
        {
            elements.Add(XmlSummaryElement(XmlText(OneLine(doc))));
        }

        if (remarks is not null)
        {
            elements.Add(XmlRemarksElement(List(RemarksContent(remarks))));
        }

        if (elements.Count == 0)
        {
            return node;
        }

        // DocumentationComment and the Xml* factories build the structured `///` trivia
        // (exterior markers, elements) so it survives NormalizeWhitespace; XmlText escapes
        // `<`, `>`, `&` for us. The first line's exterior comes with the comment; every element
        // after it opens a line of its own. Each line starts with a space, which yields the
        // conventional `/// <summary>` (the exterior alone renders `///<summary>`); the trailing
        // newline terminates the comment.
        DocumentationCommentTriviaSyntax comment = DocumentationComment(
        [
            XmlText(" "),
            elements[0],
            .. elements.Skip(1).SelectMany(element => DocLine(element)),
            XmlText(XmlTextNewLine("\n", continueXmlDocumentationComment: false)),
        ]);

        return node.WithLeadingTrivia(TriviaList(Trivia(comment)));
    }

    /// <summary>
    /// What goes inside <c>&lt;remarks&gt;</c>: the text alone stays on the tag's line; with
    /// bullets, the text, a <c>&lt;list type="bullet"&gt;</c>, and the tail each take a line.
    /// </summary>
    private static IEnumerable<XmlNodeSyntax> RemarksContent(XmlDocRemarks remarks)
    {
        if (remarks.Bullets is not { Count: > 0 } bullets)
        {
            return [XmlText(OneLine(remarks.Text))];
        }

        XmlElementSyntax list = XmlElement(
            XmlElementStartTag(XmlName("list"), SingletonList<XmlAttributeSyntax>(XmlTextAttribute("type", "bullet"))),
            List<XmlNodeSyntax>(
            [
                .. bullets.SelectMany(bullet => DocLine(
                    XmlElement("item", SingletonList<XmlNodeSyntax>(
                        XmlElement("description", SingletonList<XmlNodeSyntax>(XmlText(OneLine(bullet)))))))),
                .. DocLine(),
            ]),
            XmlElementEndTag(XmlName("list")));

        return
        [
            .. DocLine(XmlText(OneLine(remarks.Text))),
            .. DocLine(list),
            .. remarks.Tail is { } tail ? DocLine(XmlText(OneLine(tail))) : [],
            .. DocLine(),
        ];
    }

    /// <summary>
    /// A new <c>///</c> line holding <paramref name="content"/>; with none, it only indents what
    /// follows, such as a closing tag.
    /// </summary>
    private static XmlNodeSyntax[] DocLine(XmlNodeSyntax? content = null) =>
        content is null ? [XmlNewLine("\n"), XmlText(" ")] : [XmlNewLine("\n"), XmlText(" "), content];

    /// <summary>Doc text on one line: XML doc collapses line breaks anyway, and a summary reads as one sentence.</summary>
    private static string OneLine(string text) => text.Replace("\r", " ").Replace("\n", " ").Trim();

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