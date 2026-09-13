// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azclips.Help.Tool.Validation;
using Core.Abstractions;

namespace Azclips.Help.Tool.Tests
{
    public class ExampleSyntaxValidatorTests
    {
        private static IReadOnlyDictionary<string, IExtensionIndex> BuildIndexes()
        {
            // login: root + logout + show
            var login = new StubExtensionIndex(new Dictionary<string, (string flag, bool required, string[] aliases)[]>
            {
                ["login"] = new (string, bool, string[])[]
                {
                    ("subscription-id", true, Array.Empty<string>()),
                    ("use-device-code", false, Array.Empty<string>()),
                },
                ["login logout"] = Array.Empty<(string, bool, string[])>(),
                ["login show"] = Array.Empty<(string, bool, string[])>(),
            });

            // resources: create / list / show with subscription-id, name, etc.
            var resources = new StubExtensionIndex(new Dictionary<string, (string flag, bool required, string[] aliases)[]>
            {
                ["group create"] = new (string, bool, string[])[]
                {
                    ("name", true, new[] { "n" }),
                    ("location", true, Array.Empty<string>()),
                    ("subscription-id", true, Array.Empty<string>()),
                    ("resource-id", false, Array.Empty<string>()),
                },
                ["group list"] = new (string, bool, string[])[]
                {
                    ("subscription-id", false, Array.Empty<string>()),
                },
                ["group show"] = new (string, bool, string[])[]
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
            var content = Fence("az group create --name myrg --location eastus --subscription-id 00000000-0000-0000-0000-000000000000");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldPass_WhenRootCommandResolvesToEmptyKey()
        {
            // az login (no subcommand) → key "" in login's CLI table.
            var content = Fence("az login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldError_WhenGroupIsUnknown()
        {
            var content = Fence("az widget create --name foo");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.Message.Contains("Unknown CLI group 'widget'", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldPass_WhenGroupIsIntrinsicVerb()
        {
            // `version` is an intrinsic host verb (owned by the CLI host, not by any extension), so
            // it never appears in the extension index. `az version` must still validate cleanly.
            var content = Fence("az version");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldPass_WhenIntrinsicVerbUsesTopLevelFlagForm()
        {
            // Intrinsic verbs also have a top-level flag form (`az --version`), handled by the runtime.
            // It must validate cleanly even though `--version` is not an extension-registered group.
            var content = Fence("az --version");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldError_WhenSubcommandDoesNotResolve()
        {
            var content = Fence("az group bogus --name foo");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.Message.Contains("Unknown command path", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldError_WhenFlagIsNotDeclaredOnCommand()
        {
            var content = Fence("az group create --name foo --location eastus --subscription-id 00000000-0000-0000-0000-000000000000 --bogus value");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.Message.Contains("Unknown flag '--bogus'", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldAllowDocumentedGlobalFlags()
        {
            var content = Fence("az group list --subscription-id 00000000-0000-0000-0000-000000000000 --help --debug --output json --query [].name");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldAllowShortAliasFlag_WhenDeclaredOnParameter()
        {
            // -n is aliased to --name on `group create`.
            var content = Fence("az group create -n myrg --location eastus --subscription-id 00000000-0000-0000-0000-000000000000");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldSupportEqualsFlagForm()
        {
            var content = Fence("az group create --name=myrg --location=eastus --subscription-id=00000000-0000-0000-0000-000000000000");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldIgnore_NonazAndCommentLines()
        {
            var content = Fence(
                "# This is a comment",
                "echo hello",
                "az group list");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldOnlyFailOffendingLine_InMultiLineBlock()
        {
            var content = Fence(
                "az group list",
                "az group create --name foo --location westus --subscription-id 00000000-0000-0000-0000-000000000000 --rogue 1");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            var errors = report.Errors.ToList();
            Assert.Single(errors);
            Assert.Contains("'--rogue'", errors[0].Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_ShouldSkip_PowerShellFences()
        {
            // PowerShell fences are skipped entirely — never validated against CLI metadata —
            // so the bogus flag on this cmdlet must not raise an error.
            var content = "```powershell\nNew-AzResourceGroup -Name myrg -BogusFlag 1\n```";

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldIncludeFilePathAndLineNumber_InErrorLocator()
        {
            // Two-line preamble pushes the az line to absolute line 3.
            var content = "Some prose.\n\n```bash\naz widget create --name foo\n```";

            var report = new ExampleSyntaxValidator().ValidateContent(content, "extensions/foo/help/index.md", BuildIndexes());

            Assert.True(report.HasErrors);
            // The fence body starts on line 4 (1 = "Some prose", 2 = blank, 3 = "```bash", 4 = az line).
            Assert.Contains(report.Errors, e => e.FilePath!.EndsWith(":4", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldNotTreatUrlOrPathOrNegativeNumber_AsFlag()
        {
            // Values like https://..., /subscriptions/..., -1 should never be flagged as unknown flags.
            var content = Fence("az group create --name myrg --location westus --subscription-id 00000000-0000-0000-0000-000000000000 --resource-id /subscriptions/abc/resourceGroups/myrg");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        private enum StubOutputFormat { Json, Table, None }

        private static IReadOnlyDictionary<string, IExtensionIndex> BuildTypedIndexes()
        {
            // Single command 'vm create' with mixed typed parameters to exercise L3.
            var vm = new StubExtensionIndex(new Dictionary<string, ParameterMetadata[]>
            {
                ["vm create"] = new[]
                {
                    new ParameterMetadata("subscription-id", "subscription-id", typeof(Guid), "", true, Array.Empty<string>(), Array.Empty<string>()),
                    new ParameterMetadata("name", "name", typeof(string), "", true, Array.Empty<string>(), Array.Empty<string>()),
                    new ParameterMetadata("retries", "retries", typeof(int), "", false, Array.Empty<string>(), Array.Empty<string>()),
                    new ParameterMetadata("use-device-code", "use-device-code", typeof(bool), "", false, Array.Empty<string>(), Array.Empty<string>()),
                    new ParameterMetadata("format", "format", typeof(StubOutputFormat), "", false, Array.Empty<string>(), Array.Empty<string>()),
                    new ParameterMetadata("verbosity", "verbosity", typeof(string), "", false, Array.Empty<string>(), new[] { "quiet", "normal", "diagnostic" }),
                    new ParameterMetadata("tags", "tags", typeof(List<string>), "", false, Array.Empty<string>(), Array.Empty<string>()),
                },
            });

            return new Dictionary<string, IExtensionIndex>(StringComparer.OrdinalIgnoreCase)
            {
                ["vm"] = vm,
            };
        }

        [Fact]
        public void Validate_ShouldPass_WhenGuidValueIsWellFormed()
        {
            var content = Fence("az vm create --name myvm --subscription-id 11111111-2222-3333-4444-555555555555");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldError_WhenGuidValueIsMalformed()
        {
            var content = Fence("az vm create --name myvm --subscription-id not-a-guid");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.Message.Contains("not-a-guid", StringComparison.Ordinal)
                && e.Message.Contains("not a valid Guid", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldError_WhenIntValueIsMalformed()
        {
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --retries abc");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.Message.Contains("not a valid Int32", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldPass_WhenIntValueIsWellFormed()
        {
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --retries 5");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldPass_WhenBoolSwitchHasNoValue()
        {
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldPass_WhenBoolExplicitValueIsTrueOrFalse()
        {
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code true");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldError_WhenBoolValueIsNeitherTrueNorFalse()
        {
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code yes");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e => e.Message.Contains("not a valid Boolean", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldPass_WhenEnumValueMatchesCaseInsensitively()
        {
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --format JSON");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldError_WhenEnumValueIsNotAMember()
        {
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --format xml");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.Message.Contains("xml", StringComparison.Ordinal)
                && e.Message.Contains("not one of", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldError_WhenAllowedValuesAreSetAndValueIsOutOfRange()
        {
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --verbosity loud");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.True(report.HasErrors);
            Assert.Contains(report.Errors, e =>
                e.Message.Contains("loud", StringComparison.Ordinal)
                && e.Message.Contains("not in allowed values", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldPass_WhenAllowedValuesContainTheValue()
        {
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --verbosity Quiet");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldSkipL3_WhenValueIsAngleBracketPlaceholder()
        {
            var content = Fence("az vm create --name myvm --subscription-id <subId> --retries <count>");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldSkipL3_WhenValueIsShellVariable()
        {
            var content = Fence("az vm create --name $vmName --subscription-id $subId --retries $count");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldSkipL3_WhenValueIsAllZeroGuidPlaceholder()
        {
            // The all-zero GUID is also a valid Guid, but documented as a placeholder.
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldSupportEqualsForm_BothPassAndFail()
        {
            var pass = Fence("az vm create --name myvm --subscription-id=11111111-2222-3333-4444-555555555555");
            var fail = Fence("az vm create --name myvm --subscription-id=not-a-guid");

            var passReport = new ExampleSyntaxValidator().ValidateContent(pass, "fake.md", BuildTypedIndexes());
            var failReport = new ExampleSyntaxValidator().ValidateContent(fail, "fake.md", BuildTypedIndexes());

            Assert.False(passReport.HasErrors);
            Assert.True(failReport.HasErrors);
            Assert.Contains(failReport.Errors, e => e.Message.Contains("not a valid Guid", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldSkipL3_ForListParameters()
        {
            // List<string> element validation is deferred; literal value should not trigger an error.
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --tags foo");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldFireL2Only_WhenFlagIsUnknown_NotL3OnItsValue()
        {
            // --unknwn is unknown; the next token 'foo' should be skipped, not double-reported as a bad value.
            var content = Fence("az vm create --name myvm --subscription-id 00000000-0000-0000-0000-000000000000 --unknwn foo");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildTypedIndexes());

            Assert.True(report.HasErrors);
            var errors = report.Errors.ToList();
            Assert.Single(errors);
            Assert.Contains("Unknown flag '--unknwn'", errors[0].Message, StringComparison.Ordinal);
        }

        // ---- CLI_L4_MissingRequiredFlag ----------------------------------------------------

        [Fact]
        public void Validate_ShouldErrorL4_WhenRequiredFlagIsMissing()
        {
            // 'group create' requires name + location + subscription-id; only --name supplied.
            var content = Fence("az group create --name myrg");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            var l4Errors = report.Errors
                .Where(e => e.ProblemId == ProblemIds.CLI_L4_MissingRequiredFlag)
                .ToList();

            // Both --location and --subscription-id should be reported in one pass.
            Assert.Equal(2, l4Errors.Count);
            Assert.Contains(l4Errors, e => e.Message.Contains("--location", StringComparison.Ordinal));
            Assert.Contains(l4Errors, e => e.Message.Contains("--subscription-id", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_ShouldNotFireL4_WhenAliasFlagSatisfiesRequirement()
        {
            // -n is the alias for --name; using the alias should satisfy the requirement.
            var content = Fence("az group create -n myrg --location eastus --subscription-id 00000000-0000-0000-0000-000000000000");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.CLI_L4_MissingRequiredFlag);
        }

        [Fact]
        public void Validate_ShouldNotFireL4_OnHelpInvocations()
        {
            // `az group create --help` is a help invocation; required-flag policing must not apply.
            var content = Fence("az group create --help");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.CLI_L4_MissingRequiredFlag);
        }

        [Fact]
        public void Validate_ShouldStillPoliceFlags_WhenCommandNameContainsHelp()
        {
            // Regression: bare "help" as a sub-command name (e.g. `az agent tool help`) must not
            // be mistaken for a --help invocation. Unknown flags on such commands must still fire.
            var help = new StubExtensionIndex(new Dictionary<string, (string flag, bool required, string[] aliases)[]>
            {
                ["agent tool help"] = new (string, bool, string[])[]
                {
                    ("name", false, Array.Empty<string>()),
                },
            });
            var indexes = new Dictionary<string, IExtensionIndex>(StringComparer.OrdinalIgnoreCase)
            {
                ["agent"] = help,
            };
            var content = Fence("az agent tool help --command \"vm create\"");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", indexes);

            Assert.Contains(report.Errors, e =>
                e.ProblemId == ProblemIds.CLI_L2_UnknownFlag &&
                e.Message.Contains("'--command'", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("az group create ?")]
        [InlineData("az group create -?")]
        public void Validate_ShouldNotFireL4_OnBareQuestionMarkHelpInvocations(string invocation)
        {
            // Parity with ExtensionDispatcher.s_helpFlags: `?` and `-?` are accepted as help
            // flags anywhere in the arg list, so required-flag policing must not apply.
            var content = Fence(invocation);

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.CLI_L4_MissingRequiredFlag);
        }

        [Fact]
        public void Validate_ShouldNotFireL4_WhenCommandHasNoRequiredFlags()
        {
            // 'group list' has only an optional subscription-id; bare invocation must pass.
            var content = Fence("az group list");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", BuildIndexes());

            Assert.False(report.HasErrors);
        }

        [Fact]
        public void Validate_ShouldNotFireL4_WhenParameterNameIsPascalCaseAndExampleUsesKebabFlag()
        {
            // Regression: dictionary KEYS are kebab-case CLI flags ("subscription-id");
            // ParameterMetadata.Name is the PascalCase property ("SubscriptionId").
            // The validator must match against the dictionary key, not the .Name field.
            var widgetCreate = new CommandMetadata(
                Name: "widget create",
                InvocationName: "widget create",
                Aliases: Array.Empty<string>(),
                FullName: "widget create",
                AssemblyName: "Stub",
                ProcessorType: typeof(StubExtensionIndex),
                Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"]            = new ParameterMetadata("Name",           "name", typeof(string), "", IsRequired: true, Aliases: new[] { "n" }, AllowedValues: Array.Empty<string>()),
                    ["location"]        = new ParameterMetadata("Location",       "location", typeof(string), "", IsRequired: true, Aliases: Array.Empty<string>(), AllowedValues: Array.Empty<string>()),
                    ["subscription-id"] = new ParameterMetadata("SubscriptionId", "subscription-id", typeof(string), "", IsRequired: true, Aliases: Array.Empty<string>(), AllowedValues: Array.Empty<string>()),
                },
                ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["n"] = "name" });

            var widget = new StubExtensionIndex(new Dictionary<string, CommandMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["widget create"] = widgetCreate,
            });
            var indexes = new Dictionary<string, IExtensionIndex>(StringComparer.OrdinalIgnoreCase) { ["widget"] = widget };

            var content = Fence("az widget create --name myrg --location eastus --subscription-id 00000000-0000-0000-0000-000000000000");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", indexes);

            Assert.DoesNotContain(report.Errors, e => e.ProblemId == ProblemIds.CLI_L4_MissingRequiredFlag);
        }

        [Fact]
        public void Validate_ShouldFireL4_WhenParameterNameIsPascalCaseAndExampleOmitsRequiredKebabFlag()
        {
            // Mirror of the test above: same production-shape invariant, but the example omits
            // --subscription-id. The validator must still flag the missing kebab flag exactly
            // once (not zero, not duplicated for "subscription-id" + "SubscriptionId").
            var widgetCreate = new CommandMetadata(
                Name: "widget create",
                InvocationName: "widget create",
                Aliases: Array.Empty<string>(),
                FullName: "widget create",
                AssemblyName: "Stub",
                ProcessorType: typeof(StubExtensionIndex),
                Parameters: new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"]            = new ParameterMetadata("Name",           "name", typeof(string), "", IsRequired: true, Aliases: Array.Empty<string>(), AllowedValues: Array.Empty<string>()),
                    ["subscription-id"] = new ParameterMetadata("SubscriptionId", "subscription-id", typeof(string), "", IsRequired: true, Aliases: Array.Empty<string>(), AllowedValues: Array.Empty<string>()),
                },
                ParameterAliases: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            var widget = new StubExtensionIndex(new Dictionary<string, CommandMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["widget create"] = widgetCreate,
            });
            var indexes = new Dictionary<string, IExtensionIndex>(StringComparer.OrdinalIgnoreCase) { ["widget"] = widget };

            var content = Fence("az widget create --name myrg");

            var report = new ExampleSyntaxValidator().ValidateContent(content, "fake.md", indexes);

            var l4Errors = report.Errors.Where(e => e.ProblemId == ProblemIds.CLI_L4_MissingRequiredFlag).ToList();
            Assert.Single(l4Errors);
            Assert.Contains("--subscription-id", l4Errors[0].Message, StringComparison.Ordinal);
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
                            parameters[flag] = new ParameterMetadata(flag, flag, typeof(string), "", required, alias, Array.Empty<string>());
                            foreach (var a in alias)
                            {
                                aliases[a] = flag;
                            }
                        }
                        return new CommandMetadata(
                            Name: pair.Key,
                            InvocationName: pair.Key,
                            Aliases: Array.Empty<string>(),
                            FullName: pair.Key,
                            AssemblyName: "Stub",
                            ProcessorType: typeof(StubExtensionIndex),
                            Parameters: parameters,
                            ParameterAliases: aliases);
                    },
                    StringComparer.OrdinalIgnoreCase);
            }

            public StubExtensionIndex(Dictionary<string, ParameterMetadata[]> commands)
            {
                _cliTable = commands.ToDictionary(
                    pair => pair.Key,
                    pair =>
                    {
                        var parameters = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);
                        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var p in pair.Value)
                        {
                            parameters[p.Name] = p;
                            foreach (var a in p.Aliases)
                            {
                                aliases[a] = p.Name;
                            }
                        }
                        return new CommandMetadata(
                            Name: pair.Key,
                            InvocationName: pair.Key,
                            Aliases: Array.Empty<string>(),
                            FullName: pair.Key,
                            AssemblyName: "Stub",
                            ProcessorType: typeof(StubExtensionIndex),
                            Parameters: parameters,
                            ParameterAliases: aliases);
                    },
                    StringComparer.OrdinalIgnoreCase);
            }

            public StubExtensionIndex(IReadOnlyDictionary<string, CommandMetadata> table)
            {
                _cliTable = new Dictionary<string, CommandMetadata>(table, StringComparer.OrdinalIgnoreCase);
            }

            public CommandMetadata GetCommandMetadata(string name, ClientType clientType) => _cliTable[name];

            public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => _cliTable;

            public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => _cliTable.Values;
        }
    }
}
