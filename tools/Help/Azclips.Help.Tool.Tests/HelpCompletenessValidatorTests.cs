// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azclips.Help.Tool.Validation;
using Core.Abstractions;

namespace Azclips.Help.Tool.Tests
{
    public class HelpCompletenessValidatorTests
    {
        [Fact]
        public void Validate_ShouldPass_WhenAllRoutedCommandsHaveHelpFiles()
        {
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "index.md"), "x");
            File.WriteAllText(Path.Combine(helpDir, "logout.md"), "x");

            var index = new StubExtensionIndex(new[] { "login", "login logout" });

            var report = new HelpCompletenessValidator()
                .Validate("login", "login", helpDir, index);

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldReportError_WhenRoutedCommandHasNoHelpFile()
        {
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "index.md"), "x");
            // No logout.md, but command is routed.

            var index = new StubExtensionIndex(new[] { "login", "login logout" });

            var report = new HelpCompletenessValidator()
                .Validate("login", "login", helpDir, index);

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.FilePath!.EndsWith("logout.md", StringComparison.Ordinal) &&
                e.Message.Contains("'logout'", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldNormalizeMultiWordKeys_WhenMappingToFilenames()
        {
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "index.md"), "x");
            File.WriteAllText(Path.Combine(helpDir, "get.md"), "x");

            var index = new StubExtensionIndex(new[] { "config", "config get" });

            var report = new HelpCompletenessValidator()
                .Validate("config", "config", helpDir, index);

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldFailWithFriendlyMessage_WhenMultiWordCommandLacksHelp()
        {
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "index.md"), "x");

            var index = new StubExtensionIndex(new[] { "config", "config get" });

            var report = new HelpCompletenessValidator()
                .Validate("config", "config", helpDir, index);

            Assert.Contains(report.Errors, e =>
                e.FilePath!.EndsWith("get.md", StringComparison.Ordinal) &&
                e.Message.Contains("'get'", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldWarnButNotFail_WhenOrphanedHelpFileExists()
        {
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "index.md"), "x");
            File.WriteAllText(Path.Combine(helpDir, "ghost.md"), "x"); // orphan

            var index = new StubExtensionIndex(new[] { "login" });

            var report = new HelpCompletenessValidator()
                .Validate("login", "login", helpDir, index);

            Assert.False(report.HasErrors);
            Assert.Contains(report.Warnings, w =>
                w.FilePath!.EndsWith("ghost.md", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldPass_WhenOnlyIndexFileExistsForRootCommand()
        {
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "index.md"), "x");

            var index = new StubExtensionIndex(new[] { "login" });

            var report = new HelpCompletenessValidator()
                .Validate("login", "login", helpDir, index);

            Assert.False(report.HasErrors);
            Assert.Empty(report.Warnings);
        }

        [Fact]
        public void Validate_ShouldBeCaseInsensitive_WhenComparingFilenames()
        {
            // Note: file extension stays ".md" (lowercase) because Directory.EnumerateFiles
            // pattern matching is case-sensitive on Linux. The validator's case-insensitivity
            // applies to filename comparisons (Index vs index, Logout vs logout), not to
            // the extension globbing performed by the runtime.
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "Index.md"), "x");
            File.WriteAllText(Path.Combine(helpDir, "Logout.md"), "x");

            var index = new StubExtensionIndex(new[] { "login", "login logout" });

            var report = new HelpCompletenessValidator()
                .Validate("login", "login", helpDir, index);

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldNotThrow_WhenHelpDirectoryDoesNotExist()
        {
            using var temp = new TempDirectory();
            var helpDir = Path.Combine(temp.Path, "does-not-exist");

            var index = new StubExtensionIndex(new[] { "login" });

            var report = new HelpCompletenessValidator()
                .Validate("login", "login", helpDir, index);

            // index.md is expected → error, but no exception.
            Assert.Contains(report.Errors, e => e.FilePath!.EndsWith("index.md", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldPass_WhenNestedLeafAndIntermediateIndexExist()
        {
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "index.md"), "x");
            Directory.CreateDirectory(Path.Combine(helpDir, "group"));
            File.WriteAllText(Path.Combine(helpDir, "group", "index.md"), "x");
            File.WriteAllText(Path.Combine(helpDir, "group", "create.md"), "x");

            var index = new StubExtensionIndex(new[] { "deployment", "deployment group create" });

            var report = new HelpCompletenessValidator()
                .Validate("deployment", "deployment", helpDir, index);

            Assert.False(report.HasErrors);
            Assert.Empty(report.Warnings);
        }

        [Fact]
        public void Validate_ShouldFail_WhenIntermediateGroupIndexIsMissing()
        {
            // Regression guard: nested leaf without intermediate index.md must fail.
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "index.md"), "x");
            Directory.CreateDirectory(Path.Combine(helpDir, "group"));
            File.WriteAllText(Path.Combine(helpDir, "group", "create.md"), "x");

            var index = new StubExtensionIndex(new[] { "deployment", "deployment group create" });

            var report = new HelpCompletenessValidator()
                .Validate("deployment", "deployment", helpDir, index);

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.FilePath!.EndsWith(Path.Combine("group", "index.md"), StringComparison.Ordinal) &&
                e.Message.Contains("'deployment group'", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldFail_WhenNestedLeafIsMissing()
        {
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "index.md"), "x");
            Directory.CreateDirectory(Path.Combine(helpDir, "group"));
            File.WriteAllText(Path.Combine(helpDir, "group", "index.md"), "x");

            var index = new StubExtensionIndex(new[] { "deployment", "deployment group create" });

            var report = new HelpCompletenessValidator()
                .Validate("deployment", "deployment", helpDir, index);

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.FilePath!.EndsWith(Path.Combine("group", "create.md"), StringComparison.Ordinal) &&
                e.Message.Contains("'group create'", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldWarn_WhenNestedOrphanExists()
        {
            using var temp = new TempDirectory();
            var helpDir = temp.Path;
            File.WriteAllText(Path.Combine(helpDir, "index.md"), "x");
            Directory.CreateDirectory(Path.Combine(helpDir, "ghost"));
            File.WriteAllText(Path.Combine(helpDir, "ghost", "leaf.md"), "x");

            var index = new StubExtensionIndex(new[] { "login" });

            var report = new HelpCompletenessValidator()
                .Validate("login", "login", helpDir, index);

            Assert.False(report.HasErrors);
            Assert.Contains(report.Warnings, w =>
                w.FilePath!.EndsWith(Path.Combine("ghost", "leaf.md"), StringComparison.Ordinal));
        }

        private sealed class StubExtensionIndex : IExtensionIndex
        {
            private readonly Dictionary<string, CommandMetadata> _cliTable;

            public StubExtensionIndex(IEnumerable<string> commandKeys)
            {
                _cliTable = commandKeys.ToDictionary(
                    key => key,
                    key => new CommandMetadata(
                        Name: key,
                        InvocationName: key,
                        Aliases: Array.Empty<string>(),
                        FullName: key,
                        AssemblyName: "Stub",
                        ProcessorType: typeof(StubExtensionIndex),
                        Parameters: new Dictionary<string, ParameterMetadata>(),
                        ParameterAliases: new Dictionary<string, string>()),
                    StringComparer.OrdinalIgnoreCase);
            }

            public CommandMetadata GetCommandMetadata(string name, ClientType clientType) => _cliTable[name];

            public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => _cliTable;

            public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => _cliTable.Values;
        }
    }
}
