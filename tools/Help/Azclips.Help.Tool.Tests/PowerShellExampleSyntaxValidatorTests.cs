// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation;
using Azclips.Help.Tool.Validation;

namespace Azclips.Help.Tool.Tests
{
    public class PowerShellExampleSyntaxValidatorTests
    {
        // Minimal stub cmdlets the tests reflect over via CmdletIndex.AddFromAssembly.
        // The validator reflects over the executing test assembly, so these are picked up
        // automatically without needing real DLLs on disk.

        [Cmdlet(VerbsCommon.Set, "FakeWidget", SupportsShouldProcess = true)]
        public sealed class SetFakeWidgetCmdlet : System.Management.Automation.Cmdlet
        {
            [Parameter(Mandatory = true)]
            public string Key { get; set; } = string.Empty;

            [Parameter(Mandatory = true)]
            public string Value { get; set; } = string.Empty;

            [Parameter]
            public SwitchParameter Force { get; set; }
        }

        [Cmdlet(VerbsCommon.Get, "FakeWidget")]
        public sealed class GetFakeWidgetCmdlet : System.Management.Automation.Cmdlet
        {
            [Parameter(Mandatory = true)]
            [Alias("Path", "P")]
            public string LiteralPath { get; set; } = string.Empty;
        }

        public enum FakeWidgetMode { Read, Write, Append }

        [Cmdlet(VerbsCommon.New, "FakeWidget")]
        public sealed class NewFakeWidgetCmdlet : System.Management.Automation.Cmdlet
        {
            [Parameter(Mandatory = true)]
            public Guid SubscriptionId { get; set; }

            [Parameter(Mandatory = true)]
            public FakeWidgetMode Mode { get; set; }

            [Parameter]
            public string Tag { get; set; } = string.Empty;
        }

        private static CmdletIndex BuildIndex()
        {
            var index = new CmdletIndex();
            index.AddFromAssembly(typeof(PowerShellExampleSyntaxValidatorTests).Assembly);
            return index;
        }

        // The validator only walks fences that live inside `## EXAMPLES`. Tests that exercise
        // rule behaviour wrap their fence body in that section so we don't have to repeat the
        // heading in every test. The synthetic filename (`fake.md`) does not match `Verb-Noun`,
        // so PSL7 does not fire on these unit tests — that rule is covered separately.
        private static string Fence(params string[] lines) =>
            "## EXAMPLES\n\n```powershell\n" + string.Join("\n", lines) + "\n```";

