namespace Ace.Generator;

/// <summary>
/// A <c>&lt;remarks&gt;</c> block for <see cref="EmitterExtensions.WithLeadingXmlDoc{T}"/>: one line of
/// text, optionally followed by a bulleted list and a closing line.
/// </summary>
/// <param name="Text">The opening text, and the whole block when there are no <paramref name="Bullets"/>.</param>
/// <param name="Bullets">One <c>&lt;item&gt;</c> each, listed under the text.</param>
/// <param name="Tail">A line after the list.</param>
internal sealed record XmlDocRemarks(string Text, IReadOnlyList<string>? Bullets = null, string? Tail = null);
