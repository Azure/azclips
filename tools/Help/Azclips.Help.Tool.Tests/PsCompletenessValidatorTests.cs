// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azclips.Help.Tool.Validation;

namespace Azclips.Help.Tool.Tests
{
    public class PsCompletenessValidatorTests
    {
        // ---- Setup helpers ---------------------------------------------------------------

        /// <summary>
        /// Builds a minimal repo-root-shaped folder with one PS module whose
        /// <c>&lt;Module&gt;.psd1</c> declares <paramref name="cmdletsToExport"/>. Returns the
        /// path to the module directory so tests can drop additional <c>help/</c> files into it.
        /// </summary>
        private static (string RepoRoot, string ModuleDir) CreateModule(
            TempDirectory temp,
            string moduleName,
            params string[] cmdletsToExport)
        {
            var repoRoot = temp.Path;
            var moduleDir = Path.Combine(repoRoot, "src", "PS", moduleName);
            Directory.CreateDirectory(moduleDir);

            var manifestBody = cmdletsToExport.Length == 0
                ? "@{ ModuleVersion = '1.0.0'; CmdletsToExport = @() }"
                : $"@{{ ModuleVersion = '1.0.0'; CmdletsToExport = {string.Join(", ", cmdletsToExport.Select(c => $"'{c}'"))} }}";

            File.WriteAllText(Path.Combine(moduleDir, $"{moduleName}.psd1"), manifestBody);
            return (repoRoot, moduleDir);
        }

        private static void CreateHelpFile(string moduleDir, string fileName)
        {
            var helpDir = Path.Combine(moduleDir, "help");
            Directory.CreateDirectory(helpDir);
            File.WriteAllText(Path.Combine(helpDir, fileName), "stub");
        }

        // ---- PSL10: missing help folder --------------------------------------------------

