using System.Text.RegularExpressions;

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
/// Reads and parses help markdown from <c>src/CLI/Help/Docs/&lt;ext&gt;/*.md</c> at runtime.
/// Lenient: malformed/missing input returns <see cref="MarkdownCommandHelp.Empty"/> rather
/// than throwing — strict checks live in the build-time help tool.
/// </summary>
public static class MarkdownCommandHelpProvider
{
    // Title line, optional blank line(s), then the opening ``` fence. Markdown
    // authors commonly leave a blank line between the H3 heading and the fence;
    // the previous tighter `\s*\r?\n```` form silently dropped every example in
    // those files.
    private static readonly Regex ExampleRegex = new(
        @"###\s*(?<title>[^\r\n]+?)[ \t]*\r?\n(?:[ \t]*\r?\n)*```(?<lang>[a-zA-Z0-9_-]*)[ \t]*\r?\n(?<command>[\s\S]*?)\r?\n```",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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
        var description = GetField(body, "Description");

        var examples = new List<MarkdownExample>();
        foreach (Match match in ExampleRegex.Matches(body))
        {
            var title = match.Groups["title"].Value.Trim();
            var language = match.Groups["lang"].Value.Trim();
            var command = match.Groups["command"].Value.Trim();
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            var isPowerShell = language.Equals("powershell", StringComparison.OrdinalIgnoreCase)
                || language.Equals("pwsh", StringComparison.OrdinalIgnoreCase)
                || language.Equals("ps", StringComparison.OrdinalIgnoreCase);

            examples.Add(new MarkdownExample(
                string.IsNullOrWhiteSpace(title) ? "Example" : title,
                command,
                isPowerShell));
        }

        return new MarkdownCommandHelp(summary, description, examples);
    }

    private static string? ResolveHelpFilePath(string extensionName, string commandKey)
    {
        var folder = (extensionName ?? string.Empty).ToLowerInvariant();
        var fileName = string.IsNullOrWhiteSpace(commandKey)
            ? "index.md"
            : commandKey.Replace(' ', '-').ToLowerInvariant() + ".md";

        // Repo-aware preference: if we can locate `cirrus.slnx` from BaseDirectory we're
        // running against a developer build, so prefer the source markdown — authors
        // can edit `src/CLI/Help/Docs/...` and see results without rebuilding. If no repo
        // root is reachable (shipped install), fall back to the packaged copy next to
        // the CLI binary (see Core.Cli.csproj `<Content Include="Help\Docs\**\*.md">`).
        var repoRoot = FindRepoRoot();
        if (repoRoot != null)
        {
            var source = Path.Combine(repoRoot, "src", "CLI", "Help", "Docs", folder, fileName);
            if (File.Exists(source))
            {
                return source;
            }
        }

        var packaged = Path.Combine(AppContext.BaseDirectory, "help", folder, fileName);
        if (File.Exists(packaged))
        {
            return packaged;
        }

        return null;
    }

    private static string? FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var solutionPath = Path.Combine(current.FullName, "cirrus.slnx");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Reads a <c>Name: value</c> body field, including any continuation lines that follow.
    /// A continuation line is any non-blank, non-heading, non-fence line that comes after
    /// the field's first line — matches the PR-#226 authoring convention demonstrated in
    /// the structure-validator tests (`Description:` followed by indented continuation).
    /// </summary>
    private static string GetField(string content, string name)
    {
        var lines = content.Split('\n');
        var prefix = name + ":";
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r').TrimStart();
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var builder = new System.Text.StringBuilder(line[(prefix.Length)..].Trim());
            for (var j = i + 1; j < lines.Length; j++)
            {
                var next = lines[j].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(next)
                    || next.TrimStart().StartsWith("#", StringComparison.Ordinal)
                    || next.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    break;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(next.Trim());
            }

            return builder.ToString();
        }

        return string.Empty;
    }
}
