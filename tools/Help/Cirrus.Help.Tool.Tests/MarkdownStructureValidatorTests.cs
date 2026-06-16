// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cirrus.Help.Tool.Validation;

namespace Cirrus.Help.Tool.Tests
{
    public class MarkdownStructureValidatorTests
    {
        private static readonly string s_validMarkdown = string.Join("\n",
            "---",
            "summary: One-line description.",
            "status: ga",
            "schema: 1.0.0",
            "---",
            "",
            "Description: Multi-line",
            "  description.",
            "",
            "## Examples",
            "",
            "### Example title (CLI)",
            "```bash",
            "azv2 login --subscription-id 00000000-0000-0000-0000-000000000000",
            "```",
            "");

        [Fact]
        public void ValidateContent_ShouldReportNoErrors_WhenMarkdownIsWellFormed()
        {
            var report = new MarkdownStructureValidator().ValidateContent(s_validMarkdown, "test.md");

            Assert.False(report.HasErrors);
            Assert.Empty(report.Errors);
        }

        [Fact]
        public void ValidateContent_ShouldReportError_WhenFrontmatterBlockIsMissing()
        {
            // Every help file must open with a '---' delimited frontmatter block.
            var content = string.Join("\n",
                "Description: y",
                "",
                "### t (CLI)",
                "```bash",
                "azv2 x",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "test.md");

            Assert.Contains(report.Errors, e => e.Message.Contains("frontmatter", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ValidateContent_ShouldReportError_WhenSummaryFieldIsMissing()
        {
            var content = string.Join("\n",
                "---",
                "schema: 1.0.0",
                "---",
                "",
                "Description: y",
                "",
                "### t (CLI)",
                "```bash",
                "azv2 x",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "test.md");

            Assert.Contains(report.Errors, e => e.Message.Contains("'summary:'", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldReportError_WhenSchemaFieldIsMissing()
        {
            var content = string.Join("\n",
                "---",
                "summary: x",
                "---",
                "",
                "Description: y",
                "",
                "### t (CLI)",
                "```bash",
                "azv2 x",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "test.md");

            Assert.Contains(report.Errors, e => e.Message.Contains("'schema:'", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldReportError_WhenStatusIsInvalid()
        {
            var content = string.Join("\n",
                "---",
                "summary: x",
                "status: stable",                // not one of ga|preview|deprecated|internal
                "schema: 1.0.0",
                "---",
                "",
                "Description: y",
                "",
                "### t (CLI)",
                "```bash",
                "azv2 x",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "test.md");

            Assert.Contains(report.Errors, e => e.Message.Contains("status", StringComparison.OrdinalIgnoreCase) && e.Message.Contains("stable", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldAcceptStatusDefault_WhenStatusOmitted()
        {
            // status defaults to "ga" inside the parser, so omitting it is fine.
            var content = string.Join("\n",
                "---",
                "summary: x",
                "schema: 1.0.0",
                "---",
                "",
                "Description: y",
                "",
                "### t (CLI)",
                "```bash",
                "azv2 x",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "test.md");

            Assert.DoesNotContain(report.Errors, e => e.Message.Contains("status", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ValidateContent_ShouldReportError_WhenDescriptionFieldIsMissing()
        {
            var content = string.Join("\n",
                "---",
                "summary: x",
                "schema: 1.0.0",
                "---",
                "",
                "### t (CLI)",
                "```bash",
                "azv2 x",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "test.md");

            Assert.Contains(report.Errors, e => e.Message.Contains("'Description:'", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldReportError_WhenNoExamplesArePresent()
        {
            var content = string.Join("\n",
                "---",
                "summary: x",
                "schema: 1.0.0",
                "---",
                "",
                "Description: y",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "test.md");

            Assert.Contains(report.Errors, e => e.Message.Contains("Missing examples", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldReportError_WhenCodeFencesAreUnbalanced()
        {
            var content = string.Join("\n",
                "---",
                "summary: x",
                "schema: 1.0.0",
                "---",
                "",
                "Description: y",
                "",
                "### Example (CLI)",
                "```bash",
                "azv2 x",
                // missing closing fence
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "test.md");

            Assert.Contains(report.Errors, e => e.Message.Contains("Unbalanced", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldIncludeFilePath_WhenReportingErrors()
        {
            var report = new MarkdownStructureValidator().ValidateContent(string.Empty, "src/CLI/help/foo/bar.md");

            Assert.NotEmpty(report.Errors);
            Assert.All(report.Errors, e => Assert.Equal("src/CLI/help/foo/bar.md", e.FilePath));
        }

        [Fact]
        public void ValidateContent_ShouldReportAllIssues_WhenMultipleFieldsAreMissing()
        {
            var report = new MarkdownStructureValidator().ValidateContent(string.Empty, "test.md");

            // Empty content → missing frontmatter + missing Description + missing examples = 3 errors.
            Assert.Equal(3, report.Errors.Count());
        }

        [Fact]
        public void Validate_ShouldReportError_WhenHelpDirectoryDoesNotExist()
        {
            using var tempDir = new TempDirectory();

            var report = new MarkdownStructureValidator().Validate(tempDir.Path);

            Assert.Contains(report.Errors, e => e.Message.Contains("CLI help directory not found", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldReportError_WhenNoHelpMarkdownFilesExist()
        {
            using var tempDir = new TempDirectory();
            Directory.CreateDirectory(Path.Combine(tempDir.Path, "src", "CLI", "Help", "Docs"));

            var report = new MarkdownStructureValidator().Validate(tempDir.Path);

            Assert.Contains(report.Errors, e => e.Message.Contains("No help markdown files", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldWalkAllHelpFiles_WhenMultipleExtensionsExist()
        {
            using var tempDir = new TempDirectory();
            var loginHelp = Path.Combine(tempDir.Path, "src", "CLI", "Help", "Docs", "login");
            var configHelp = Path.Combine(tempDir.Path, "src", "CLI", "Help", "Docs", "config");
            Directory.CreateDirectory(loginHelp);
            Directory.CreateDirectory(configHelp);

            File.WriteAllText(Path.Combine(loginHelp, "index.md"), s_validMarkdown);
            File.WriteAllText(Path.Combine(configHelp, "index.md"), string.Empty); // invalid

            var report = new MarkdownStructureValidator().Validate(tempDir.Path);

            // The login file is well-formed, but config/index.md has issues.
            Assert.True(report.HasErrors);
            Assert.All(report.Errors, e => Assert.Contains("config", e.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }

        // ---- TODO placeholder rule -------------------------------------------------------

        [Fact]
        public void ValidateContent_ShouldErrorTodo_WhenSummaryContainsTodoMarker()
        {
            // Scaffolder seeds `summary: TODO \u2014 short summary of \`azv2 vm list\`.`
            var content = string.Join("\n",
                "---",
                "summary: TODO \u2014 short summary.",
                "schema: 1.0.0",
                "---",
                "",
                "Description: Real description.",
                "",
                "### List",
                "```bash",
                "azv2 vm list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "vm/list.md");

            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.MD_TodoPlaceholder
                && e.Message.Contains("'summary:'", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldErrorTodo_WhenDescriptionContainsTodoMarker()
        {
            var content = string.Join("\n",
                "---",
                "summary: Real one-liner.",
                "schema: 1.0.0",
                "---",
                "",
                "Description: TODO \u2014 describe what this does.",
                "",
                "### List",
                "```bash",
                "azv2 vm list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "vm/list.md");

            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.MD_TodoPlaceholder
                && e.Message.Contains("'Description:'", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldErrorTodo_WhenExampleFenceContainsTodoMarker()
        {
            // Authors sometimes leave a TODO comment inside an example body.
            var content = string.Join("\n",
                "---",
                "summary: Real summary.",
                "schema: 1.0.0",
                "---",
                "",
                "Description: Real description.",
                "",
                "### List",
                "```bash",
                "# TODO replace with real value",
                "azv2 vm list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "vm/list.md");

            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.MD_TodoPlaceholder
                && e.Message.Contains("Example fence", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldNotErrorTodo_WhenNoTodoMarkers()
        {
            var report = new MarkdownStructureValidator().ValidateContent(s_validMarkdown, "test.md");

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.MD_TodoPlaceholder);
        }

        // ---- angle-bracket placeholder rule ----------------------------------------------

        [Fact]
        public void ValidateContent_ShouldErrorPlaceholder_WhenExampleFenceContainsTypeNamePlaceholder()
        {
            // The exact shape the scaffolder emits: --param <typename>.
            var content = string.Join("\n",
                "---",
                "summary: Real summary.",
                "schema: 1.0.0",
                "---",
                "",
                "Description: Real description.",
                "",
                "### List",
                "```bash",
                "azv2 vm list --resource-group <string> --subscription-id <string>",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "vm/list.md");

            var placeholders = report.Errors
                .Where(e => e.ProblemId == ProblemIds.CLI_ExamplePlaceholder)
                .ToList();

            // Each occurrence is reported individually so every gap surfaces in one CI pass.
            Assert.Equal(2, placeholders.Count);
            Assert.All(placeholders, e => Assert.Contains("<string>", e.Message, StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldErrorPlaceholder_WhenDomainShapedAngleBracketUsed()
        {
            // Per-spec: any <...> in a CLI fence is rejected, including <my-rg> / <name>.
            var content = string.Join("\n",
                "---",
                "summary: Real summary.",
                "schema: 1.0.0",
                "---",
                "",
                "Description: Real description.",
                "",
                "### Show",
                "```bash",
                "azv2 group show --name <my-rg> --subscription-id <subscription-id>",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "group/show.md");

            var placeholders = report.Errors
                .Where(e => e.ProblemId == ProblemIds.CLI_ExamplePlaceholder)
                .ToList();

            Assert.Equal(2, placeholders.Count);
        }

        [Fact]
        public void ValidateContent_ShouldNotErrorPlaceholder_WhenExampleUsesShellVariableOrZeroGuid()
        {
            // Allowed escape hatches: $var, ${var}, $env:NAME, all-zero GUID.
            var content = string.Join("\n",
                "---",
                "summary: Real summary.",
                "schema: 1.0.0",
                "---",
                "",
                "Description: Real description.",
                "",
                "### Create",
                "```bash",
                "rg=myrg",
                "azv2 group create --name $rg --location eastus --subscription-id 00000000-0000-0000-0000-000000000000",
                "azv2 group create --name ${RG_VAR} --location $env:LOCATION --subscription-id 00000000-0000-0000-0000-000000000000",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "group/create.md");

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.CLI_ExamplePlaceholder);
        }

        [Fact]
        public void ValidateContent_ShouldNotErrorPlaceholder_WhenAngleBracketIsInProseNotFence()
        {
            // The Description body can mention `<name>` as a parameter identifier in prose.
            // Only example fences are policed.
            var content = string.Join("\n",
                "---",
                "summary: Real summary.",
                "schema: 1.0.0",
                "---",
                "",
                "Description: Pass a kebab-cased value via --name <name>.",
                "",
                "### List",
                "```bash",
                "azv2 vm list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "vm/list.md");

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.CLI_ExamplePlaceholder);
        }

        [Fact]
        public void ValidateContent_ShouldNotErrorPlaceholder_WhenFenceIsPowerShell()
        {
            // PS fences in CLI markdown shouldn't fire CLI_ExamplePlaceholder; the PS validator
            // owns those (in src/PS/<Mod>/help/*.md). CLI markdown should not contain PS fences
            // in practice, but if it does we don't double-fire.
            var content = string.Join("\n",
                "---",
                "summary: Real summary.",
                "schema: 1.0.0",
                "---",
                "",
                "Description: Real description.",
                "",
                "### From PowerShell",
                "```powershell",
                "Get-VM -Name <vmname>",
                "```",
                "",
                "### From bash",
                "```bash",
                "azv2 vm list --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000",
                "```",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "vm/list.md");

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.CLI_ExamplePlaceholder);
        }
    }
}