        [Fact]
        public void Validate_ShouldFirePSL10_WhenModuleExportsCmdletsButHasNoHelpFolder()
        {
            using var temp = new TempDirectory();
            var (repoRoot, _) = CreateModule(temp, "Accounts", "Connect-AzAccount", "Get-AzAccount");

            var report = new PsCompletenessValidator().Validate(repoRoot);

            Assert.True(report.HasErrors);
            var psl10 = report.Errors.Where(e => e.ProblemId == ProblemIds.PSL10_MissingHelpFolder).ToList();
            Assert.Single(psl10);
            Assert.Contains("Accounts", psl10[0].Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_ShouldNotFirePSL9_WhenPSL10AlreadyFiredForSameModule()
        {
            // PSL10 supersedes per-cmdlet PSL9 errors so multi-cmdlet modules don't drown
            // the log when the entire help/ folder is missing.
            using var temp = new TempDirectory();
            var (repoRoot, _) = CreateModule(temp, "Accounts", "Connect-AzAccount", "Get-AzAccount");

            var report = new PsCompletenessValidator().Validate(repoRoot);

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL9_MissingHelpFile);
        }

        // ---- PSL9: missing help file -----------------------------------------------------

        [Fact]
        public void Validate_ShouldFirePSL9_WhenExportedCmdletHasNoHelpFile()
        {
            using var temp = new TempDirectory();
            var (repoRoot, moduleDir) = CreateModule(temp, "Accounts", "Connect-AzAccount", "Get-AzAccount");
            CreateHelpFile(moduleDir, "Connect-AzAccount.md");
            // Get-AzAccount.md is intentionally absent.

            var report = new PsCompletenessValidator().Validate(repoRoot);

            var psl9 = report.Errors.Where(e => e.ProblemId == ProblemIds.PSL9_MissingHelpFile).ToList();
            Assert.Single(psl9);
            Assert.Contains("Get-AzAccount", psl9[0].Message, StringComparison.Ordinal);
            Assert.EndsWith("Get-AzAccount.md", psl9[0].FilePath, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_ShouldNotFirePSL9_WhenAllExportedCmdletsHaveHelpFiles()
        {
            using var temp = new TempDirectory();
            var (repoRoot, moduleDir) = CreateModule(temp, "Accounts", "Connect-AzAccount", "Get-AzAccount");
            CreateHelpFile(moduleDir, "Connect-AzAccount.md");
            CreateHelpFile(moduleDir, "Get-AzAccount.md");

            var report = new PsCompletenessValidator().Validate(repoRoot);

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldBeCaseInsensitive_WhenMatchingHelpFilesToCmdlets()
        {
            using var temp = new TempDirectory();
            var (repoRoot, moduleDir) = CreateModule(temp, "Accounts", "Connect-AzAccount");
            CreateHelpFile(moduleDir, "connect-azaccount.md"); // lowercased

            var report = new PsCompletenessValidator().Validate(repoRoot);

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.PSL9_MissingHelpFile);
        }

        // ---- PSL11: orphaned help file ---------------------------------------------------

        [Fact]
        public void Validate_ShouldWarnPSL11_WhenHelpFileHasNoCorrespondingExportedCmdlet()
        {
            using var temp = new TempDirectory();
            var (repoRoot, moduleDir) = CreateModule(temp, "Accounts", "Connect-AzAccount");
            CreateHelpFile(moduleDir, "Connect-AzAccount.md");
            CreateHelpFile(moduleDir, "Get-Ghost.md"); // orphan

            var report = new PsCompletenessValidator().Validate(repoRoot);

            Assert.False(report.HasErrors);
            var psl11 = report.Warnings.Where(w => w.ProblemId == ProblemIds.PSL11_OrphanedHelpFile).ToList();
            Assert.Single(psl11);
            Assert.Contains("Get-Ghost.md", psl11[0].Message, StringComparison.Ordinal);
        }

        // ---- Skip cases -------------------------------------------------------------------

        [Fact]
        public void Validate_ShouldSkipModule_WhenCmdletsToExportIsEmpty()
        {
            using var temp = new TempDirectory();
            var (repoRoot, _) = CreateModule(temp, "Accounts");

            var report = new PsCompletenessValidator().Validate(repoRoot);

            Assert.False(report.HasErrors);
            Assert.Empty(report.Warnings);
        }

        [Fact]
        public void Validate_ShouldSkipModule_WhenCmdletsToExportIsWildcard()
        {
            // Wildcard exports defer to runtime discovery — static validation can't see them.
            using var temp = new TempDirectory();
            var repoRoot = temp.Path;
            var moduleDir = Path.Combine(repoRoot, "src", "PS", "Accounts");
            Directory.CreateDirectory(moduleDir);
            File.WriteAllText(Path.Combine(moduleDir, "Accounts.psd1"),
                "@{ ModuleVersion = '1.0.0'; CmdletsToExport = '*' }");

            var report = new PsCompletenessValidator().Validate(repoRoot);

            Assert.False(report.HasErrors);
            Assert.Empty(report.Warnings);
        }

        [Fact]
        public void Validate_ShouldSkipFolder_WhenItHasNoModuleManifest()
        {
            // src/PS/ contains shared helpers (e.g. TelemetryHelper.ps1) without a .psd1.
            // The validator should ignore those folders silently — only psd1-bearing
            // module folders are in scope.
            using var temp = new TempDirectory();
            var repoRoot = temp.Path;
            var sharedDir = Path.Combine(repoRoot, "src", "PS", "Shared");
            Directory.CreateDirectory(sharedDir);
            File.WriteAllText(Path.Combine(sharedDir, "Helper.ps1"), "function Get-Foo {}");

            var report = new PsCompletenessValidator().Validate(repoRoot);

            Assert.False(report.HasErrors);
            Assert.Empty(report.Warnings);
        }

        [Fact]
        public void Validate_ShouldSkipModule_WhenManifestIsSyntacticallyInvalid()
        {
            // Don't double-report — Test-ModuleManifest / PlatyPS will surface the parse error.
            using var temp = new TempDirectory();
            var repoRoot = temp.Path;
            var moduleDir = Path.Combine(repoRoot, "src", "PS", "Accounts");
            Directory.CreateDirectory(moduleDir);
            File.WriteAllText(Path.Combine(moduleDir, "Accounts.psd1"), "this is { not @ valid syntax");

            var report = new PsCompletenessValidator().Validate(repoRoot);

            // Should not throw; module is silently skipped.
            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldReturnEmpty_WhenPsRootDoesNotExist()
        {
            using var temp = new TempDirectory();
            // No src/PS/ subtree at all.

            var report = new PsCompletenessValidator().Validate(temp.Path);

            Assert.False(report.HasErrors);
            Assert.Empty(report.Warnings);
        }

        // ---- Multi-module behaviour -------------------------------------------------------

        [Fact]
        public void Validate_ShouldReportEachModuleIndependently()
        {
            using var temp = new TempDirectory();
            var (repoRoot, accountsDir) = CreateModule(temp, "Accounts", "Connect-AzAccount");
            CreateHelpFile(accountsDir, "Connect-AzAccount.md");

            // Second module: missing help file.
            var (_, configDir) = CreateModule(temp, "Config", "Get-AzConfig");
            Directory.CreateDirectory(Path.Combine(configDir, "help")); // help/ exists, file does not

            var report = new PsCompletenessValidator().Validate(repoRoot);

            var psl9 = report.Errors.Where(e => e.ProblemId == ProblemIds.PSL9_MissingHelpFile).ToList();
            Assert.Single(psl9);
            Assert.Contains("Get-AzConfig", psl9[0].Message, StringComparison.Ordinal);
            Assert.Contains("Config", psl9[0].Message, StringComparison.Ordinal);
        }

        // ---- Manifest parser unit tests ---------------------------------------------------
        // ParseCmdletsToExport is internal; covered indirectly by the Validate_* tests above
        // (comma-separated literals, wildcard '*', empty list, broken-syntax skip).
    }
}
