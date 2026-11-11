// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;

namespace Azclips.Help.Tool.Validation
{
    /// <summary>
    /// Validates PowerShell examples in help markdown against the live <see cref="CmdletIndex"/>:
    /// <list type="bullet">
    ///   <item><description>Every command invocation resolves to a registered cmdlet
    ///   (<c>Verb-Noun</c>). Native commands and pipeline fragments are skipped.</description></item>
    ///   <item><description>Every <c>-Parameter</c> token matches a declared parameter
    ///   name or alias on that cmdlet, or is one of the common parameters (<c>Verbose</c>,
    ///   <c>ErrorAction</c>, <c>WhatIf</c>, etc.).</description></item>
    /// </list>
    /// Parsing is done via the PowerShell language parser (<see cref="Parser.ParseInput(string, out Token[], out ParseError[])"/>),
    /// which gives accurate line numbers, backtick-continuation handling, and splatting detection
    /// for free.
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

        // Matches the start of an H2 markdown section heading. Used to scope fence collection
        // to the `## EXAMPLES` block: we deliberately do not police fences in `## SYNTAX` or
        // `## NOTES` because those are PlatyPS-generated and contain `<Type>` placeholders that
        // cannot parse.
        private static readonly Regex s_h2HeadingRegex = new(
            @"(?m)^\#\#\s+(?<title>[^\r\n]+?)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Matches any fenced code block whose language is PowerShell-flavored or empty. PlatyPS
        // emits bare fences (no tag) by default, so we accept those alongside the explicit tags.
        // Other languages (`bash`, `json`, `yaml`, …) are deliberately skipped — those are a
        // different validator's job, even when they appear inside a PowerShell help file.
        private static readonly Regex s_anyFenceInExamplesRegex = new(
            @"```(?<lang>powershell|pwsh|ps|)\s*\r?\n(?<body>[\s\S]*?)\r?\n```",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Walks every markdown file under <c>src/PS/&lt;Module&gt;/help/</c> and validates the
        /// PowerShell examples it contains.
        /// </summary>
        public ValidationReport Validate(string repoRoot, CmdletIndex cmdlets)
        {
            var report = new ValidationReport();
            var helpRoot = Core.Cli.Help.HelpPaths.PsModulesRoot(repoRoot);

            if (!Directory.Exists(helpRoot))
            {
                return report;
            }

            // PS authoring lives in `src/PS/<Module>/help/*.md`. The `help` segment is lowercase.
            var psHelpSegment = $"{Path.DirectorySeparatorChar}{Core.Cli.Help.HelpPaths.PsHelpFolder}{Path.DirectorySeparatorChar}";
            var markdownFiles = Directory
                .EnumerateFiles(helpRoot, "*.md", SearchOption.AllDirectories)
                .Where(path => path.Contains(psHelpSegment, StringComparison.OrdinalIgnoreCase))
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
            // The documented cmdlet is encoded in the filename (e.g. `Connect-AzAccount.md`).
            // PSL7 enforces that every fence in the EXAMPLES section actually invokes that
            // cmdlet — anything else means the example is wrong help, regardless of whether
            // the individual lines parse cleanly.
            var documentedCmdlet = Path.GetFileNameWithoutExtension(filePath);
            var hasDocumentedCmdletName = s_verbNounPattern.IsMatch(documentedCmdlet);

            // Whole-file TODO scan. The only producer of TODO in the PS corpus is our scaffolder.
            if (PlaceholderDetector.ContainsTodo(content))
            {
                report.AddError(
                    ProblemIds.PS_TodoPlaceholder,
                    "PowerShell help file still contains a TODO marker. Replace every scaffolded 'TODO - ...' placeholder with real content before merging.",
                    filePath);
            }

            // PSL12: PlatyPS `{{ Fill ... }}` markers render literally in Get-Help -Full.
            // `{{Insert list of aliases}}` is excluded (see PlaceholderDetector.s_fillPlaceholderRegex).
            foreach (var (token, lineNumber) in PlaceholderDetector.EnumerateFillPlaceholders(content))
            {
                report.AddError(
                    ProblemIds.PSL12_FillPlaceholder,
                    $"Help file contains scaffolded placeholder '{token}'. Replace with real content (parameter description, notes, or related links) before merging. For optional sections such as Notes, remove the placeholder if no content applies; retain the heading.",
                    $"{filePath}:{lineNumber}");
            }

            var fenceCount = 0;
            foreach (var (body, fenceBodyAbsoluteLine) in EnumerateExampleFences(content))
            {
                fenceCount++;

                // Ban `<...>` placeholders in PS example fences. Allowed escapes: literals,
                // `$variable`, splatted hashtables.
                foreach (var placeholder in PlaceholderDetector.EnumerateAngleBracketPlaceholders(body))
                {
                    report.AddError(
                        ProblemIds.PS_ExamplePlaceholder,
                        $"Example contains placeholder '{placeholder}'. Replace with a realistic literal value, a `$variable`, or a splatted hashtable.",
                        $"{filePath}:{fenceBodyAbsoluteLine}");
                }

                // Strip PS prompt prefixes ("PS>" / "PS C:\>") line-by-line so the parser sees clean script.
                var cleanedBody = StripPrompts(body);

                var ast = Parser.ParseInput(cleanedBody, out _, out var parseErrors);

                // Don't let raw parse errors prevent us from running cmdlet/parameter checks — many of our examples
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
                    .Cast<CommandAst>()
                    .ToList();

                foreach (var cmd in commands)
                {
                    ValidateCommand(cmd, cmdlets, filePath, fenceBodyAbsoluteLine, report);
                }

                ValidateUnassignedVariables(ast, filePath, fenceBodyAbsoluteLine, report);

                // PSL7: every example fence must invoke the cmdlet whose help file it lives in.
                // Setup commands (e.g. `$rg = New-AzResourceGroup ...` in `Get-AzResourceGroup.md`) are
                // fine as long as the documented cmdlet appears at least once in the same fence.
                if (hasDocumentedCmdletName
                    && !commands.Any(c => string.Equals(c.GetCommandName(), documentedCmdlet, StringComparison.OrdinalIgnoreCase)))
                {
                    report.AddError(
                        ProblemIds.PSL7_ExampleMustInvokeDocumentedCmdlet,
                        $"Example does not invoke the documented cmdlet '{documentedCmdlet}'. PowerShell help examples must demonstrate the cmdlet they belong to.",
                        $"{filePath}:{fenceBodyAbsoluteLine}");
                }
            }

            // PSL8: every cmdlet help file must contain at least one example fence under
            // `## EXAMPLES`. Empty `### <Title>` headings with no code block don't count —
            // they render as an empty section in `Get-Help -Examples`. PlatyPS-scaffolded
            // stubs land here when an author hasn't filled them in yet.
            if (hasDocumentedCmdletName && fenceCount == 0)
            {
                report.AddError(
                    ProblemIds.PSL8_NoExamples,
                    $"Cmdlet '{documentedCmdlet}' has no examples. Add at least one '### Title' + fenced PowerShell code block under '## EXAMPLES'.",
                    filePath);
            }
        }

        /// <summary>
        /// Enumerates fenced code blocks that live inside the <c>## EXAMPLES</c> H2 section
        /// (case-insensitive). Tagged fences (<c>```powershell</c>) and bare fences (<c>```</c>)
        /// are both collected; PlatyPS emits the latter by default. Fences in <c>## SYNTAX</c>,
        /// <c>## PARAMETERS</c>, etc. are deliberately skipped — those contain <c>&lt;Type&gt;</c>
        /// placeholders that are not valid PowerShell.
        /// </summary>
        private static IEnumerable<(string Body, int FenceBodyAbsoluteLine)> EnumerateExampleFences(string content)
        {
            // Find the EXAMPLES heading and the next H2 (or EOF) — that span is what we scan.
            var headings = s_h2HeadingRegex.Matches(content);
            for (var i = 0; i < headings.Count; i++)
            {
                var title = headings[i].Groups["title"].Value.Trim();
                if (!string.Equals(title, "EXAMPLES", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var sectionStart = headings[i].Index + headings[i].Length;
                var sectionEnd = i + 1 < headings.Count ? headings[i + 1].Index : content.Length;
                var sectionLength = sectionEnd - sectionStart;

                foreach (Match fence in s_anyFenceInExamplesRegex.Matches(content, sectionStart))
                {
                    if (fence.Index >= sectionEnd)
                    {
                        break;
                    }

                    var bodyGroup = fence.Groups["body"];
                    yield return (bodyGroup.Value, LineOfOffset(content, bodyGroup.Index));
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

            // Absolute line number = (markdown line of fence body start) + (1-based line within body) - 1.
            var lineNumber = fenceBodyAbsoluteLine + command.Extent.StartLineNumber - 1;
            var locator = $"{filePath}:{lineNumber}";

            if (!cmdlets.TryGet(name, out var metadata))
            {
                // Inside a help example, every command leader must be a registered AzClips cmdlet.
                // Native commands (`az`, `dotnet`, `git`), built-in PS cmdlets we don't ship, or
                // plain typos all fall here. We deliberately do not whitelist anything — help docs
                // exist to demonstrate AzClips cmdlets, not arbitrary shell commands.
                report.AddError(
                    ProblemIds.PSL1_UnknownCmdlet,
                    $"Unknown cmdlet '{name}' in PowerShell example. Not registered by any AzClips cmdlet assembly.",
                    locator);
                return;
            }

            // If the command splats (`@params`), we cannot statically see the parameter set — skip parameter checks.
            if (HasSplattedArgument(command))
            {
                return;
            }

            // Track canonical parameter names we've seen on this invocation so we can flag
            // duplicates (PSL4) and compute the missing-required diff (PSL3) at the end.
            var seenCanonical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var elements = command.CommandElements;
            for (var i = 0; i < elements.Count; i++)
            {
                if (elements[i] is not CommandParameterAst element)
                {
                    continue;
                }

                var paramName = element.ParameterName;
                if (string.IsNullOrEmpty(paramName))
                {
                    continue;
                }

                var elementLine = fenceBodyAbsoluteLine + element.Extent.StartLineNumber - 1;
                var elementLocator = $"{filePath}:{elementLine}";

                if (s_commonParameters.Contains(paramName))
                {
                    continue;
                }

                // Resolve to canonical name (alias → declared name) so duplicate detection
                // catches `-Name foo -N bar` when N is an alias of Name.
                string? canonical = null;
                if (metadata.ParameterNames.Contains(paramName))
                {
                    canonical = metadata.ParameterNames.First(n => string.Equals(n, paramName, StringComparison.OrdinalIgnoreCase));
                }
                else if (metadata.ParameterAliases.TryGetValue(paramName, out var aliased))
                {
                    canonical = aliased;
                }
                else
                {
                    report.AddError(
                        ProblemIds.PSL2_UnknownParameter,
                        $"Unknown parameter '-{paramName}' on cmdlet '{name}'. Not declared as a parameter or alias.",
                        elementLocator);
                    continue;
                }

                if (!seenCanonical.Add(canonical))
                {
                    report.AddError(
                        ProblemIds.PSL4_DuplicateParameter,
                        $"Parameter '-{paramName}' (canonical '{canonical}') is bound more than once on cmdlet '{name}'.",
                        elementLocator);
                    continue;
                }

                // Value-type check (PSL6). Only fires when the value is a literal string/numeric
                // constant — variables, expressions, and interpolated strings are out of scope.
                if (TryResolveValueExpression(element, elements, ref i, out var valueExpr)
                    && valueExpr is not null
                    && metadata.ParameterTypes.TryGetValue(canonical, out var declaredType))
                {
                    var valueLine = fenceBodyAbsoluteLine + valueExpr.Extent.StartLineNumber - 1;
                    var valueLocator = $"{filePath}:{valueLine}";
                    CheckValueType(name, canonical, declaredType, valueExpr, valueLocator, report);
                }
            }

            // Required-parameter diff (PSL3). Only fires when the cmdlet declares parameters as
            // Mandatory in AllParameterSets — set-specific Mandatory parameters are not policed.
            foreach (var requiredName in metadata.RequiredParameters)
            {
                if (!seenCanonical.Contains(requiredName))
                {
                    report.AddError(
                        ProblemIds.PSL3_MissingRequiredParameter,
                        $"Required parameter '-{requiredName}' is missing from invocation of cmdlet '{name}'.",
                        locator);
                }
            }
        }

        /// <summary>
        /// Resolves the value expression bound to <paramref name="param"/>: either the
        /// <c>-Foo:value</c> shorthand (carried on the <see cref="CommandParameterAst.Argument"/>)
        /// or the next positional element (e.g. <c>-Foo value</c>). Advances <paramref name="i"/>
        /// past the consumed value so the outer walk does not revisit it.
        /// </summary>
        private static bool TryResolveValueExpression(
            CommandParameterAst param,
            ReadOnlyCollection<CommandElementAst> elements,
            ref int i,
            out ExpressionAst? value)
        {
            // `-Foo:bar` syntax embeds the value on the param node itself.
            if (param.Argument is not null)
            {
                value = param.Argument;
                return true;
            }

            // Switch parameters consume no value. We can't tell switch from non-switch without
            // the parameter type, so callers must filter by type after.
            if (i + 1 < elements.Count && elements[i + 1] is not CommandParameterAst)
            {
                value = elements[i + 1] as ExpressionAst;
                if (value is not null)
                {
                    i++;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static void CheckValueType(
            string cmdletName,
            string canonicalParamName,
            Type declaredType,
            ExpressionAst valueExpr,
            string locator,
            ValidationReport report)
        {
            // Only check literal constants. Variable references, sub-expressions, splatted
            // arguments, and `"$rg-foo"` interpolated strings are skipped — we do not know
            // their runtime values.
            if (valueExpr is not StringConstantExpressionAst literal)
            {
                return;
            }

            var rawValue = literal.Value;

            // [switch] parameters: anything other than `$true`/`$false`/0/1 is a value, and a
            // bare string after `-Force` would actually bind to a different position. We don't
            // try to validate switch values here — too easy to misread.
            if (declaredType == typeof(SwitchParameter))
            {
                return;
            }

            if (declaredType == typeof(Guid))
            {
                if (!Guid.TryParse(rawValue, out _) && !LooksLikePlaceholder(rawValue))
                {
                    report.AddError(
                        ProblemIds.PSL6_InvalidParameterValue,
                        $"Parameter '-{canonicalParamName}' on cmdlet '{cmdletName}' expects a Guid; '{rawValue}' is not a valid Guid.",
                        locator);
                }
                return;
            }

            if (declaredType.IsEnum)
            {
                if (!Enum.GetNames(declaredType).Any(n => string.Equals(n, rawValue, StringComparison.OrdinalIgnoreCase))
                    && !LooksLikePlaceholder(rawValue))
                {
                    var allowed = string.Join(", ", Enum.GetNames(declaredType));
                    report.AddError(
                        ProblemIds.PSL6_InvalidParameterValue,
                        $"Parameter '-{canonicalParamName}' on cmdlet '{cmdletName}' expects one of [{allowed}]; got '{rawValue}'.",
                        locator);
                }
                return;
            }
        }

        private static bool HasSplattedArgument(CommandAst command)
        {
            // `@params` shows up as a VariableExpressionAst whose Splatted property is true.
            return command.CommandElements
                .OfType<VariableExpressionAst>()
                .Any(v => v.Splatted);
        }

        /// <summary>
        /// Heuristic for spotting documentation placeholders like <c>&lt;your-rg-id&gt;</c>,
        /// <c>{guid}</c>, or <c>YOUR-VALUE-HERE</c>. We don't validate those literally because
        /// they're meant to be replaced.
        /// </summary>
        private static bool LooksLikePlaceholder(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            if (value.StartsWith('<') && value.EndsWith('>')) return true;
            if (value.StartsWith('{') && value.EndsWith('}')) return true;
            if (value.Contains("YOUR", StringComparison.Ordinal)) return true;
            return false;
        }

        // PowerShell automatic variables that are always defined. We deliberately keep this list
        // tight — the goal is to catch typos like `$resourceGroup` vs `$resourceGroupName`, not to
        // be a full PowerShell linter.
        private static readonly HashSet<string> s_automaticVariables = new(StringComparer.OrdinalIgnoreCase)
        {
            "_", "PSItem", "args", "input", "null", "true", "false", "this",
            "matches", "error", "pwd", "home", "pid", "psversiontable", "host",
            "lastexitcode", "?", "PSCommandPath", "PSScriptRoot", "MyInvocation",
            "PSCmdlet", "PSBoundParameters", "ExecutionContext", "ForEach", "Switch",
            "StackTrace", "ConsoleFileName", "Event", "EventArgs", "EventSubscriber",
            "Sender", "ShellId", "OFS", "PSCulture", "PSUICulture", "PSDebugContext",
            "PSEdition", "PSHOME", "PSDefaultParameterValues", "PSEmailServer",
            "PSModuleAutoLoadingPreference", "PSSenderInfo", "PSSessionApplicationName",
            "PSSessionConfigurationName", "PSSessionOption", "Profile",
        };

        /// <summary>
        /// Reports references to <see cref="VariableExpressionAst"/> variables that are never
        /// assigned in the same fence. Write-side variables (LHS, loop, param-block,
        /// <c>-OutVariable</c>/<c>-ErrorVariable</c>/etc.), drive-qualified (<c>$env:</c>), and
        /// automatic variables are excluded.
        /// </summary>
        private static void ValidateUnassignedVariables(
            ScriptBlockAst ast,
            string filePath,
            int fenceBodyAbsoluteLine,
            ValidationReport report)
        {
            var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // LHS of assignments: $foo = ...
            foreach (var assign in ast.FindAll(n => n is AssignmentStatementAst, true).Cast<AssignmentStatementAst>())
            {
                CollectDefinedTargets(assign.Left, defined);
            }

            // foreach ($item in $list) { ... }
            foreach (var fe in ast.FindAll(n => n is ForEachStatementAst, true).Cast<ForEachStatementAst>())
            {
                if (fe.Variable.VariablePath is { IsVariable: true } vp)
                {
                    defined.Add(vp.UserPath);
                }
            }

            // param($foo, $bar) blocks anywhere (including nested script blocks).
            foreach (var pb in ast.FindAll(n => n is ParamBlockAst, true).Cast<ParamBlockAst>())
            {
                foreach (var p in pb.Parameters)
                {
                    if (p.Name.VariablePath is { IsVariable: true } vp)
                    {
                        defined.Add(vp.UserPath);
                    }
                }
            }

            // Cmdlet-side variable creators: -OutVariable foo / -ErrorVariable err / -PipelineVariable pv.
            foreach (var cmd in ast.FindAll(n => n is CommandAst, true).Cast<CommandAst>())
            {
                var elements = cmd.CommandElements;
                for (var i = 0; i < elements.Count - 1; i++)
                {
                    if (elements[i] is not CommandParameterAst p)
                    {
                        continue;
                    }
                    if (!s_variableCreatingParameters.Contains(p.ParameterName))
                    {
                        continue;
                    }
                    var argExpr = p.Argument ?? elements[i + 1] as ExpressionAst;
                    if (argExpr is StringConstantExpressionAst lit && !string.IsNullOrEmpty(lit.Value))
                    {
                        defined.Add(lit.Value);
                    }
                }
            }

            // Now walk read-side references.
            foreach (var v in ast.FindAll(n => n is VariableExpressionAst, true).Cast<VariableExpressionAst>())
            {
                // Splatted variables (`@params`) are skipped: the convention in PowerShell help
                // is to define the splat hashtable in surrounding prose or a sibling fence we
                // cannot see, so flagging these would be a guaranteed false positive.
                if (v.Splatted)
                {
                    continue;
                }

                var path = v.VariablePath;
                var userPath = path.UserPath;
                if (string.IsNullOrEmpty(userPath))
                {
                    continue;
                }
                if (userPath.Contains(':'))
                {
                    // Drive- or scope-qualified ($env:FOO, $script:Bar, $global:Baz, $using:x).
                    // Out of scope for unassigned-variable checks.
                    continue;
                }
                if (s_automaticVariables.Contains(userPath))
                {
                    continue;
                }
                if (defined.Contains(userPath))
                {
                    continue;
                }

                var line = fenceBodyAbsoluteLine + v.Extent.StartLineNumber - 1;
                report.AddError(
                    ProblemIds.PSL5_UnassignedVariable,
                    $"Variable '${userPath}' is read but never assigned in this example.",
                    $"{filePath}:{line}");
            }
        }

        // Cmdlet parameters that, when bound to a literal name, *create* a variable in scope.
        private static readonly HashSet<string> s_variableCreatingParameters = new(StringComparer.OrdinalIgnoreCase)
        {
            "OutVariable", "ErrorVariable", "WarningVariable", "InformationVariable", "PipelineVariable",
        };

        private static void CollectDefinedTargets(ExpressionAst target, HashSet<string> defined)
        {
            switch (target)
            {
                case VariableExpressionAst v when v.VariablePath.IsVariable:
                    defined.Add(v.VariablePath.UserPath);
                    break;
                case ArrayLiteralAst arr:
                    // ($a, $b) = 1, 2
                    foreach (var el in arr.Elements)
                    {
                        CollectDefinedTargets(el, defined);
                    }
                    break;
                case ParenExpressionAst paren:
                    // ($foo) = 1 — the inner pipeline ought to wrap a single command-expression.
                    if (paren.Pipeline is PipelineAst pl
                        && pl.PipelineElements.Count == 1
                        && pl.PipelineElements[0] is CommandExpressionAst ce)
                    {
                        CollectDefinedTargets(ce.Expression, defined);
                    }
                    break;
            }
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
