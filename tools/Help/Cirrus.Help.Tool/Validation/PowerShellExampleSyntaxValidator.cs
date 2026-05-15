// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation.Language;
using System.Text.RegularExpressions;

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Validates PowerShell examples in help markdown against the live <see cref="CmdletIndex"/>:
    /// <list type="bullet">
    ///   <item><description><b>PS-L1</b> — every command invocation resolves to a registered cmdlet
    ///   (<c>Verb-Noun</c>). Native commands and pipeline fragments are skipped.</description></item>
    ///   <item><description><b>PS-L2</b> — every <c>-Parameter</c> token matches a declared parameter
    ///   name or alias on that cmdlet, or is one of the documented common parameters.</description></item>
    /// </list>
    /// Parsing is done via the PowerShell language parser (<see cref="Parser.ParseInput(string, out Token[], out ParseError[])"/>),
    /// which gives accurate line numbers, backtick-continuation handling, and splatting detection
    /// for free. Inspired by — but lighter than — azure-powershell's <c>ExampleAnalyzer</c>, which
    /// runs <c>PSScriptAnalyzer</c> over a generated temp script. We only need static name resolution,
    /// so the AST alone is enough. See <c>docs/spec/help-system.md §15.4 item 7</c>.
    /// </summary>
    public sealed class PowerShellExampleSyntaxValidator
    {
        // Common parameters that every advanced cmdlet inherits. Mirrors the list azure-powershell's
        // ExampleAnalyzer/utils.ps1 whitelists.
        private static readonly HashSet<string> s_commonParameters = new(StringComparer.OrdinalIgnoreCase)
        {
            "Verbose",
            "Debug",
            "ErrorAction",
            "ErrorVariable",
            "WarningAction",
            "WarningVariable",
            "InformationAction",
            "InformationVariable",
            "OutVariable",
            "OutBuffer",
            "PipelineVariable",
            "WhatIf",
            "Confirm",
        };

        // Verb-Noun pattern: at least one letter, dash, at least one letter. We deliberately do not
        // enforce PowerShell's approved verbs list — that's a separate concern.
        private static readonly Regex s_verbNounPattern = new(
            @"^[A-Za-z]+-[A-Za-z][A-Za-z0-9]*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Matches a powershell/pwsh fenced code block. Captures the body for AST parsing.
        private static readonly Regex s_pwshFenceRegex = new(
            @"```(?:powershell|pwsh|ps)\s*\r?\n(?<body>[\s\S]*?)\r?\n```",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Walks every markdown file under <c>src/Extensions/*/help/</c> and validates the
        /// PowerShell examples it contains.
        /// </summary>
        public ValidationReport Validate(string repoRoot, CmdletIndex cmdlets)
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
                ValidateContent(content, relativePath, cmdlets, report);
            }

            return report;
        }

        /// <summary>
        /// I/O-free overload exposed for unit testing. Validates examples in a single markdown document.
        /// </summary>
        public ValidationReport ValidateContent(string content, string filePath, CmdletIndex cmdlets)
        {
            var report = new ValidationReport();
            ValidateContent(content, filePath, cmdlets, report);
            return report;
        }

        private static void ValidateContent(
            string content,
            string filePath,
            CmdletIndex cmdlets,
            ValidationReport report)
        {
            foreach (Match fence in s_pwshFenceRegex.Matches(content))
            {
                var body = fence.Groups["body"].Value;
                var fenceBodyAbsoluteLine = LineOfOffset(content, fence.Groups["body"].Index);

                // Strip PS prompt prefixes ("PS>" / "PS C:\>") line-by-line so the parser sees clean script.
                var cleanedBody = StripPrompts(body);

                var ast = Parser.ParseInput(cleanedBody, out _, out var parseErrors);

                // Don't let raw parse errors prevent us from running L1/L2 — many of our examples
                // contain placeholder constructs that ParseInput dislikes (e.g. `<your-rg>`).
                // We only surface parse errors when there is no usable AST at all.
                if (ast is null)
                {
                    foreach (var err in parseErrors)
                    {
                        report.AddError(
                            ProblemIds.PS_ParseError,
                            $"PowerShell parse error: {err.Message}",
                            $"{filePath}:{fenceBodyAbsoluteLine + (err.Extent?.StartLineNumber ?? 1) - 1}");
                    }
                    continue;
                }

                var commands = ast.FindAll(node => node is CommandAst, searchNestedScriptBlocks: true)
                    .Cast<CommandAst>();

                foreach (var cmd in commands)
                {
                    ValidateCommand(cmd, cmdlets, filePath, fenceBodyAbsoluteLine, report);
                }
            }
        }

        private static void ValidateCommand(
            CommandAst command,
            CmdletIndex cmdlets,
            string filePath,
            int fenceBodyAbsoluteLine,
            ValidationReport report)
        {
            var name = command.GetCommandName();
            if (string.IsNullOrEmpty(name))
            {
                // Anonymous invocation (e.g. `& $scriptblock`) — nothing to validate.
                return;
            }

            // Only police things that look like cmdlets. Native commands (`dotnet build`, `git`),
            // executables, and bare functions are out of scope.
            if (!s_verbNounPattern.IsMatch(name))
            {
                return;
            }

            // Absolute line number = (markdown line of fence body start) + (1-based line within body) - 1.
            var lineNumber = fenceBodyAbsoluteLine + command.Extent.StartLineNumber - 1;
            var locator = $"{filePath}:{lineNumber}";

            if (!cmdlets.TryGet(name, out var metadata))
            {
                report.AddError(
                    ProblemIds.PSL1_UnknownCmdlet,
                    $"Unknown cmdlet '{name}' in PowerShell example. Not registered by any Cirrus cmdlet assembly.",
                    locator);
                return;
            }

            // If the command splats (`@params`), we cannot statically see the parameter set — skip L2.
            if (HasSplattedArgument(command))
            {
                return;
            }

            foreach (var element in command.CommandElements.OfType<CommandParameterAst>())
            {
                var paramName = element.ParameterName;
                if (string.IsNullOrEmpty(paramName))
                {
                    continue;
                }

                if (s_commonParameters.Contains(paramName))
                {
                    continue;
                }

                if (metadata.ParameterNames.Contains(paramName))
                {
                    continue;
                }

                if (metadata.ParameterAliases.ContainsKey(paramName))
                {
                    continue;
                }

                var elementLine = fenceBodyAbsoluteLine + element.Extent.StartLineNumber - 1;
                report.AddError(
                    ProblemIds.PSL2_UnknownParameter,
                    $"Unknown parameter '-{paramName}' on cmdlet '{name}'. Not declared as a parameter or alias.",
                    $"{filePath}:{elementLine}");
            }
        }

        private static bool HasSplattedArgument(CommandAst command)
        {
            // `@params` shows up as a VariableExpressionAst whose Splatted property is true.
            return command.CommandElements
                .OfType<VariableExpressionAst>()
                .Any(v => v.Splatted);
        }

        private static string StripPrompts(string body)
        {
            // Rewrites lines that start with `PS>` / `PS C:\>` / `PS /home/me>` to drop the prompt.
            // The trailing whitespace and prompt sigil get replaced with equivalent-width spaces to
            // preserve column numbers from the AST.
            var lines = body.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var m = Regex.Match(line, @"^(\s*PS[^>]*>\s?)");
                if (m.Success)
                {
                    lines[i] = new string(' ', m.Length) + line[m.Length..];
                }
            }
            return string.Join('\n', lines);
        }

        /// <summary>
        /// Returns the 1-based line number of the absolute character offset within <paramref name="content"/>.
        /// </summary>
        private static int LineOfOffset(string content, int offset)
        {
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
