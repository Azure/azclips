// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation;
using Cirrus.Help.Tool.Validation;

namespace Cirrus.Help.Tool.Tests
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

        private static CmdletIndex BuildIndex()
        {
            var index = new CmdletIndex();
            index.AddFromAssembly(typeof(PowerShellExampleSyntaxValidatorTests).Assembly);
            return index;
        }

        private static string Fence(params string[] lines) =>
            "```powershell\n" + string.Join("\n", lines) + "\n```";

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
            // -Keys is a typo (canonical is -Key) — the live regression that motivated 15.4.7.
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

            var errors = report.Errors.ToList();
            Assert.Single(errors);
            Assert.Contains("-Keys", errors[0].Message, StringComparison.Ordinal);
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
            // bash fences are someone else's problem (ExampleSyntaxValidator) — must be ignored here.
            var content = "```bash\nazv2 group create --name foo --bogus value\n```";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldIncludeFilePathAndLineNumber_InErrorLocator()
        {
            // Three lines of preamble push the offending line to absolute line 4.
            var content = "Some prose.\n\n```powershell\nSet-FakeWidget -Keys k -Value v\n```";

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "extensions/foo/help/set.md", BuildIndex());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.FilePath!.EndsWith(":4", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldSkip_NativeCommands()
        {
            // `dotnet build` and similar bare names should not be policed by this validator.
            var content = Fence("dotnet build", "git status");

            var report = new PowerShellExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndex());

            Assert.False(report.HasErrors);
        }
    }
}
