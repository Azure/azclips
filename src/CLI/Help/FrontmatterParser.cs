// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Cli.Help;

/// <summary>
/// File-level frontmatter for a help markdown document (v1.0.0 schema).
/// </summary>
public sealed record FrontmatterDoc(
    string Summary,
    string Status,
    string Schema,
    string OnlineVersion,
    IReadOnlyList<string> RelatedLinks)
{
    public static readonly FrontmatterDoc Empty = new(string.Empty, string.Empty, string.Empty, string.Empty, []);
}

/// <summary>
/// Hand-rolled parser for the YAML-subset frontmatter at the top of a help file:
/// <c>---</c>-delimited block, flat <c>key: value</c> pairs, and one list key
/// (<c>relatedLinks:</c>). Lenient at runtime; strict checks live in <c>MarkdownStructureValidator</c>.
/// </summary>
public static class FrontmatterParser
{
    /// <summary>Parses the frontmatter block at the top of <paramref name="content"/> and returns the doc + remaining body.</summary>
    public static (FrontmatterDoc Doc, string Body) Parse(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return (FrontmatterDoc.Empty, string.Empty);
        }

        var lines = content.Split('\n');
        var firstNonEmpty = FindFirstNonEmpty(lines);
        if (firstNonEmpty < 0 || TrimEol(lines[firstNonEmpty]) != "---")
        {
            return (FrontmatterDoc.Empty, content);
        }

        // Locate the closing '---'.
        var closeIdx = -1;
        for (var i = firstNonEmpty + 1; i < lines.Length; i++)
        {
            if (TrimEol(lines[i]) == "---")
            {
                closeIdx = i;
                break;
            }
        }

        if (closeIdx < 0)
        {
            // Unterminated frontmatter — return empty doc and original body to fail gracefully.
            return (FrontmatterDoc.Empty, content);
        }

        var summary = string.Empty;
        var status = string.Empty;
        var schema = string.Empty;
        var onlineVersion = string.Empty;
        List<string>? relatedLinks = null;
        var inRelatedLinks = false;

        for (var i = firstNonEmpty + 1; i < closeIdx; i++)
        {
            var raw = TrimEol(lines[i]);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            // List item under the active list key.
            var trimmed = raw.TrimStart();
            if (inRelatedLinks && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                relatedLinks ??= [];
                relatedLinks.Add(trimmed[2..].Trim());
                continue;
            }

            inRelatedLinks = false;

            var colon = raw.IndexOf(':');
            if (colon < 0)
            {
                continue;
            }

            var key = raw[..colon].Trim();
            var value = raw[(colon + 1)..].Trim();

            switch (key.ToLowerInvariant())
            {
                case "summary":
                    summary = value;
                    break;
                case "status":
                    status = value;
                    break;
                case "schema":
                    schema = value;
                    break;
                case "online version":
                    onlineVersion = value;
                    break;
                case "relatedlinks":
                    inRelatedLinks = true;
                    relatedLinks ??= [];
                    break;
            }
        }

        // Default status to "ga" when absent.
        var statusOrDefault = string.IsNullOrEmpty(status) ? "ga" : status;

        // Re-assemble body (everything after the closing '---').
        var bodyStart = closeIdx + 1;
        var body = bodyStart >= lines.Length
            ? string.Empty
            : string.Join('\n', lines, bodyStart, lines.Length - bodyStart);

        // Trim a single leading blank line for tidiness.
        if (body.StartsWith('\n'))
        {
            body = body[1..];
        }
        else if (body.StartsWith("\r\n", StringComparison.Ordinal))
        {
            body = body[2..];
        }

        return (
            new FrontmatterDoc(
                summary,
                statusOrDefault,
                schema,
                onlineVersion,
                (IReadOnlyList<string>?)relatedLinks ?? []),
            body);
    }

    /// <summary>
    /// The set of values <c>status:</c> may take. Validators use this to flag typos.
    /// </summary>
    public static readonly IReadOnlyCollection<string> ValidStatuses = new[] { "ga", "preview", "deprecated", "internal" };

    private static int FindFirstNonEmpty(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string TrimEol(string line)
        => line.EndsWith('\r') ? line[..^1] : line;
}
