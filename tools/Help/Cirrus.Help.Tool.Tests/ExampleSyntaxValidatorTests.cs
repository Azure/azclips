// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cirrus.Help.Tool.Validation;
using Core.Abstractions;

namespace Cirrus.Help.Tool.Tests
{
    public class ExampleSyntaxValidatorTests
    {
        private static IReadOnlyDictionary<string, IExtensionIndex> BuildIndexes()
        {
            // login: root + logout + show
            var login = new StubExtensionIndex(new Dictionary<string, (string flag, bool required, string[] aliases)[]>
            {
                [""] = new (string, bool, string[])[]
                {
                    ("subscription-id", true, Array.Empty<string>()),
                    ("use-device-code", false, Array.Empty<string>()),
                },
                ["logout"] = Array.Empty<(string, bool, string[])>(),
                ["show"] = Array.Empty<(string, bool, string[])>(),
            });

            // resources: create / list / show with subscription-id, name, etc.
            var resources = new StubExtensionIndex(new Dictionary<string, (string flag, bool required, string[] aliases)[]>
            {
                ["create"] = new (string, bool, string[])[]
                {
                    ("name", true, new[] { "n" }),
                    ("location", true, Array.Empty<string>()),
                    ("subscription-id", true, Array.Empty<string>()),
                    ("resource-id", false, Array.Empty<string>()),
                },
                ["list"] = new (string, bool, string[])[]
                {
                    ("subscription-id", false, Array.Empty<string>()),
                },
                ["show"] = new (string, bool, string[])[]
                {
                    ("name", true, Array.Empty<string>()),
                    ("subscription-id", false, Array.Empty<string>()),
                },
            });

            return new Dictionary<string, IExtensionIndex>(StringComparer.OrdinalIgnoreCase)
            {
                ["login"] = login,
                ["group"] = resources,
            };
        }

        private static string Fence(params string[] lines)
        {
            return "```bash\n" + string.Join("\n", lines) + "\n```";
        }

        [Fact]
        public void Validate_ShouldPass_WhenExampleResolvesAndAllFlagsAreKnown()
        {
            var content = Fence("azv2 group create --name myrg --location eastus --subscription-id 00000000-0000-0000-0000-000000000000");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldPass_WhenRootCommandResolvesToEmptyKey()
        {
            // azv2 login (no subcommand) → key "" in login's CLI table.
            var content = Fence("azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldError_WhenGroupIsUnknown()
        {
            var content = Fence("azv2 widget create --name foo");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.Message.Contains("Unknown CLI group 'widget'", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldError_WhenSubcommandDoesNotResolve()
        {
            var content = Fence("azv2 group bogus --name foo");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.Message.Contains("Unknown command path", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldError_WhenFlagIsNotDeclaredOnCommand()
        {
            var content = Fence("azv2 group create --name foo --location eastus --subscription-id 00000000-0000-0000-0000-000000000000 --bogus value");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.Message.Contains("Unknown flag '--bogus'", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldAllowDocumentedGlobalFlags()
        {
            var content = Fence("azv2 group list --subscription-id 00000000-0000-0000-0000-000000000000 --help --debug --output json");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldAllowShortAliasFlag_WhenDeclaredOnParameter()
        {
            // -n is aliased to --name on `group create`.
            var content = Fence("azv2 group create -n myrg --location eastus --subscription-id 00000000-0000-0000-0000-000000000000");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldSupportEqualsFlagForm()
        {
            var content = Fence("azv2 group create --name=myrg --location=eastus --subscription-id=00000000-0000-0000-0000-000000000000");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldIgnore_NonAzv2AndCommentLines()
        {
            var content = Fence(
                "# This is a comment",
                "echo hello",
                "azv2 group list");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldOnlyFailOffendingLine_InMultiLineBlock()
        {
            var content = Fence(
                "azv2 group list",
                "azv2 group create --name foo --location westus --subscription-id 00000000-0000-0000-0000-000000000000 --rogue 1");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            var errors = report.Errors.ToList();
            Assert.Single(errors);
            Assert.Contains("'--rogue'", errors[0].Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_ShouldSkip_PowerShellFences()
        {
            // PowerShell example uses a fake cmdlet — must not be validated against CLI metadata.
            var content = "```powershell\nNew-ResourceGroup -Name myrg -BogusFlag 1\n```";

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldIncludeFilePathAndLineNumber_InErrorLocator()
        {
            // Two-line preamble pushes the azv2 line to absolute line 3.
            var content = "Some prose.\n\n```bash\nazv2 widget create --name foo\n```";

            var report = new ExampleSyntaxValidator().ValidateContent(content, "extensions/foo/help/index.md", BuildIndexes());

            Assert.True(report.HasErrors);
            // The fence body starts on line 4 (1 = "Some prose", 2 = blank, 3 = "```bash", 4 = azv2 line).
            Assert.Contains(report.Errors, e => e.FilePath!.EndsWith(":4", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldNotTreatUrlOrPathOrNegativeNumber_AsFlag()
        {
            // Values like https://..., /subscriptions/..., -1 should never be flagged as unknown flags.
            var content = Fence("azv2 group create --name myrg --location westus --subscription-id 00000000-0000-0000-0000-000000000000 --resource-id /subscriptions/abc/resourceGroups/myrg");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        private sealed class StubExtensionIndex : IExtensionIndex
        {
            private readonly Dictionary<string, CommandMetadata> _cliTable;

            public StubExtensionIndex(Dictionary<string, (string flag, bool required, string[] aliases)[]> commands)
            {
                _cliTable = commands.ToDictionary(
                    pair => pair.Key,
                    pair =>
                    {
                        var parameters = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);
                        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var (flag, required, alias) in pair.Value)
                        {
                            parameters[flag] = new ParameterMetadata(flag, typeof(string), "", required, alias, Array.Empty<string>());
                            foreach (var a in alias)
                            {
                                aliases[a] = flag;
                            }
                        }
                        return new CommandMetadata(
                            Name: pair.Key,
                            Aliases: Array.Empty<string>(),
                            FullName: pair.Key,
                            AssemblyName: "Stub",
                            ProcessorType: typeof(StubExtensionIndex),
                            Parameters: parameters,
                            ParameterAliases: aliases);
                    },
                    StringComparer.OrdinalIgnoreCase);
            }

            public string CliRootSummary => "Stub";

            public CommandMetadata GetCommandMetadata(string name, ClientType clientType) => _cliTable[name];

            public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => _cliTable;

            public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => _cliTable.Values;
        }
    }
}