        [Fact]
        public void Validate_ShouldPass_WhenCmdletAndParametersAreKnown()
        {
            var content = Fence("Set-FakeWidget -Key \"output.format\" -Value \"json\"");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldError_WhenCmdletIsUnknown()
        {
            var content = Fence("Set-Unknown -Key foo -Value bar");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.Message.Contains("Unknown cmdlet 'Set-Unknown'", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldError_WhenParameterIsUnknown()
        {
            // -Keys is a typo (canonical is -Key)
            var content = Fence("Set-FakeWidget -Keys \"output.format\" -Value \"json\"");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.Message.Contains("Unknown parameter '-Keys'", StringComparison.Ordinal) &&
                e.Message.Contains("Set-FakeWidget", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldAcceptCommonParameters()
        {
            var content = Fence("Set-FakeWidget -Key k -Value v -Verbose -ErrorAction Stop -WhatIf");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldAcceptDeclaredAlias()
        {
            // GetFakeWidgetCmdlet declares [Alias("Path","P")] on LiteralPath.
            var content = Fence(
                "Get-FakeWidget -Path \"x\"",
                "Get-FakeWidget -P \"y\"",
                "Get-FakeWidget -LiteralPath \"z\"");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldIgnore_CommentAndPromptLines()
        {
            var content = Fence(
                "# This is a comment",
                "PS> Set-FakeWidget -Key k -Value v",
                "PS C:\\> Get-FakeWidget -LiteralPath x");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldOnlyFailOffendingLine_InMultiLineBlock()
        {
            var content = Fence(
                "Set-FakeWidget -Key a -Value b",
                "Set-FakeWidget -Keys c -Value d");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            // Line 2 is the offending line. Multiple rules may fire on it (PSL2 unknown -Keys,
            // PSL3 missing required -Key) — we only care that line 1 stays clean and that the
            // typo'd flag is reported.
            var errors = report.Errors.ToList();
            Assert.NotEmpty(errors);
            // Fence preamble inserted by the helper: line 1 = `## EXAMPLES`, line 2 = blank,
            // line 3 = ```powershell, line 4 = first body line, line 5 = second (offending) line.
            Assert.All(errors, e => Assert.EndsWith(":5", e.FilePath!));
            Assert.Contains(errors, e => e.Message.Contains("-Keys", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldHandle_BacktickContinuation()
        {
            // Backtick-continued line — the AST treats the two lines as one CommandAst.
            var content = Fence(
                "Set-FakeWidget `",
                "  -Key \"a\" `",
                "  -Value \"b\"");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldSkipParameterCheck_OnSplattedCommand()
        {
            // We can't see inside @params at static-analysis time; L2 is skipped, L1 still runs.
            var content = Fence("Get-FakeWidget @params");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldNotAffect_BashFences()
        {
            // bash fences are someone else's problem (ExampleSyntaxValidator) — must be ignored here
            // even when they sit inside the ## EXAMPLES section.
            var content = "## EXAMPLES\n\n```bash\naz group create --name foo --bogus value\n```";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldIncludeFilePathAndLineNumber_InErrorLocator()
        {
            // Lines 1–3: prose. Line 4: `## EXAMPLES`. Line 5: blank. Line 6: ```powershell.
            // Line 7: the offending invocation.
            var content = "Some prose.\n\n\n## EXAMPLES\n\n```powershell\nSet-FakeWidget -Keys k -Value v\n```";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "extensions/foo/help/set.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.FilePath!.EndsWith(":7", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldError_OnNativeCommand()
        {
            // Help docs are for cmdlets, not arbitrary shell commands. Anything the parser sees
            // as a CommandAst leader must resolve to a registered AzClips cmdlet (PSL1).
            // This is the rule that catches `az ...` baked into a PowerShell help example.
            var content = Fence("az login --subscription-id 00000000-0000-0000-0000-000000000000");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.PSL1_UnknownCmdlet
                && e.Message.Contains("az", StringComparison.Ordinal));
        }

        // ----- PSL3: Required parameter missing -----

        [Fact]
        public void Validate_ShouldError_WhenRequiredParameterIsMissing()
        {
            // -Value is Mandatory; omit it.
            var content = Fence("Set-FakeWidget -Key foo");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.PSL3_MissingRequiredParameter
                && e.Message.Contains("-Value", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldNotFlagRequiredMissing_WhenAllRequiredAreSupplied()
        {
            var content = Fence("Set-FakeWidget -Key k -Value v");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL3_MissingRequiredParameter);
        }

        [Fact]
        public void Validate_ShouldNotFlagRequiredMissing_OnSplattedInvocation()
        {
            // PSL3 must not fire when params are splatted — we cannot see inside @hash.
            var content = Fence("Set-FakeWidget @widgetParams");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL3_MissingRequiredParameter);
        }

        [Fact]
        public void Validate_ShouldRecognizeRequiredViaAlias()
        {
            // -Path is an alias for the Mandatory -LiteralPath; supplying the alias must satisfy PSL3.
            var content = Fence("Get-FakeWidget -Path foo");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL3_MissingRequiredParameter);
        }

        // ----- PSL4: Duplicate parameter -----

        [Fact]
        public void Validate_ShouldError_WhenSameParameterAppearsTwice()
        {
            var content = Fence("Set-FakeWidget -Key a -Value v -Key b");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.PSL4_DuplicateParameter
                && e.Message.Contains("-Key", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldError_WhenAliasAndCanonicalNameCollide()
        {
            // -Path is an alias of -LiteralPath; using both is still a duplicate bind.
            var content = Fence("Get-FakeWidget -LiteralPath x -Path y");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.Contains(report.Errors, e => e.ProblemId == ProblemIds.PSL4_DuplicateParameter);
        }

        // ----- PSL5: Unassigned variable -----

        [Fact]
        public void Validate_ShouldError_WhenVariableIsReadButNeverAssigned()
        {
            // $resourceGroup is never assigned in this fence.
            var content = Fence("Set-FakeWidget -Key $resourceGroup -Value v");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.PSL5_UnassignedVariable
                && e.Message.Contains("$resourceGroup", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldPass_WhenVariableIsAssignedBeforeUse()
        {
            var content = Fence(
                "$rg = \"my-rg\"",
                "Set-FakeWidget -Key $rg -Value v");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL5_UnassignedVariable);
        }

        [Fact]
        public void Validate_ShouldNotFlag_AutomaticVariables()
        {
            // $_, $args, $true, $null, etc. are always defined.
            var content = Fence(
                "1, 2, 3 | ForEach-Object { Set-FakeWidget -Key $_ -Value $true }");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL5_UnassignedVariable);
        }

        [Fact]
        public void Validate_ShouldNotFlag_DriveQualifiedVariables()
        {
            // $env:HOME and similar drive-qualified vars are out of scope.
            var content = Fence("Set-FakeWidget -Key $env:USERNAME -Value $script:foo");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL5_UnassignedVariable);
        }

        [Fact]
        public void Validate_ShouldNotFlag_ForeachLoopVariable()
        {
            var content = Fence(
                "foreach ($item in 1, 2, 3) { Set-FakeWidget -Key $item -Value v }");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL5_UnassignedVariable);
        }

        [Fact]
        public void Validate_ShouldNotFlag_OutVariableTarget()
        {
            // -OutVariable creates `$result` in scope.
            var content = Fence(
                "Get-FakeWidget -LiteralPath x -OutVariable result",
                "Set-FakeWidget -Key foo -Value $result");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL5_UnassignedVariable);
        }

        // ----- PSL6: Invalid parameter value -----

        [Fact]
        public void Validate_ShouldError_WhenGuidParameterReceivesNonGuidLiteral()
        {
            var content = Fence(
                "New-FakeWidget -SubscriptionId not-a-guid -Mode Read");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.PSL6_InvalidParameterValue
                && e.Message.Contains("Guid", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldPass_WhenGuidParameterReceivesValidGuid()
        {
            var content = Fence(
                "New-FakeWidget -SubscriptionId 11111111-2222-3333-4444-555555555555 -Mode Read");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL6_InvalidParameterValue);
        }

        [Fact]
        public void Validate_ShouldPass_WhenGuidParameterReceivesPlaceholder()
        {
            // Documentation placeholders are not literal values; do not flag them.
            var content = Fence(
                "New-FakeWidget -SubscriptionId <your-subscription-id> -Mode Read");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL6_InvalidParameterValue);
        }

        [Fact]
        public void Validate_ShouldError_WhenEnumParameterReceivesUndeclaredMember()
        {
            var content = Fence(
                "New-FakeWidget -SubscriptionId 11111111-2222-3333-4444-555555555555 -Mode Sideways");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.PSL6_InvalidParameterValue
                && e.Message.Contains("Sideways", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldPass_WhenEnumParameterReceivesValidMember_CaseInsensitive()
        {
            var content = Fence(
                "New-FakeWidget -SubscriptionId 11111111-2222-3333-4444-555555555555 -Mode write");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL6_InvalidParameterValue);
        }

        [Fact]
        public void Validate_ShouldNotCheckValueType_WhenValueIsAVariable()
        {
            // $sub is a runtime expression — we cannot statically validate its value.
            var content = Fence(
                "$sub = (Get-AzContext).Subscription.Id",
                "New-FakeWidget -SubscriptionId $sub -Mode Read");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL6_InvalidParameterValue);
        }

        // ----- PSL7: example must invoke documented cmdlet -----

        [Fact]
        public void Validate_ShouldError_WhenExampleDoesNotInvokeDocumentedCmdlet()
        {
            // File is named after Set-FakeWidget but the example uses Get-FakeWidget.
            var content = Fence("Get-FakeWidget -LiteralPath x");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.PSL7_ExampleMustInvokeDocumentedCmdlet
                && e.Message.Contains("Set-FakeWidget", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldError_WhenExampleIsNativeCommand_InCmdletHelpFile()
        {
            // The bug Copilot caught: PowerShell help for Connect-AzAccount whose example
            // is `az login ...`. Both PSL1 (az not a cmdlet) and PSL7 (Connect-AzAccount
            // not invoked) must fire.
            var content = Fence("az login --subscription-id 00000000-0000-0000-0000-000000000000");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.ProblemId == ProblemIds.PSL1_UnknownCmdlet);
            Assert.Contains(report.Errors, e => e.ProblemId == ProblemIds.PSL7_ExampleMustInvokeDocumentedCmdlet);
        }

        [Fact]
        public void Validate_ShouldPass_WhenSetupCmdletPrecedesDocumentedCmdletInSameFence()
        {
            // Realistic: set up a resource with one cmdlet, then demonstrate the documented one.
            // PSL7 fires per-fence, not per-line — as long as the documented cmdlet appears at
            // least once in the same fence, the example is fine.
            var content = Fence(
                "$widget = Set-FakeWidget -Key foo -Value bar",
                "Get-FakeWidget -LiteralPath $widget.Path");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Get-FakeWidget.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL7_ExampleMustInvokeDocumentedCmdlet);
        }

        [Fact]
        public void Validate_ShouldNotEnforce_WhenFilenameIsNotVerbNounShape()
        {
            // Synthetic test files are usually `fake.md` — they can't satisfy PSL7 because there
            // is no cmdlet named `fake`. PSL7 silently disables itself in that case so unit-test
            // helpers don't have to know about it.
            var content = Fence("Set-FakeWidget -Key k -Value v");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL7_ExampleMustInvokeDocumentedCmdlet);
        }

        // ----- bare-fence support (PlatyPS default emits ``` not ```powershell) -----

        [Fact]
        public void Validate_ShouldWalk_BareFences_InsideExamplesSection()
        {
            // PlatyPS emits bare fences by default. They must still be validated so authoring
            // bugs (like `az ...` inside a cmdlet help file) cannot hide.
            var content =
                "## EXAMPLES\n\n" +
                "### Example 1\n```\n" +
                "Set-FakeWidget -Keys k -Value v\n" +
                "```";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.Contains(report.Errors, e => e.ProblemId == ProblemIds.PSL2_UnknownParameter);
        }

        [Fact]
        public void Validate_ShouldIgnore_FencesOutsideExamplesSection()
        {
            // ## SYNTAX in PlatyPS output contains pseudocode like `Set-FakeWidget [-Key <String>]`
            // — `<String>` does not parse, and even when it does the surface syntax isn't a real
            // invocation. Fences in any section other than `## EXAMPLES` are skipped.
            var content =
                "## SYNTAX\n\n```\n" +
                "Set-FakeWidget -Keys <String>\n" +
                "```\n\n" +
                "## EXAMPLES\n\n```\n" +
                "Set-FakeWidget -Key k -Value v\n" +
                "```";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.False(report.HasErrors);
        }

        // ----- PSL8: cmdlet help file must contain at least one example fence -----

        [Fact]
        public void Validate_ShouldError_WhenNoFencesUnderExamples()
        {
            // PlatyPS scaffolds `### <Title>` headings without bodies when the cmdlet has no
            // example metadata. Those render as empty sections in `Get-Help -Examples`; PSL8
            // forces authors to fill them in.
            var content =
                "## EXAMPLES\n\n" +
                "### Get a widget\n\n" +
                "### List widgets\n\n" +
                "## PARAMETERS\n";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.PSL8_NoExamples &&
                e.Message.Contains("Set-FakeWidget", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldNotEmitPSL8_WhenAtLeastOneFenceExists()
        {
            var content = Fence("Set-FakeWidget -Key k -Value v");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL8_NoExamples);
        }

        [Fact]
        public void Validate_ShouldNotEmitPSL8_WhenFilenameIsNotVerbNounShape()
        {
            // Synthetic helper files (`fake.md`, README fragments, etc.) shouldn't be forced to
            // carry examples — PSL8 mirrors PSL7's filename guard.
            var content = "## EXAMPLES\n\n### Empty\n\n## PARAMETERS\n";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL8_NoExamples);
        }

        // ---- TODO placeholder rule (PS) -------------------------------------------------

        [Fact]
        public void Validate_ShouldErrorTodo_WhenSynopsisContainsTodoMarker()
        {
            // PlatyPS scaffolder seeds: `## SYNOPSIS\n\nTODO - one-line summary of <cmd>.`
            var content =
                "## SYNOPSIS\n\nTODO - one-line summary of Set-FakeWidget.\n\n" +
                "## EXAMPLES\n\n```powershell\nSet-FakeWidget -Key k -Value v\n```\n";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.Contains(report.Errors, e => e.ProblemId == ProblemIds.PS_TodoPlaceholder);
        }

        [Fact]
        public void Validate_ShouldErrorTodo_WhenExampleBodyContainsTodoMarker()
        {
            // Scaffolder also seeds per-example bodies: `TODO - describe the example.`
            var content =
                "## EXAMPLES\n\n" +
                "### Example 1\n\nTODO - describe the example.\n\n" +
                "```powershell\nSet-FakeWidget -Key k -Value v\n```\n";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.Contains(report.Errors, e => e.ProblemId == ProblemIds.PS_TodoPlaceholder);
        }

        [Fact]
        public void Validate_ShouldNotErrorTodo_WhenNoTodoMarkers()
        {
            var content = Fence("Set-FakeWidget -Key k -Value v");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PS_TodoPlaceholder);
        }

        // ---- angle-bracket placeholder rule (PS) ----------------------------------------

        [Fact]
        public void Validate_ShouldErrorPlaceholder_WhenExampleFenceContainsAngleBrackets()
        {
            // PlatyPS scaffolder emits `Set-FakeWidget -Key <key> -Value <value>` style stubs.
            var content = Fence("Set-FakeWidget -Key <key> -Value <value>");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            // Two placeholders, two errors — gives authors visibility on every gap.
            var placeholders = report.Errors
                .Where(e => e.ProblemId == ProblemIds.PS_ExamplePlaceholder)
                .ToList();
            Assert.Equal(2, placeholders.Count);
        }

        [Fact]
        public void Validate_ShouldNotErrorPlaceholder_WhenExampleUsesShellVariableOrSplat()
        {
            // `$variable` and splatted `@params` are the documented escape hatches.
            var content =
                "## EXAMPLES\n\n```powershell\n" +
                "$key = \"output.format\"\n" +
                "$value = \"json\"\n" +
                "Set-FakeWidget -Key $key -Value $value\n" +
                "$params = @{ Key = $key; Value = $value }\n" +
                "Set-FakeWidget @params\n" +
                "```\n";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PS_ExamplePlaceholder);
        }

        [Fact]
        public void Validate_ShouldNotErrorPlaceholder_WhenAngleBracketIsInSynopsisProse()
        {
            // Prose outside `## EXAMPLES` is not policed (matches CLI behaviour).
            var content =
                "## SYNOPSIS\n\nUse this cmdlet to set <key>=<value> on a widget.\n\n" +
                "## EXAMPLES\n\n```powershell\nSet-FakeWidget -Key k -Value v\n```\n";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PS_ExamplePlaceholder);
        }

        // ---- PSL12: PlatyPS '{{ Fill ... }}' placeholder rule ---------------------------

        [Fact]
        public void Validate_ShouldErrorFillPlaceholder_WhenParameterDescriptionIsScaffoldStub()
        {
            // PlatyPS seeds parameter descriptions with `{{ Fill ... }}` until authored.
            var content =
                "## SYNOPSIS\n\nSet a widget value.\n\n" +
                "## EXAMPLES\n\n```powershell\nSet-FakeWidget -Key k -Value v\n```\n\n" +
                "## PARAMETERS\n\n### -Key\n\n{{ Fill Key Description }}\n\n" +
                "### -Value\n\n{{ Fill Value Description }}\n";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            var fills = report.Errors
                .Where(e => e.ProblemId == ProblemIds.PSL12_FillPlaceholder)
                .ToList();
            Assert.Equal(2, fills.Count);
        }

        [Fact]
        public void Validate_ShouldErrorFillPlaceholder_WhenNotesAreScaffoldStub()
        {
            // PlatyPS seeds NOTES and RELATED LINKS sections with `{{ Fill in the ... }}`.
            var content =
                "## SYNOPSIS\n\nSet a widget value.\n\n" +
                "## EXAMPLES\n\n```powershell\nSet-FakeWidget -Key k -Value v\n```\n\n" +
                "## NOTES\n\n{{ Fill in the Notes }}\n\n" +
                "## RELATED LINKS\n\n{{ Fill in the related links here }}\n";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            var fills = report.Errors
                .Where(e => e.ProblemId == ProblemIds.PSL12_FillPlaceholder)
                .ToList();
            Assert.Equal(2, fills.Count);
            Assert.All(fills, error => Assert.Contains(
                "For optional sections such as Notes, remove the placeholder if no content applies; retain the heading.",
                error.Message));
        }

        [Fact]
        public void Validate_ShouldNotErrorFillPlaceholder_WhenAliasInsertBoilerplateIsPresent()
        {
            // `{{Insert list of aliases}}` is evergreen PlatyPS boilerplate; deliberately skipped.
            var content =
                "## ALIASES\n\nThis cmdlet has the following aliases,\n  {{Insert list of aliases}}\n\n" +
                "## EXAMPLES\n\n```powershell\nSet-FakeWidget -Key k -Value v\n```\n";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL12_FillPlaceholder);
        }

        [Fact]
        public void Validate_ShouldReportLineNumber_WhenFillPlaceholderFires()
        {
            // Location string must include a line number; mirrors `<file>:<line>`.
            var content =
                "line 1\n" +
                "line 2\n" +
                "{{ Fill Key Description }}\n";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            var fill = report.Errors.Single(e => e.ProblemId == ProblemIds.PSL12_FillPlaceholder);
            Assert.EndsWith(":3", fill.FilePath);
        }

        [Fact]
        public void Validate_ShouldNotErrorFillPlaceholder_WhenContentIsAuthored()
        {
            var content = Fence("Set-FakeWidget -Key k -Value v");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "Set-FakeWidget.md", BuildIndex());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL12_FillPlaceholder);
        }
    }
}
