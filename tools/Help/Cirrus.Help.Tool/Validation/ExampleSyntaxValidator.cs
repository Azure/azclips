// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
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
    ///   <item><description><b>L3</b> — every literal value following a known flag parses as the parameter's declared
    ///   <see cref="ParameterMetadata.ParameterType"/>, and (if specified) is a member of
    ///   <see cref="ParameterMetadata.AllowedValues"/>. Placeholders (<c>&lt;...&gt;</c>, <c>$var</c>) are skipped.</description></item>
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
                    ProblemIds.CLI_L1_UnknownGroup,
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
                    ProblemIds.CLI_L1_UnknownCommand,
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
                    // Positional value (or value following an earlier flag we already consumed). Skip.
                    continue;
                }

                // Support `--flag=value` and `-f=value` forms.
                var flag = token;
                string? equalsValue = null;
                var eq = flag.IndexOf('=');
                if (eq > 0)
                {
                    equalsValue = flag[(eq + 1)..];
                    flag = flag[..eq];
                }

                if (s_globalFlags.Contains(flag))
                {
                    // Consume a possible value (e.g. --output table). Globals are not L3-checked.
                    if (equalsValue is null && i + 1 < tokens.Count && !IsFlag(tokens[i + 1]))
                    {
                        i++;
                    }
                    continue;
                }

                var bareName = flag.TrimStart('-');
                if (!TryGetParameter(command, bareName, out var parameter))
                {
                    report.AddError(
                        ProblemIds.CLI_L2_UnknownFlag,
                        $"Unknown flag '{flag}' on command '{command.Name}'. Not declared as a parameter or alias.",
                        locator);
                    continue;
                }

                // L3: determine the value (if any) and type-check it.
                var value = equalsValue;
                if (value is null && i + 1 < tokens.Count && !IsFlag(tokens[i + 1]))
                {
                    value = tokens[i + 1];
                    i++; // consume the value token
                }

                if (value is null || IsPlaceholder(value))
                {
                    // Switch-form (bool flag with no value) is accepted silently \u2014 see docs/spec/help-system.md \u00a715.4.8.
                    continue;
                }

                var typeError = CheckValue(parameter!, value);
                if (typeError is not null)
                {
                    report.AddError(
                        ProblemIds.CLI_L3_InvalidValue,
                        $"Value '{value}' for flag '{flag}' on command '{command.Name}' is invalid: {typeError}",
                        locator);
                }
            }
        }

        private static bool TryGetParameter(CommandMetadata command, string bareName, out ParameterMetadata? parameter)
        {
            if (command.Parameters.TryGetValue(bareName, out var direct))
            {
                parameter = direct;
                return true;
            }

            // Aliases map alias-name \u2192 canonical-name.
            if (command.ParameterAliases.TryGetValue(bareName, out var canonical)
                && command.Parameters.TryGetValue(canonical, out var aliased))
            {
                parameter = aliased;
                return true;
            }

            // Case-insensitive fallback for parameter names (defensive against PascalCase keys).
            foreach (var (key, meta) in command.Parameters)
            {
                if (string.Equals(key, bareName, StringComparison.OrdinalIgnoreCase))
                {
                    parameter = meta;
                    return true;
                }
            }

            // Canonical of an alias used as the flag itself.
            foreach (var canonicalName in command.ParameterAliases.Values)
            {
                if (string.Equals(canonicalName, bareName, StringComparison.OrdinalIgnoreCase)
                    && command.Parameters.TryGetValue(canonicalName, out var byCanonical))
                {
                    parameter = byCanonical;
                    return true;
                }
            }

            parameter = null;
            return false;
        }

        /// <summary>
        /// Placeholder values that should not be type-checked. Mirrors conventions in <c>docs/howto/help.md</c>.
        /// </summary>
        private static bool IsPlaceholder(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (value[0] == '<' && value[^1] == '>')
            {
                return true; // <my-rg>, <subId>, <password>
            }

            if (value[0] == '$')
            {
                return true; // $var, ${var}
            }

            if (value.Equals("00000000-0000-0000-0000-000000000000", StringComparison.Ordinal))
            {
                return true; // canonical example Guid
            }

            return false;
        }

        /// <summary>
        /// Returns null when the value is acceptable; otherwise returns a short reason fragment
        /// to embed in the error message (e.g. <c>"not a valid Guid"</c>).
        /// </summary>
        private static string? CheckValue(ParameterMetadata parameter, string value)
        {
            // AllowedValues takes precedence: if specified, the value MUST match (case-insensitive).
            if (parameter.AllowedValues is { Length: > 0 })
            {
                foreach (var allowed in parameter.AllowedValues)
                {
                    if (string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }

                return $"not in allowed values [{string.Join(", ", parameter.AllowedValues)}]";
            }

            var type = parameter.ParameterType;
            if (type is null)
            {
                return null;
            }

            // Unwrap Nullable<T>.
            type = Nullable.GetUnderlyingType(type) ?? type;

            // List/IEnumerable element validation is deferred (see docs/spec/help-system.md \u00a715.4.8).
            if (IsListLike(type))
            {
                return null;
            }

            if (type == typeof(string))
            {
                return null;
            }

            if (type == typeof(Guid))
            {
                return Guid.TryParse(value, out _) ? null : "not a valid Guid";
            }

            if (type == typeof(int))
            {
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                    ? null : "not a valid Int32";
            }

            if (type == typeof(long))
            {
                return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                    ? null : "not a valid Int64";
            }

            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            {
                return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                    ? null : $"not a valid {type.Name}";
            }

            if (type == typeof(bool))
            {
                return (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                    ? null : "not a valid Boolean";
            }

            if (type.IsEnum)
            {
                var names = Enum.GetNames(type);
                foreach (var name in names)
                {
                    if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }
                return $"not one of [{string.Join(", ", names)}]";
            }

            // Unknown type \u2014 no L3 check.
            return null;
        }

        private static bool IsListLike(Type type)
        {
            if (type.IsArray)
            {
                return true;
            }

            if (!type.IsGenericType)
            {
                return false;
            }

            var def = type.GetGenericTypeDefinition();
            return def == typeof(List<>)
                || def == typeof(IEnumerable<>)
                || def == typeof(IList<>)
                || def == typeof(ICollection<>)
                || def == typeof(IReadOnlyList<>)
                || def == typeof(IReadOnlyCollection<>);
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
