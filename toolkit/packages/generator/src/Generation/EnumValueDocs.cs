using System.Globalization;
using System.Text.Json;
using Ace.Generator.Generators;
using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;

namespace Ace.Generator;

/// <summary>
/// The <c>&lt;remarks&gt;</c> that lists an enum's values on each member typed by it. No enum
/// types are emitted (<see cref="IrTypeMapper"/> renders an enum as its value type), so this is
/// where the generated code still shows what the spec allows.
/// </summary>
/// <remarks>
/// <para>
/// A member is typed by an enum directly, or as the item or dictionary value of a collection.
/// A fixed enum lists its "allowed" values; an extensible one its "known" values, because the
/// service accepts others too. The enum is named as the document names it.
/// </para>
/// <para>
/// A value renders as a C# literal: a string quoted, a number bare. A member's name follows its
/// value only when the two differ beyond case and punctuation (<c>RSA_HSM</c> adds nothing to
/// <c>"RSA-HSM"</c>), and its doc only when it says more than the value or the name; specs often
/// restate the value as its doc. An enum the document does not declare has no values to list.
/// </para>
/// </remarks>
internal static class EnumValueDocs
{
    /// <summary>
    /// The most values listed on one line. More, or any member with a name or doc of its own,
    /// turn the line into a bulleted list.
    /// </summary>
    private const int InlineLimit = 10;

    /// <summary>
    /// The most values a list shows; the rest are only counted. Every member typed by an enum
    /// repeats its list, and an enum can run to hundreds of values (VM sizes, for one).
    /// </summary>
    private const int ListLimit = 20;

    /// <summary>The values of the enum behind <paramref name="property"/>, if one is.</summary>
    public static XmlDocRemarks? For(IRProperty property, IrIndex index) => property switch
    {
        IRPropertyEnum p => For(p.Type, index),
        IRPropertyCollection p => For(p.Type, index),
        // A dictionary arrives as an object property.
        IRPropertyObject p => For(p.Type, index),
        _ => null,
    };

    /// <summary>The values of the enum behind <paramref name="type"/>, if one is.</summary>
    public static XmlDocRemarks? For(IRTypeRef? type, IrIndex index) => For(type, index, each: null);

    private static XmlDocRemarks? For(IRTypeRef? type, IrIndex index, string? each) => type switch
    {
        IRTypeRefArray array => For(array.Element, index, "item"),
        IRTypeRefDictionary dictionary => For(dictionary.Value, index, "dictionary value"),
        IRTypeRefEnum reference when index.Enum(reference.Id) is { Members.Count: > 0 } declared => Describe(declared, each),
        _ => null,
    };

    private static XmlDocRemarks Describe(IREnum declared, string? each)
    {
        string lead =
            $"{(declared.IsFixed ? "Allowed" : "Known")} values{(each is null ? "" : $" for each {each}")} " +
            $"({(declared.IsFixed ? "fixed" : "extensible")} enum {declared.Name})";
        Listed[] values = [.. declared.Members.Select(Listed.Of)];

        if (values.Length <= InlineLimit && values.All(value => value.Name is null && value.Doc is null))
        {
            return new XmlDocRemarks($"{lead}: {string.Join(", ", values.Select(value => value.Literal))}.");
        }

        int hidden = values.Length - ListLimit;
        return new XmlDocRemarks(
            lead + ":",
            [.. values.Take(ListLimit).Select(value => value.Render())],
            hidden > 0 ? $"...and {hidden} more." : null);
    }

    /// <summary>
    /// One member as listed: its value as a C# literal, plus its name and doc where they say more
    /// than the value does.
    /// </summary>
    private readonly record struct Listed(string Literal, string? Name, string? Doc)
    {
        public static Listed Of(IREnumMember member)
        {
            (string text, string literal) = Value(member.Value);
            string? doc = member.Docs ?? member.Summary;
            return new Listed(
                literal,
                SaysMore(member.Name, text) ? member.Name : null,
                !string.IsNullOrWhiteSpace(doc) && SaysMore(doc, text) && SaysMore(doc, member.Name) ? doc : null);
        }

        public string Render() => Literal + (Name is null ? "" : $" ({Name})") + (Doc is null ? "" : $": {Doc}");

        /// <summary>A value as the spec writes it, and as a C# literal.</summary>
        private static (string Text, string Literal) Value(object? value)
        {
            // Deserialized IR carries a JsonElement; a hand-built document carries the CLR value.
            string? text = value switch
            {
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                string s => s,
                _ => null,
            };
            if (text is not null)
            {
                return (text, SymbolDisplay.FormatLiteral(text, quote: true));
            }

            string bare = value switch
            {
                JsonElement element => element.GetRawText(),
                IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
                _ => $"{value}",
            };
            return (bare, bare);
        }

        /// <summary>True when <paramref name="text"/> still differs from <paramref name="other"/> ignoring case and punctuation.</summary>
        private static bool SaysMore(string text, string other) =>
            !string.Equals(LettersAndDigits(text), LettersAndDigits(other), StringComparison.Ordinal);

        private static string LettersAndDigits(string text) =>
            string.Concat(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant));
    }
}
