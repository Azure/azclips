using System.Text.RegularExpressions;

namespace Core.Cli.Help;

/// <summary>
/// A single example block parsed from a help markdown file. The <see cref="IsPowerShell"/>
/// flag is driven entirely by the fence language tag (<c>powershell</c>/<c>pwsh</c> → true,
/// <c>bash</c>/<c>sh</c>/anything else → false); the <c>(CLI)</c> / <c>(PowerShell)</c> suffix
/// in the H3 title is cosmetic and has no parsing effect.
/// </summary>
public sealed record MarkdownExample(string Title, string Command, bool IsPowerShell);

/// <summary>
/// The parsed shape of a help markdown file: frontmatter <c>summary</c>, body
/// <c>Description:</c> line, and the ordered list of example fences. Use
/// <see cref="Empty"/> as the sentinel when no markdown exists for a command — callers
/// fall back to <c>[ProcessorInfo]</c>/<c>[ArgumentInfo]</c> attributes from there.
/// </summary>
public sealed record MarkdownCommandHelp(string Summary, string Description, IReadOnlyList<MarkdownExample> Examples)
{
    public static readonly MarkdownCommandHelp Empty = new(string.Empty, string.Empty, []);
}

/// <summary>
/// Reads and parses help markdown from <c>src/CLI/Help/&lt;ext&gt;/*.md</c> at
/// runtime. This is the cold-path entry point for CLI help rendering.
/// </summary>
/// <remarks>
/// <para>
/// File resolution: <c>commandKey</c> is dash-joined and lower-cased; an empty key resolves
/// to <c>index.md</c>. Extension folder names are used verbatim (lower-case, e.g. <c>compute</c>).
/// </para>
/// <para>
/// Two lookup roots, in order:
/// <list type="number">
///   <item><description><c>&lt;AppContext.BaseDirectory&gt;/help/&lt;ext&gt;/&lt;key&gt;.md</c> — the packaged path; the
///   <c>Core.Cli</c> project copies <c>src/CLI/Help/**/*.md</c> to its output as content.</description></item>
///   <item><description><c>&lt;repoRoot&gt;/src/CLI/Help/&lt;ext&gt;/&lt;key&gt;.md</c> — dev-loop fallback so authors can edit
///   markdown without re-running the build.</description></item>
/// </list>
/// </para>
/// <para>
/// Lenient at runtime, strict at build. Malformed or missing markdown returns
/// <see cref="MarkdownCommandHelp.Empty"/> rather than throwing — a typo in one file never
/// crashes the CLI. Structural and semantic validation is the job of
/// <c>MarkdownStructureValidator</c> + the example validators in the help tool.
/// </para>
/// <para>
/// No caching today. Help is read once per CLI process invocation; if profiling later
/// shows I/O hot-pathing, key a <c>ConcurrentDictionary</c> on
/// <c>(extensionName, commandKey)</c> with <c>LastWriteTime</c> invalidation.
/// </para>
/// </remarks>
public static class MarkdownCommandHelpProvider
{
    private static readonly Regex ExampleRegex = new(
        @"###\s*(?<title>.+?)\s*\r?\n```(?<lang>[a-zA-Z0-9_-]*)\s*\r?\n(?<command>[\s\S]*?)\r?\n```",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Loads and parses the help markdown file for a given extension + command key.
    /// Returns <see cref="MarkdownCommandHelp.Empty"/> when the file does not exist.
    /// </summary>
    /// <param name="extensionName">Extension name (case-insensitive; lower-cased for folder lookup).</param>
    /// <param name="commandKey">Canonical CLI command key (space-separated, lowercased, dashed for file lookup). Empty string resolves to <c>index.md</c>.</param>
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

    /// <summary>
    /// Parses raw markdown content into a <see cref="MarkdownCommandHelp"/>.
    /// Designed for graceful degradation: malformed input returns empty/partial content
    /// rather than throwing. Strict validation lives in the build-time help tool.
    /// </summary>
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
                || language.Equals("pwsh", StringComparison.OrdinalIgnoreCase);

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

        // Primary: packaged path next to the CLI binary (see Core.Cli.csproj <Content Include="help\**\*.md">).
        var packaged = Path.Combine(AppContext.BaseDirectory, "help", folder, fileName);
        if (File.Exists(packaged))
        {
            return packaged;
        }

        // Dev-loop fallback: read straight from the repo source so authors don't need
        // a rebuild to see their .md edits reflected in --help output.
        var repoRoot = FindRepoRoot();
        if (repoRoot != null)
        {
            var source = Path.Combine(repoRoot, "src", "CLI", "Help", folder, fileName);
            if (File.Exists(source))
            {
                return source;
            }
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
            var line = lines[i].TrimEnd('\r');
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var builder = new System.Text.StringBuilder(line[(prefix.Length)..].Trim());
            for (var j = i + 1; j < lines.Length; j++)
            {
                var next = lines[j].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(next)
                    || next.StartsWith("#", StringComparison.Ordinal)
                    || next.StartsWith("```", StringComparison.Ordinal))
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
