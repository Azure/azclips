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
        // Use the shared parser so the runtime view of "Description" / "examples" is
        // identical to what MarkdownStructureValidator sees at build time. See
        // MarkdownHelpParser.cs for the canonical implementation.
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
            var source = Path.Combine(HelpPaths.CliHelpDocsRoot(repoRoot), folder, fileName);
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
}
