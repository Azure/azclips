// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cirrus.Help.Tool.Validation;

namespace Cirrus.Help.Tool.Tests
{
    public class MarkdownStructureValidatorTests
    {
        private static readonly string s_validMarkdown = string.Join("\n",
            "Summary: One-line description.",
            "Description: Multi-line",
            "  description.",
            "",
            "## Examples",
            "",
            "### Example title",
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
        public void ValidateContent_ShouldReportError_WhenSummaryFieldIsMissing()
        {
            var content = s_validMarkdown.Replace("Summary: One-line description.\n", "", StringComparison.Ordinal);

            var report = new MarkdownStructureValidator().ValidateContent(content, "test.md");

            Assert.Contains(report.Errors, e => e.Message.Contains("'Summary:'", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldReportError_WhenDescriptionFieldIsMissing()
        {
            var content = string.Join("\n",
                "Summary: x",
                "",
                "## Examples",
                "",
                "### t",
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
                "Summary: x",
                "Description: y",
                "");

            var report = new MarkdownStructureValidator().ValidateContent(content, "test.md");

            Assert.Contains(report.Errors, e => e.Message.Contains("Missing examples", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateContent_ShouldReportError_WhenCodeFencesAreUnbalanced()
        {
            var content = string.Join("\n",
                "Summary: x",
                "Description: y",
                "",
                "### Example",
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
            var report = new MarkdownStructureValidator().ValidateContent(string.Empty, "src/Extensions/Foo/help/bar.md");

            Assert.NotEmpty(report.Errors);
            Assert.All(report.Errors, e => Assert.Equal("src/Extensions/Foo/help/bar.md", e.FilePath));
        }

        [Fact]
        public void ValidateContent_ShouldReportAllIssues_WhenMultipleFieldsAreMissing()
        {
            var report = new MarkdownStructureValidator().ValidateContent(string.Empty, "test.md");

            // Empty content → missing Summary + Description + examples = 3 errors
            Assert.Equal(3, report.Errors.Count());
        }

        [Fact]
        public void Validate_ShouldReportError_WhenExtensionsDirectoryDoesNotExist()
        {
            using var tempDir = new TempDirectory();

            var report = new MarkdownStructureValidator().Validate(tempDir.Path);

            Assert.Contains(report.Errors, e => e.Message.Contains("Extensions directory not found", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldReportError_WhenNoHelpMarkdownFilesExist()
        {
            using var tempDir = new TempDirectory();
            Directory.CreateDirectory(Path.Combine(tempDir.Path, "src", "Extensions"));

            var report = new MarkdownStructureValidator().Validate(tempDir.Path);

            Assert.Contains(report.Errors, e => e.Message.Contains("No help markdown files", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldWalkAllHelpFiles_WhenMultipleExtensionsExist()
        {
            using var tempDir = new TempDirectory();
            var loginHelp = Path.Combine(tempDir.Path, "src", "Extensions", "Login", "help");
            var configHelp = Path.Combine(tempDir.Path, "src", "Extensions", "Config", "help");
            Directory.CreateDirectory(loginHelp);
            Directory.CreateDirectory(configHelp);

            File.WriteAllText(Path.Combine(loginHelp, "index.md"), s_validMarkdown);
            File.WriteAllText(Path.Combine(configHelp, "index.md"), string.Empty); // invalid

            var report = new MarkdownStructureValidator().Validate(tempDir.Path);

            // The login file is well-formed, but config/index.md has 3 issues.
            Assert.True(report.HasErrors);
            Assert.All(report.Errors, e => Assert.Contains("Config", e.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }
    }
}
