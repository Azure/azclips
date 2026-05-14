// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Core.Abstractions;

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Validates CLI examples in help markdown against the live <see cref="IExtensionIndex"/>:
    /// <list type="bullet">
    ///   <item><description><b>L1</b> — every <c>azv2 ...</c> line tokenizes to a routed command path.</description></item>
    ///   <item><description><b>L2</b> — every <c>--flag</c> / <c>-x</c> token resolves to a declared parameter
    ///   (or to the documented global flag set: <c>--help</c>, <c>--debug</c>, <c>--output</c>, <c>-h</c>).</description></item>
    /// </list>
    /// Required-parameter-presence (a stricter form of L2) is intentionally deferred until positional
    /// parameter metadata is exposed by the framework — see <c>docs/spec/help-system.md §15.4.6</c>.
    /// PowerShell examples and non-<c>azv2</c> shell lines are ignored.
    /// </summary>
    public sealed class ExampleSyntaxValidator
    {
        // Globals allowed on every CLI invocation (documented in docs/howto/help.md).
        private static readonly HashSet<string> s_globalFlags = new(StringComparer.OrdinalIgnoreCase)
        {
            "--help", "-h",
            "--debug",
            "--output",
        };

        // Matches a bash/sh fenced code block. Captures the body for line-by-line scanning.
        private static readonly Regex s_bashFenceRegex = new(
            @"```(?:bash|sh)\s*\r?\n(?<body>[\s\S]*?)\r?\n```",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Walks every markdown file under <c>src/Extensions/*/help/</c> and validates the
        /// CLI examples it contains against the supplied per-CLI-group indexes.
        /// </summary>
        /// <param name="repoRoot">Repository root (folder containing <c>src/</c>).</param>
        /// <param name="indexesByCliGroup">
        /// Map of CLI group name (the first token after <c>azv2</c>, e.g. <c>group</c>, <c>login</c>) → its
        /// <see cref="IExtensionIndex"/>. Case-insensitive.
        /// </param>
        public ValidationReport Validate(string repoRoot, IReadOnlyDictionary<string, IExtensionIndex> indexesByCliGroup)
        {
            var report = new ValidationReport();
            var helpRoot = Path.Combine(repoRoot, "src", "Extensions");

            if (!Directory.Exists(helpRoot))
            {
                return report;
            }

            var markdownFiles = Directory
                .EnumerateFiles(helpRoot, "*.md", SearchOption.AllDirectories)
                .Where(static path => path.Contains($"{Path.DirectorySeparatorChar}help{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var file in markdownFiles)
            {
                var content = File.ReadAllText(file);
                var relativePath = Path.GetRelativePath(repoRoot, file);
                ValidateContent(content, relativePath, indexesByCliGroup, report);
            }

            return report;
        }

        /// <summary>
        /// I/O-free overload exposed for unit testing. Validates examples in a single markdown document.
        /// </summary>
        public ValidationReport ValidateContent(
            string content,
            string filePath,
            IReadOnlyDictionary<string, IExtensionIndex> indexesByCliGroup)
        {
            var report = new ValidationReport();
            ValidateContent(content, filePath, indexesByCliGroup, report);
            return report;
        }

        private static void ValidateContent(
            string content,
            string filePath,
            IReadOnlyDictionary<string, IExtensionIndex> indexesByCliGroup,
            ValidationReport report)
        {
            foreach (Match fence in s_bashFenceRegex.Matches(content))
            {
                var body = fence.Groups["body"].Value;
                var fenceStartLine = LineOfOffset(content, fence.Groups["body"].Index);

                var lines = body.Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].TrimEnd('\r').Trim();

                    // Skip blank lines, comments, and any non-azv2 lines (prompts, output samples, etc.).
                    if (line.Length == 0 || line.StartsWith('#') || !line.StartsWith("azv2", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var lineNumber = fenceStartLine + i;
                    ValidateExampleLine(line, $"{filePath}:{lineNumber}", indexesByCliGroup, report);
                }
            }
        }

        private static void ValidateExampleLine(
            string line,
            string locator,
            IReadOnlyDictionary<string, IExtensionIndex> indexesByCliGroup,
            ValidationReport report)
        {
            var tokens = Tokenize(line);

            // Expect: azv2 <group> [subcommand...] [--flag value]...
            if (tokens.Count < 2 || !string.Equals(tokens[0], "azv2", StringComparison.Ordinal))
            {
                return; // not an azv2 invocation
            }

            var groupName = tokens[1];
            if (!indexesByCliGroup.TryGetValue(groupName, out var index))
            {
                report.AddError(
                    $"Unknown CLI group '{groupName}' in example. No extension registers this group.",
                    locator);
                return;
            }

            var cliTable = index.GetCommandTable(ClientType.CLI);

            // Greedy resolve: try the longest prefix of remaining tokens that does not look like a flag/value.
            var commandTokens = new List<string>();
            var consumed = 2;
            for (var i = 2; i < tokens.Count; i++)
            {
                if (IsFlag(tokens[i]))
                {
                    break;
                }

                commandTokens.Add(tokens[i]);
            }

            // Try longest match first, shrinking until we find a table key (or fall back to root "").
            CommandMetadata? command = null;
            for (var take = commandTokens.Count; take >= 0; take--)
            {
                var key = string.Join(' ', commandTokens.Take(take));
                if (cliTable.TryGetValue(key, out var match))
                {
                    command = match;
                    consumed = 2 + take;
                    break;
                }
            }

            if (command is null)
            {
                var attempted = commandTokens.Count == 0 ? "<root>" : string.Join(' ', commandTokens);
                report.AddError(
                    $"Unknown command path 'azv2 {groupName} {attempted}'. No matching command in the '{groupName}' extension.",
                    locator);
                return;
            }

            ValidateFlags(tokens, consumed, command, locator, report);
        }

        private static void ValidateFlags(
            List<string> tokens,
            int startIndex,
            CommandMetadata command,
            string locator,
            ValidationReport report)
        {
            for (var i = startIndex; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (!IsFlag(token))
                {
                    // Positional value (or value following an earlier flag) — skip in L2.
                    continue;
                }

                // Support `--flag=value` and `-f=value` forms.
                var flag = token;
                var eq = flag.IndexOf('=');
                if (eq > 0)
                {
                    flag = flag[..eq];
                }

                if (s_globalFlags.Contains(flag))
                {
                    continue;
                }

                var bareName = flag.TrimStart('-');
                if (IsKnownParameter(command, bareName))
                {
                    continue;
                }

                report.AddError(
                    $"Unknown flag '{flag}' on command '{command.Name}'. Not declared as a parameter or alias.",
                    locator);
            }
        }

        private static bool IsKnownParameter(CommandMetadata command, string bareName)
        {
            // CLI parameter table keys are stored in kebab-case (matching the on-the-wire flag).
            if (command.Parameters.ContainsKey(bareName))
            {
                return true;
            }

            // Aliases map alias-name → canonical-name. Either side counts as "known".
            if (command.ParameterAliases.ContainsKey(bareName) || command.ParameterAliases.Values.Contains(bareName, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            // Case-insensitive fallback for parameter names — defensive against extension authors using PascalCase keys.
            foreach (var key in command.Parameters.Keys)
            {
                if (string.Equals(key, bareName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A flag token starts with <c>--</c> (long form) or with a single <c>-</c> followed by an
        /// alphabetic character (short form). URLs (<c>https://...</c>), paths (<c>/subscriptions/...</c>),
        /// negative numbers (<c>-1</c>) are NOT treated as flags.
        /// </summary>
        private static bool IsFlag(string token)
        {
            if (token.Length < 2 || token[0] != '-')
            {
                return false;
            }

            if (token[1] == '-')
            {
                return token.Length > 2;
            }

            return char.IsLetter(token[1]);
        }

        /// <summary>
        /// Shell-style tokenizer: splits on whitespace, honoring single/double quotes so values
        /// containing spaces stay intact. Backslash escapes are not interpreted (rarely needed in help).
        /// </summary>
        private static List<string> Tokenize(string line)
        {
            var tokens = new List<string>();
            var current = new System.Text.StringBuilder();
            char? quote = null;

            foreach (var c in line)
            {
                if (quote is char q)
                {
                    if (c == q)
                    {
                        quote = null;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    quote = c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
            }

            return tokens;
        }

        private static int LineOfOffset(string content, int offset)
        {
            // 1-based line number for the supplied character offset.
            var line = 1;
            for (var i = 0; i < offset && i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    line++;
                }
            }

            return line;
        }
    }
}
