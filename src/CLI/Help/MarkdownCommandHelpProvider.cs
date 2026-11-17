// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Help;

/// <summary>
/// One example block parsed from a help markdown file. <see cref="IsPowerShell"/> is
/// driven by the fence language tag (<c>powershell</c>/<c>pwsh</c> → true); the
/// <c>(CLI)</c> / <c>(PowerShell)</c> suffix in the H3 title is cosmetic.
/// </summary>
public sealed record MarkdownExample(string Title, string Command, bool IsPowerShell);

/// <summary>
/// Parsed help markdown: frontmatter <c>summary</c>, body <c>Description:</c> line,
/// and ordered example fences. <see cref="Empty"/> is the no-markdown sentinel.
/// </summary>
public sealed record MarkdownCommandHelp(string Summary, string Description, IReadOnlyList<MarkdownExample> Examples)
{
    public static readonly MarkdownCommandHelp Empty = new(string.Empty, string.Empty, []);
}

/// <summary>
/// Reads and parses help markdown from the nested corpus at <c>src/CLI/Help/Docs/&lt;ext&gt;/</c>.
/// Layout: <c>&lt;ext&gt;/&lt;seg1&gt;/.../&lt;segN&gt;.md</c> for leaves,
/// <c>&lt;ext&gt;/&lt;seg1&gt;/.../&lt;segN&gt;/index.md</c> for groups. Lenient: malformed/missing
/// input returns <see cref="MarkdownCommandHelp.Empty"/> rather than throwing.
/// </summary>
public static class MarkdownCommandHelpProvider
{
    /// <summary>
    /// Loads help markdown for an extension + command key, or <see cref="MarkdownCommandHelp.Empty"/> if no file exists.
    /// </summary>
    public static MarkdownCommandHelp Get(string extensionName, string commandKey)
    {
        var filePath = ResolveHelpFilePath(extensionName, commandKey);
        if (filePath == null)
        {
            return MarkdownCommandHelp.Empty;
        }

        var content = File.ReadAllText(filePath);
        return Parse(content);
    }

    /// <summary>Parses raw markdown content. Lenient: malformed input returns empty/partial.</summary>
    public static MarkdownCommandHelp Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return MarkdownCommandHelp.Empty;
        }

        // Extract file-level frontmatter (§4 of help-system.md). Summary comes from
        // `summary:` in the frontmatter; description stays as a `Description:` body line.
        // If the file has no frontmatter block, FrontmatterParser returns an empty doc
        // and the original body — graceful degradation at runtime; strict validation
        // happens in the help tool.
        var (frontmatter, body) = FrontmatterParser.Parse(content);
        var summary = frontmatter.Summary;
        // Shared parser: keeps runtime view aligned with MarkdownStructureValidator.
        var description = MarkdownHelpParser.GetField(body, "Description");

        var examples = new List<MarkdownExample>();
        foreach (var parsed in MarkdownHelpParser.EnumerateExamples(body))
        {
            var isPowerShell =
                parsed.Language.Equals("powershell", StringComparison.OrdinalIgnoreCase)
                || parsed.Language.Equals("pwsh", StringComparison.OrdinalIgnoreCase)
                || parsed.Language.Equals("ps", StringComparison.OrdinalIgnoreCase);

            examples.Add(new MarkdownExample(parsed.Title, parsed.Command, isPowerShell));
        }

        return new MarkdownCommandHelp(summary, description, examples);
    }

    private static string? ResolveHelpFilePath(string extensionName, string commandKey)
    {
        var folder = (extensionName ?? string.Empty).ToLowerInvariant();
        var segments = string.IsNullOrWhiteSpace(commandKey)
            ? []
            : commandKey.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Reject path-traversal / rooted / separator-bearing segments. Callers (GetGroupHelp)
        // pass user-supplied CLI tokens straight through, so the resolver must not let one
        // like ".." or "C:\\..." escape the help corpus root.
        if (!IsSafeSegment(folder) || segments.Any(static s => !IsSafeSegment(s)))
        {
            return null;
        }

        // Dev-loop: prefer source markdown when running against a repo checkout.
        foreach (var root in ExtensionDiscoveryPaths.GetHelpRoots(AppContext.BaseDirectory))
        {
            var candidate = ProbeCandidates(root, folder, segments);
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsSafeSegment(string segment)
        => !string.IsNullOrEmpty(segment)
        && segment != "."
        && segment != ".."
        && !segment.Contains('/')
        && !segment.Contains('\\')
        && !Path.IsPathRooted(segment);

    // Tries leaf form (<segments>.md) first, then group form (<segments>/index.md).
    private static string? ProbeCandidates(string root, string folder, string[] segments)
    {
        if (segments.Length == 0)
        {
            var indexPath = Path.Combine(root, folder, "index.md");
            return File.Exists(indexPath) ? indexPath : null;
        }

        var leafSegments = new string[segments.Length + 1];
        leafSegments[0] = folder;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            leafSegments[i + 1] = segments[i];
        }
        leafSegments[^1] = segments[^1] + ".md";
        var leafPath = Path.Combine(root, Path.Combine(leafSegments));
        if (File.Exists(leafPath))
        {
            return leafPath;
        }

        var groupSegments = new string[segments.Length + 2];
        groupSegments[0] = folder;
        for (var i = 0; i < segments.Length; i++)
        {
            groupSegments[i + 1] = segments[i];
        }
        groupSegments[^1] = "index.md";
        var groupPath = Path.Combine(root, Path.Combine(groupSegments));
        return File.Exists(groupPath) ? groupPath : null;
    }

}
