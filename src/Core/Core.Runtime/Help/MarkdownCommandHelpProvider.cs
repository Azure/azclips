using System.Text.RegularExpressions;

namespace Core.Runtime;

public sealed record MarkdownExample(string Title, string Command, bool IsPowerShell);

public sealed record MarkdownCommandHelp(string Summary, string Description, IReadOnlyList<MarkdownExample> Examples)
{
    public static readonly MarkdownCommandHelp Empty = new(string.Empty, string.Empty, []);
}

public static class MarkdownCommandHelpProvider
{
    private static readonly Regex ExampleRegex = new(
        @"###\s*(?<title>.+?)\s*\r?\n```(?<lang>[a-zA-Z0-9_-]*)\s*\r?\n(?<command>[\s\S]*?)\r?\n```",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static MarkdownCommandHelp Get(string extensionName, string commandKey)
    {
        var filePath = GetHelpFilePath(extensionName, commandKey);
        if (filePath == null || !File.Exists(filePath))
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

        var summary = GetField(content, "Summary");
        var description = GetField(content, "Description");

        var examples = new List<MarkdownExample>();
        foreach (Match match in ExampleRegex.Matches(content))
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

    private static string? GetHelpFilePath(string extensionName, string commandKey)
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot == null)
        {
            return null;
        }

        var extensionFolder = ToPascalCase(extensionName);
        var fileName = string.IsNullOrWhiteSpace(commandKey)
            ? "index.md"
            : commandKey.Replace(' ', '-').ToLowerInvariant() + ".md";

        return Path.Combine(repoRoot, "src", "Extensions", extensionFolder, "help", fileName);
    }

    private static string FindRepoRoot()
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

        return Directory.GetCurrentDirectory();
    }

    private static string GetField(string content, string name)
    {
        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
            {
                return line[(name.Length + 1)..].Trim();
            }
        }

        return string.Empty;
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length == 1
            ? value.ToUpperInvariant()
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }
}
