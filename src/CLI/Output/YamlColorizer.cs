// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Tokens;

namespace Core.Cli.Output;

/// <summary>
/// Applies ANSI syntax-highlighting to already-serialized YAML text for terminal display.
/// Colorization is purely cosmetic — callers that need the raw YAML (e.g. for piping/parsing)
/// must gate this on <c>Core.Runtime.ColorPolicy.ShouldEmitColor</c> before calling it.
/// </summary>
/// <remarks>
/// Walks the input with <see cref="Scanner"/> (rather than regex), mirroring <see
/// cref="JsonColorizer"/>'s <see cref="System.Text.Json.Utf8JsonReader"/>-based approach.
/// YAML's scanner has no dedicated "this scalar is a mapping key" token type the way JSON's
/// reader has <see cref="System.Text.Json.JsonTokenType.PropertyName"/> — instead a <see
/// cref="Key"/> marker token precedes a key scalar and a <see cref="Value"/> marker precedes a
/// value scalar, so this colorizer tracks which marker it last saw. Block-sequence items (e.g.
/// <c>- alpha</c>) have neither marker; those scalars are treated as values and colored by
/// their inferred type, matching how JSON colors array elements.
/// </remarks>
internal static class YamlColorizer
{
    internal static string Colorize(string yaml)
    {
        using var reader = new StringReader(yaml);
        var scanner = new Scanner(reader, skipComments: false);
        var sb = new StringBuilder(yaml.Length + 64);
        var copiedTo = 0L;
        var nextScalarIsKey = false;

        while (scanner.MoveNext())
        {
            switch (scanner.Current)
            {
                case Key:
                    nextScalarIsKey = true;
                    continue;
                case Value:
                    // A mapping can omit an explicit key scalar (e.g. `? : value`); Value always
                    // means "what follows is a value", so it takes precedence over a stale Key.
                    nextScalarIsKey = false;
                    continue;
                case Scalar scalar:
                    var color = nextScalarIsKey ? JsonColorizer.KeyColor : ColorFor(scalar);
                    nextScalarIsKey = false;
                    AppendColored(sb, yaml, ref copiedTo, scalar.Start.Index, scalar.End.Index, color);
                    break;
            }
        }

        AppendSlice(sb, yaml, copiedTo, yaml.Length);
        return sb.ToString();
    }

    // Quoted scalars are always strings regardless of content (e.g. a YAML-quoted "42" is a
    // string, not a number); only plain (unquoted) scalars get type-inferred.
    private static string ColorFor(Scalar scalar)
    {
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonColorizer.StringColor;
        }

        if (scalar.Value.Length == 0
            || string.Equals(scalar.Value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scalar.Value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scalar.Value, "null", StringComparison.OrdinalIgnoreCase)
            || scalar.Value == "~")
        {
            return JsonColorizer.LiteralColor;
        }

        return double.TryParse(scalar.Value, System.Globalization.CultureInfo.InvariantCulture, out _)
            ? JsonColorizer.NumberColor
            : JsonColorizer.StringColor;
    }

    private static void AppendColored(StringBuilder sb, string yaml, ref long copiedTo, long start, long end, string color)
    {
        if (end <= start)
        {
            return;
        }
        AppendSlice(sb, yaml, copiedTo, start);
        sb.Append(color);
        AppendSlice(sb, yaml, start, end);
        sb.Append(JsonColorizer.Reset);
        copiedTo = end;
    }

    private static void AppendSlice(StringBuilder sb, string yaml, long start, long end)
    {
        if (end <= start)
        {
            return;
        }
        sb.Append(yaml, (int)start, (int)(end - start));
    }
}
