"""Regression tests for automatic command-surface test signal attribution."""

import copy
import importlib.util
import json
import re
from html.parser import HTMLParser
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
from concurrent.futures import ThreadPoolExecutor

import extension_coverage_analysis as syntax
from extension_coverage_analysis import Analysis, ArmScenarioAnalysis, LocalScenarioAnalysis, descendants


SCRIPT = Path(__file__).with_name("generate-extension-test-coverage.py")
SPEC = importlib.util.spec_from_file_location("extension_test_coverage", SCRIPT)
coverage = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = coverage
SPEC.loader.exec_module(coverage)
CLI_HELPERS = ("RunCli", "ExecuteCli", "RunCliAsync", "ExecuteCliAsync")


class InvocationTests(unittest.TestCase):
    def test_literal_helper_forms(self):
        for helper in CLI_HELPERS:
            for receiver in ("", "fixture.", "ScenarioHost."):
                for args in (
                    '"profile", "show", "prod", "--verbose"',
                    '["profile", "show", "prod", "--verbose"]',
                    'new[] { "profile", "show", "prod", "--verbose" }',
                ):
                    with self.subTest(helper=helper, receiver=receiver, args=args):
                        self.assertEqual(
                            ({"CLI:profile show prod"}, []),
                            coverage.invocations_from_csharp(f"{receiver}{helper}({args});"),
                        )

    def test_argument_helpers(self):
        for helper in CLI_HELPERS:
            for declaration, argument in (
                ('string[] Args() => ["profile", "show", "prod"];', "Args()"),
                ('var args = new[] { "profile", "show", "prod" };', "args"),
                ('string[] args = ["profile", "show", "prod"];', "args"),
            ):
                with self.subTest(helper=helper, declaration=declaration):
                    self.assertEqual(
                        ({"CLI:profile show prod"}, []),
                        coverage.invocations_from_csharp(declaration + f"\nfixture.{helper}({argument});"),
                    )

    def test_local_fallback_uses_actual_helper_name(self):
        for helper in CLI_HELPERS:
            with self.subTest(helper=helper):
                self.assertEqual(
                    ({"CLI:profile show prod"}, []),
                    coverage.invocations_from_csharp(
                        f'int {helper}() => host.RunCli(["profile", "show", "prod"]);\n{helper}();'
                    ),
                )

    def test_unresolved_calls_preserve_helper_names(self):
        for helper in CLI_HELPERS:
            with self.subTest(helper=helper):
                self.assertEqual(
                    (set(), [f"{helper}(dynamicArgs)"]),
                    coverage.invocations_from_csharp(f"fixture.{helper}(dynamicArgs);"),
                )

    def test_declaration_detection_does_not_match_quoted_arguments(self):
        for value in ("params", "string[] args", "ScenarioHost host"):
            with self.subTest(value=value):
                self.assertEqual(
                    ({f"CLI:profile show {value.lower()}"}, []),
                    coverage.invocations_from_csharp(f'ExecuteCli(["profile", "show", "{value}"]);'),
                )

    def test_other_helpers_do_not_resolve_unknown_or_receiver_calls(self):
        self.assertEqual(
            ({"CLI:profile list"}, ["ExecuteCli(dynamicArgs)", "RunCli(dynamicArgs)"]),
            coverage.invocations_from_csharp(
                'int RunCli() => host.RunCli(["profile", "list"]);\n'
                "ExecuteCli(dynamicArgs);\nfixture.RunCli(dynamicArgs);"
            ),
        )

    def test_declarations_are_not_calls(self):
        for helper in CLI_HELPERS:
            for parameter in (
                "string[] args", "string [] args", "params string[] args",
                "string command", "ScenarioHost host, string[] args", "IReadOnlyList<string> args",
            ):
                with self.subTest(helper=helper, parameter=parameter):
                    self.assertEqual(
                        (set(), []),
                        coverage.invocations_from_csharp(f"public int {helper}({parameter}) {{ return 0; }}"),
                    )

    def test_powershell_helpers_keep_exact_keys(self):
        self.assertEqual(
            ({"PowerShell:get-azprofile", "PowerShell:get-azprofile prod",
              "PowerShell:get-azprofile_detail"}, []),
            coverage.invocations_from_csharp(
                'host.RunPowerShell("Get-AzProfile");\n'
                'host.RunPowerShellCommand("Get-AzProfile prod");\n'
                'pipeline.AddCommand("Get-AzProfile_Detail");'
            ),
        )


class ResolutionTests(unittest.TestCase):
    def test_longest_cli_prefix_at_token_boundary_and_exact_powershell(self):
        commands = [coverage.CommandCoverage("Accounts", "CLI", name) for name in
                    ("profile", "profile show", "profile show details")]
        commands.append(coverage.CommandCoverage("Accounts", "PowerShell", "Get-AzProfile"))
        index = {coverage.command_key(c.surface, c.invocation_name): c for c in commands}
        for invocation, expected in (
            ("CLI:profile show", commands[1]), ("CLI:profile show prod", commands[1]),
            ("CLI:profile show details prod", commands[2]), ("CLI:profile showcase prod", commands[0]),
            ("CLI:profiles show prod", None), ("CLI:unknown show", None),
            ("PowerShell:get-azprofile", commands[3]), ("PowerShell:get-azprofile prod", None),
            ("PowerShell:get-azprofile_detail", None),
        ):
            with self.subTest(invocation=invocation):
                self.assertIs(expected, coverage.resolve_invocation(invocation, index))


def write(repo, relative, text):
    path = repo / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    if relative.startswith("tests/") and relative.endswith(".cs") and not re.search(r"\bclass\s+\w+", text):
        imports = re.findall(r"^\s*using\s+[\w.]+\s*;", text, re.MULTILINE)
        text = re.sub(r"^\s*using\s+[\w.]+\s*;", "", text, flags=re.MULTILINE)
        if not re.search(r"\b(public|private|internal)\s+(static\s+)?\w+(?:\[\])?\s+\w+\s*\(", text):
            attribute = "[Benchmark]" if "/Benchmark/" in relative else "[Fact]"
            text = re.sub(r"\[(?:Fact|BenchmarkExtension)\b[^\]]*\]", "", text)
            text = attribute + " public void Exercises() { " + text + " }"
        text = "\n".join(imports) + """
using Example;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
namespace Synthetic {
class Tests {
    readonly Driver fixture = new();
    readonly Driver host = new();
    readonly FixtureSupport.IndexFixture index = new();
    readonly Core.ArgumentParsing.ArgumentParsingService binder;
    readonly InitialSessionState state = InitialSessionState.CreateDefault2();
    readonly PowerShell shell = PowerShell.Create();
    void RunCli(params string[] args) => Core.Cli.Program.Main(args);
    void ExecuteCli(params string[] args) => Core.Cli.Program.Main(args);
    void RunCliAsync(params string[] args) => Core.Cli.Program.Main(args);
    void ExecuteCliAsync(params string[] args) => Core.Cli.Program.Main(args);
    void AddCommand(string name) => shell.AddCommand(name);
""" + text + """
}
class Driver {
    System.IServiceProvider serviceProvider;
    public void ExecuteCli(params string[] args) => Core.Cli.Program.Main(args);
    public void RunCli(params string[] args) => Core.Cli.Program.Main(args);
    public void RunCliAsync(params string[] args) => Core.Cli.Program.Main(args);
    public void ExecuteCliAsync(params string[] args) => Core.Cli.Program.Main(args);
    public void RunPowerShell(string name, object parameters) {
        var engine = serviceProvider.GetRequiredService<Core.Abstractions.IEngine>();
        var invocation = new CommandInvocation("Example", name, parameters);
        using var session = engine.OpenExecutionSession(invocation);
        session.Run();
    }
}
class ScriptScenarioRunner {
    public static void RunPlayback(string name) { Process.Start(name); }
}
}
namespace Core.Abstractions { interface IExtensionIndex {} interface IEngine {} }
namespace Core.ArgumentParsing { class ArgumentParsingService {} }
"""
        text = text.replace("namespace Synthetic {", "namespace Fixture" + re.sub(r"\W", "", relative) + " {")
        support = repo / "src/FixtureSupport/IndexFixture.cs"
        support.parent.mkdir(parents=True, exist_ok=True)
        support.write_text(
            "namespace FixtureSupport { class IndexFixture : Core.Abstractions.IExtensionIndex {} }",
            encoding="utf-8",
        )
    path.write_text(text, encoding="utf-8")
    return path


def command(surface="CLI", name="example", processor="Example.SharedProcessor"):
    return coverage.CommandCoverage("Example", surface, name, processor)


def hit(kind="cli-execution"):
    return coverage.TestHit("SharedTests", "tests/SharedTests.cs", "fixture", kind,
                            method="ExecutesAndAsserts", line=20)


class DiscoveryTests(unittest.TestCase):
    def test_setup_and_engine_calls_are_not_cmdlet_execution(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            cli, ps = command(), command("PowerShell", "Get-AzExample")
            reference = command("PowerShell", "Get-AzReference", "Other.ReferenceProcessor")
            write(repo, "tests/Example.Tests/Tests.cs", """
                [Fact] public void Setup() { var p = new SharedProcessor(); }
                [Fact] public void Engine() { host.RunPowerShell("Get-AzExample", parameters); }
                [Fact] public void Reference() { shell.AddCommand("Get-AzReference"); }
                [Fact] public void TypeOnly() { var t = typeof(ReferenceProcessor); }
            """)
            coverage.map_tests(repo, [cli, ps, reference])
            self.assertEqual({"processor-setup"}, {h.kind for h in cli.unit_tests})
            self.assertTrue(cli.has_processor_behavior())  # Shared engine behavior, not setup.
            self.assertEqual("inferred-shared", coverage.dimension_label(cli, "processorBehavior"))
            for candidate in (cli, ps, reference):
                self.assertEqual("no-mapped-signal", candidate.execution_status())
                self.assertFalse(candidate.dimension_hits()["binding"])
            self.assertFalse(reference.has_processor_behavior())
            self.assertEqual({"surface-reference"}, {h.kind for h in reference.unit_tests})

    def test_all_tiers_share_prefix_resolution(self):
        for helper in CLI_HELPERS:
            for script_name, script in (
                ("profile.sh", "az profile show details prod --verbose"),
                ("profile.ps1", "& $az profile show details prod --verbose"),
            ):
                with self.subTest(helper=helper, script=script_name), tempfile.TemporaryDirectory() as directory:
                    repo = Path(directory)
                    commands = [command(name=name, processor=f"Example.Processor{i}") for i, name in
                                enumerate(("profile", "profile show", "profile show details"))]
                    commands.append(command("PowerShell", "Get-AzProfile", "Example.PsProcessor"))
                    text = (f'{helper}(["profile", "show", "details", "prod"]);\n'
                            f'{helper}(dynamicArgs);\nAddCommand("Get-AzProfile");\n'
                            'AddCommand("Get-AzProfile extra");')
                    write(repo, "tests/Unit.Tests/ProfileTests.cs", "[Fact]\n" + text)
                    write(repo, "tests/Scenario.Tests/ProfileScenario.cs",
                          text + f'\nScriptScenarioRunner.RunPlayback("{script_name}");')
                    write(repo, f"tests/Scenario.Tests/Scripts/{script_name}",
                          script + ("\naz unknown show" if script_name.endswith(".sh") else
                                    "\nGet-AzProfile -Name prod\n& $az unknown show"))
                    write(repo, "tests/Benchmark/CLI/ProfileBenchmarks.cs",
                          '[BenchmarkExtension("Example")]\n' + text)
                    write(repo, "tests/Benchmark/CLI/OtherBenchmarks.cs",
                          '[BenchmarkExtension("Other")]\n' + text)
                    unmapped = coverage.map_tests(repo, commands)
                    for candidate in commands[:2]:
                        for tier in ("unit_tests", "scenario_tests", "script_scenario_tests", "benchmarks"):
                            self.assertEqual([], getattr(candidate, tier))
                    mapped = commands[2]
                    for tier in ("unit_tests", "scenario_tests", "script_scenario_tests", "benchmarks"):
                            self.assertEqual(2 if tier == "benchmarks" else 1, len(getattr(mapped, tier)))
                    self.assertEqual("Good", mapped.status())
                    self.assertEqual(4, mapped.score())
                    self.assertIsNone(mapped.score_maximum())
                    self.assertEqual({"unit", "scenario", "benchmark"},
                                     {t.tier for t in unmapped if t.evidence == f"{helper}(dynamicArgs)"})
                    self.assertEqual({"unit", "scenario", "benchmark"},
                                     {t.tier for t in unmapped if t.evidence == "PowerShell:get-azprofile extra"})
                    self.assertTrue(any(t.tier == "script-scenario" and t.evidence == "CLI:unknown show"
                                        for t in unmapped))
                    self.assertEqual(int(script_name.endswith(".ps1")),
                                     len(commands[3].script_scenario_tests))

    def test_metadata_helpers_and_binding_are_discovered_without_dispatch_credit(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            cli, ps = command(), command("PowerShell", "Get-AzExample")
            write(repo, "tests/Example.Tests/Tests.cs", """
                using Example;
                [Fact] public void Helper() {
                    var result = SharedProcessor.CreateInfo();
                    Assert.True(result.Enabled);
                }
                [Theory]
                [InlineData(ClientType.CLI, "example")]
                [InlineData(ClientType.PowerShell, "Get-AzExample")]
                public void Registered(ClientType clientType, string commandName) {
                    var metadata = index.GetCommandMetadata(commandName, clientType);
                    Assert.Equal(typeof(SharedProcessor), metadata.ProcessorType);
                }
                [Fact] public void Binding() {
                    var metadata = index.GetCommandMetadata("Get-AzExample", ClientType.PowerShell);
                    binder.BindArgumentsToProcessor(processor, args, metadata.Parameters);
                    Assert.True(processor.Enabled);
                }
            """)
            coverage.map_tests(repo, [cli, ps])
            self.assertEqual({"registration", "processor-helper"}, {h.kind for h in cli.unit_tests})
            self.assertEqual({"registration", "processor-helper", "binding"}, {h.kind for h in ps.unit_tests})
            for candidate in (cli, ps):
                self.assertFalse(candidate.has_processor_behavior())
                self.assertEqual("Partial", candidate.status())
                self.assertEqual("no-mapped-signal", candidate.execution_status())
                self.assertTrue(all(h.method and h.line and h.reason for h in candidate.unit_tests))

    def test_helper_type_resolution_is_conservative(self):
        for imports, call in (
            ("using Example;\nusing Other;", "SharedProcessor.CreateInfo()"),
            ("using Example;", "Other.SharedProcessor.CreateInfo()"),
            ("using Example;", "typeof(SharedProcessor)"),
        ):
            with self.subTest(imports=imports, call=call), tempfile.TemporaryDirectory() as directory:
                repo = Path(directory)
                commands = [command(), command(processor="Other.SharedProcessor", name="other")]
                write(repo, "tests/Example.Tests/Tests.cs",
                      imports + f"\n[Fact] public void Check() {{ var info = {call}; Assert.NotNull(info); }}")
                coverage.map_tests(repo, commands)
                self.assertFalse(commands[0].dimension_hits()["processorHelper"])
                self.assertEqual(call == "Other.SharedProcessor.CreateInfo()",
                                 bool(commands[1].dimension_hits()["processorHelper"]))
                self.assertFalse(any(c.has_processor_behavior() for c in commands))

    def test_method_boundaries_do_not_borrow_assertions_from_another_method(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            candidate = command()
            write(repo, "tests/Example.Tests/Tests.cs", """
                using Example;
                [Fact] public void NotAsserted() { SharedProcessor.CreateInfo(); }
                [Fact] public void Other() { var text = "}"; Assert.NotNull(text); }
                private void NotATest() { SharedProcessor.CreateInfo(); Assert.True(true); }
            """)
            coverage.map_tests(repo, [candidate])
            self.assertFalse(candidate.dimension_hits()["processorHelper"])

    def test_runspace_execution_requires_registration_wiring_and_invoke(self):
        source = """
            [Fact] public void Executes() {
                state.Commands.Add(new SessionStateCmdletEntry("Get-AzExample", typeof(ExampleCmdlet), null));
                using var runspace = RunspaceFactory.CreateRunspace(state);
                runspace.Open();
                Assert.NotNull(Invoke(runspace, "Get-AzExample", parameters));
            }
            private static List<object> Invoke(Runspace runspace, string command, object parameters) {
                using var ps = PowerShell.Create();
                ps.Runspace = runspace;
                return ps.AddCommand(command).AddParameters(parameters).Invoke();
            }
        """
        for old, replacement in (
            ("", ""), ("ps.Runspace = runspace;", ""), (".Invoke()", ""),
            ('new SessionStateCmdletEntry("Get-AzExample"', 'new SessionStateCmdletEntry("Get-Other"'),
            ("runspace.Open();", ""),
        ):
            with self.subTest(missing=old), tempfile.TemporaryDirectory() as directory:
                repo = Path(directory)
                candidate = command("PowerShell", "Get-AzExample")
                write(repo, "tests/Example.Tests/Tests.cs",
                      source.replace(old, replacement) if old else source)
                coverage.map_tests(repo, [candidate])
                self.assertEqual("mapped" if not old else "no-mapped-signal", candidate.execution_status())


class SyntaxFlowTests(unittest.TestCase):
    def test_rooted_cli_helper_forms_resolve_real_helper_bodies(self):
        for helper in CLI_HELPERS:
            for declaration, argument in (
                ("", '"profile", "show", "prod"'),
                ("", '["profile", "show", "prod"]'),
                ("", 'new[] { "profile", "show", "prod" }'),
                ('string[] Args() => ["profile", "show", "prod"];', "Args()"),
                ('var args = new[] { "profile", "show", "prod" };', "args"),
            ):
                with self.subTest(helper=helper, argument=argument), tempfile.TemporaryDirectory() as directory:
                    repo = Path(directory)
                    candidate = command(name="profile show")
                    write(repo, "tests/Flow.Tests/Forms.cs",
                          f"[Fact] public void Executes() {{ {declaration} fixture.{helper}({argument}); }}")
                    coverage.map_tests(repo, [candidate])
                    self.assertEqual("mapped", candidate.execution_status())
                    self.assertEqual({"Executes"}, {h.method for _, h in candidate.dimension_hits()["surfaceExecution"]})

    def test_wired_script_is_a_candidate_not_unconditional_execution_proof(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "tests/Scenario.Tests/Recorded.cs",
                  '[Fact] public void Playback() { ScriptScenarioRunner.RunPlayback("recorded.sh"); }')
            write(repo, "tests/Scenario.Tests/Scripts/recorded.sh", "if false; then\naz example\nfi")
            candidate = command()
            coverage.map_tests(repo, [candidate])
            self.assertEqual(["script-candidate"], [h.kind for h in candidate.script_scenario_tests])
            self.assertFalse(candidate.has_processor_behavior())
            self.assertEqual("no-mapped-signal", candidate.execution_status())
            self.assertEqual("Partial", candidate.status())

    def test_processor_execution_follows_instances_factories_fields_and_error_callbacks(self):
        source = """
using Example;
namespace Flow {
class Tests {
    readonly SharedProcessor field = new();
    readonly SharedProcessor constructed;
    public Tests() { constructed = new SharedProcessor(); }
    static T Create<T>() where T : new() => new T();
    static (SharedProcessor, object) Wrap(SharedProcessor p) => (p, new object());
    static async Task Run(SharedProcessor p) { await p.ProcessAsync(); }
    static IProcessor Choose(bool first) => first ? new SharedProcessor() : new Other.SharedProcessor();
    [Fact] public async Task Local() { var p = new SharedProcessor(); await p.ProcessAsync(); Assert.True(true); }
    [Fact] public async Task Field() { await field.ProcessAsync(); Assert.True(true); }
    [Fact] public async Task Constructor() { await constructed.ProcessAsync(); Assert.True(true); }
    [Fact] public async Task Generic() { var p = Create<SharedProcessor>(); await Run(p); Assert.True(true); }
    [Fact] public async Task Tuple() { var (p, output) = Wrap(new SharedProcessor()); await p.ProcessAsync(); Assert.NotNull(output); }
    [Fact] public async Task Error() {
        var p = new SharedProcessor();
        await Assert.ThrowsAsync<Exception>(() => p.ProcessAsync());
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Cases(bool first) { var p = Choose(first); await p.ProcessAsync(); Assert.True(true); }
    [Fact] public void Scope() {
        { var field = new Other.SharedProcessor(); field.ProcessAsync(); }
        field.ProcessAsync(); Assert.True(true);
    }
}
}
"""
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "tests/Flow.Tests/Flow.cs", source)
            commands = [command(), command("PowerShell", "Get-Shared"), command(name="other", processor="Other.SharedProcessor")]
            coverage.map_tests(repo, commands)
            expected = {"Local", "Field", "Constructor", "Generic", "Tuple", "Error", "Cases", "Scope"}
            for candidate in commands[:2]:
                proofs = candidate.dimension_hits()["processorBehavior"]
                self.assertEqual(expected, {h.method for _, h in proofs})
                self.assertEqual("no-mapped-signal", candidate.execution_status())
                self.assertTrue(all(h.call_path and h.line and h.assertion_status == "detected" for _, h in proofs))
                self.assertFalse(candidate.dimension_hits()["binding"])
            self.assertEqual({"Cases", "Scope"}, {h.method for _, h in commands[2].dimension_hits()["processorBehavior"]})

    def test_unused_setup_dead_paths_callbacks_and_overrides_do_not_create_behavior(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/Extensions/Example/SharedProcessor.cs",
                  "namespace Example { class SharedProcessor { public virtual Task ProcessAsync() => Task.CompletedTask; } }")
            write(repo, "tests/Flow.Tests/Negative.cs", """
using Example;
namespace Flow {
class Stub : SharedProcessor { public override Task ProcessAsync() => Task.CompletedTask; }
class Tests {
    [Fact] public void SetupOnly() { var p = new SharedProcessor(); Assert.NotNull(p); }
    private void Unused() { new SharedProcessor().ProcessAsync(); }
    [Fact] public void UnusedDelegate() {
        var p = new SharedProcessor(); Action action = () => p.ProcessAsync(); Assert.NotNull(action);
    }
    [Fact] public void StoredDelegate() { Assert.NotNull(() => new SharedProcessor().ProcessAsync()); }
    [Fact] public void DeadBranch() { if (false) new SharedProcessor().ProcessAsync(); Assert.True(true); }
    [Fact] public void AfterReturn() { return; new SharedProcessor().ProcessAsync(); }
    static SharedProcessor ThrowingFactory() { throw new Exception(); }
    [Fact] public void AfterThrowingHelper() { var p = ThrowingFactory(); p.ProcessAsync(); }
    [Fact] public void ConditionalReturn() { if (true) return; new SharedProcessor().ProcessAsync(); }
    [Fact] public void NullReceiver() { SharedProcessor p = null; p.ProcessAsync(); }
    [Fact] public void UnknownMockObject() { SharedProcessor p = mock.Object; p.ProcessAsync(); Assert.NotNull(p); }
    [Fact] public void Override() { new Stub().ProcessAsync(); Assert.True(true); }
    [Theory] [MemberData(nameof(Data))]
    public void DynamicData(string command) { new SharedProcessor().ProcessAsync(); }
    [Fact(Skip="disabled")] public void Skipped() { new SharedProcessor().ProcessAsync(); }
    [Fact] public void UnknownCondition() { if (EnvironmentFlag) new SharedProcessor().ProcessAsync(); Assert.True(true); }
    [Fact] public void CommentsAndStrings() {
        // new SharedProcessor().ProcessAsync();
        var s = "new SharedProcessor().ProcessAsync()"; Assert.NotNull(s);
    }
}
}
""")
            candidate = command()
            coverage.map_tests(repo, [candidate])
            self.assertFalse(candidate.has_processor_behavior())
            self.assertEqual("no-mapped-signal", candidate.execution_status())
            reasons = candidate.analysis["diagnosticCounts"]
            for reason in ("unresolved-test-data", "skipped-test", "unresolved-conditional-branch"):
                self.assertIn(reason, reasons)

    def test_namespaces_and_framework_name_lookalikes_do_not_cross_credit(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/Core/Interfaces.cs", "namespace Core.Abstractions { interface IExtensionIndex {} }")
            write(repo, "tests/Flow.Tests/Names.cs", """
using Example;
using Other;
using Chosen = Example.SharedProcessor;
namespace Flow {
class Fake : Core.Abstractions.IExtensionIndex {
    public object GetCommandMetadata(string name, object client) => null;
    public Fake Parse(string[] args) => this;
    public Fake OpenExecutionSession(Fake invocation) => this;
    public void Run() {}
    public void RunCli(params string[] args) {}
}
class Tests {
    [Fact] public void Ambiguous() { var p = new SharedProcessor(); p.ProcessAsync(); Assert.True(true); }
    [Fact] public void Alias() { new Chosen().ProcessAsync(); Assert.True(true); }
    [Fact] public void FullyQualified() { new Other.SharedProcessor().ProcessAsync(); Assert.True(true); }
    [Fact] public void FakeDispatch() {
        var fake = new Fake(); fake.RunCli("example");
        fake.OpenExecutionSession(fake.Parse(["example"])).Run();
        var metadata = fake.GetCommandMetadata("example", ClientType.CLI);
        Assert.NotNull(metadata);
    }
}
}
""")
            cli, other = command(), command(name="other", processor="Other.SharedProcessor")
            coverage.map_tests(repo, [cli, other])
            self.assertEqual({"Alias"}, {h.method for _, h in cli.dimension_hits()["processorBehavior"]})
            self.assertEqual({"FullyQualified"}, {h.method for _, h in other.dimension_hits()["processorBehavior"]})
            self.assertFalse(cli.dimension_hits()["registration"])
            self.assertEqual("no-mapped-signal", cli.execution_status())
            self.assertIn("ambiguous-type", cli.analysis["diagnosticCounts"])

    def test_generic_and_non_generic_base_identities_do_not_collapse(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/Extensions/Example/Processors.cs", """
namespace Example {
class ProcessorBase { public virtual Task ProcessAsync() => Task.CompletedTask; }
class ProcessorBase<T> : ProcessorBase { public override Task ProcessAsync() => Task.CompletedTask; }
class SharedProcessor : ProcessorBase<object> {}
}
""")
            write(repo, "tests/Flow.Tests/Inherited.cs", """
using Example;
namespace Flow {
class Tests {
    [Fact] public void InheritedProductBehavior() { new SharedProcessor().ProcessAsync(); Assert.True(true); }
}
}
""")
            candidate = command()
            coverage.map_tests(repo, [candidate])
            self.assertEqual({"InheritedProductBehavior"},
                             {h.method for _, h in candidate.dimension_hits()["processorBehavior"]})
            self.assertNotIn("ambiguous-overload", candidate.analysis["diagnosticCounts"])

    def test_literal_switch_and_try_do_not_execute_other_arms_or_exception_handlers(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "tests/Flow.Tests/Control.cs", """
using Example;
namespace Flow {
class Tests {
    [Theory] [InlineData("left")]
    public void SelectedArm(string name) {
        var p = name switch { "left" => new SharedProcessor(), _ => new Other.SharedProcessor() };
        p.ProcessAsync(); Assert.True(true);
    }
    [Fact] public void TryDoesNotProveCatch() {
        try { Core.Cli.Program.Main(["example"]); }
        catch { new Other.SharedProcessor().ProcessAsync(); }
        finally { Assert.True(true); }
    }
    [Fact] public void UnsupportedLoop() {
        for (int i = 0; i < 0; i++) { new Other.SharedProcessor().ProcessAsync(); }
    }
}
}
""")
            first, second = command(), command(name="other", processor="Other.SharedProcessor")
            coverage.map_tests(repo, [first, second])
            self.assertEqual({"SelectedArm", "TryDoesNotProveCatch"},
                             {h.method for _, h in first.dimension_hits()["processorBehavior"]})
            self.assertFalse(second.unit_tests)
            self.assertIn("unsupported-control-flow", first.analysis["diagnosticCounts"])

    def test_same_full_processor_name_in_different_projects_is_unknown_not_shared(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            for extension in ("One", "Two"):
                write(repo, f"src/Extensions/{extension}/Shared.cs",
                      "namespace Collision { class Shared { public Task ProcessAsync() => Task.CompletedTask; } }")
            write(repo, "tests/Flow.Tests/Collision.cs", """
namespace Flow {
class Tests {
    [Fact] public void AmbiguousIdentity() { new Collision.Shared().ProcessAsync(); Assert.True(true); }
    [Fact] public void KnownDispatch() { Core.Cli.Program.Main(["one"]); Assert.True(true); }
}
}
""")
            first, second = command(name="one", processor="Collision.Shared"), command(name="two", processor="Collision.Shared")
            first.extension, second.extension = "One", "Two"
            coverage.map_tests(repo, [first, second])
            self.assertEqual("mapped", first.execution_status())
            self.assertEqual("Unknown", first.status())
            self.assertEqual("Unknown", second.status())
            self.assertFalse(second.has_processor_behavior())
            self.assertEqual([], second.inferred_processor_tests)
            self.assertIn("ambiguous-processor-identity", first.analysis["diagnosticCounts"])

    def test_benchmark_setup_is_not_workload_and_names_or_tags_cannot_grant_command_credit(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "tests/Benchmark/CLI/Workload.cs", """
namespace Flow {
[BenchmarkExtension("Example")] class Benchmarks {
    [GlobalSetup] public void Login() { Core.Cli.Program.Main(["example"]); }
    [Benchmark] public void Unrelated() { var text = "example SharedProcessor"; }
    [Benchmark] public void ActualWorkload() { Core.Cli.Program.Main(["other"]); }
}
}
""")
            cli, other = command(), command(name="other", processor="Other.Processor")
            coverage.map_tests(repo, [cli, other])
            self.assertEqual([], cli.benchmarks)
            self.assertEqual(["ActualWorkload"], [h.method for h in other.benchmarks])
            self.assertFalse(other.has_processor_behavior())
            self.assertEqual("no-mapped-signal", other.execution_status())

    def test_generic_cmdlet_helper_traces_reflection_registration_and_invoke_not_internal_aliases(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "tests/Flow.Tests/Cmdlets.cs", """
using System.Management.Automation;
using System.Management.Automation.Runspaces;
namespace Flow {
[Cmdlet(VerbsCommon.Get, "AzExample")] class ExampleCmdlet {}
class Tests {
    [Fact] public void RealRunspace() { var output = Invoke<ExampleCmdlet>(); Assert.NotNull(output); }
    object Invoke<T>() {
        var attr = (CmdletAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(CmdletAttribute));
        var name = $"{attr.VerbName}-{attr.NounName}";
        var state = InitialSessionState.CreateDefault2();
        state.Commands.Add(new SessionStateCmdletEntry(name, typeof(T), null));
        using var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        using var shell = PowerShell.Create();
        shell.Runspace = runspace;
        return shell.AddCommand(name).Invoke();
    }
}
}
""")
            public = command("PowerShell", "Get-AzExample")
            route = command("PowerShell", "Get-AzExample_List")
            coverage.map_tests(repo, [public, route])
            self.assertEqual("mapped", public.execution_status())
            self.assertEqual("no-mapped-signal", route.execution_status())
            self.assertTrue(route.has_processor_behavior())
            self.assertFalse(route.dimension_hits()["binding"])

    def test_static_imported_parameterized_helper_and_global_cli_parser_preserve_execution(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/CLI/Parser/GlobalArgParser.cs",
                  "namespace Core.Cli.Parser { class GlobalArgParser {} class ExtensionDispatcher {} }")
            write(repo, "src/Core/Types.cs", "namespace Core.Abstractions { interface IEngine {} }")
            write(repo, "tests/Flow.Tests/Factory.cs", """
using static Flow.Factory;
using Example;
using Core.Cli.Parser;
namespace Flow {
class Factory { public static T Configure<T>(T p) => p; }
class Tests {
    System.IServiceProvider services;
    [Fact] public void Imported() { var p = Configure(new SharedProcessor()); p.ProcessAsync(); Assert.True(true); }
    [Fact] public void GlobalArguments() {
        var global = GlobalArgParser.Parse(["example", "positional", "--output", "json"]);
        var dispatcher = new ExtensionDispatcher();
        var invocation = dispatcher.Parse(global.Positional.ToArray());
        var engine = services.GetRequiredService<Core.Abstractions.IEngine>();
        using var session = engine.OpenExecutionSession(invocation);
        session.Run(); Assert.True(true);
    }
}
}
""")
            candidate = command()
            coverage.map_tests(repo, [candidate])
            self.assertEqual({"Imported", "GlobalArguments"},
                             {h.method for _, h in candidate.dimension_hits()["processorBehavior"]})
            self.assertEqual({"GlobalArguments"}, {h.method for _, h in candidate.dimension_hits()["surfaceExecution"]})

    def test_source_factory_inventory_uses_parameters_groups_and_guards_without_extension_rules(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/Extensions/Fresh/Processors.cs", "namespace Widgets { class A {} class B {} }")
            write(repo, "src/Extensions/Fresh/ExtensionIndex.cs", """
using Widgets;
namespace Widgets {
class ExtensionIndex {
    static object cliTable = Build(true);
    static object psTable = Build(false);
    static object Build(bool cli) {
        var table = new Dictionary<string, CommandMetadata>();
        Put<A>("show", "Get-One");
        Put<B>("show", "Get-Two", second: true);
        if (!cli) Put<A>("recover", "Undo-One");
        return table;
        void Put<T>(string verb, string ps, bool second = false) {
            string entity = second ? "two" : "one";
            string invocation = cli ? $"widgets {entity} {verb}" : ps;
            table.Add(invocation, new CommandMetadata(InvocationName: invocation, ProcessorType: typeof(T)));
        }
    }
}
}
""")
            commands = coverage.parse_commands(repo)
            self.assertEqual({
                ("CLI", "widgets one show", "Widgets.A"), ("CLI", "widgets two show", "Widgets.B"),
                ("PowerShell", "Get-One", "Widgets.A"), ("PowerShell", "Get-Two", "Widgets.B"),
                ("PowerShell", "Undo-One", "Widgets.A"),
            }, {(c.surface, c.invocation_name, c.processor_type) for c in commands})

    def test_commented_registration_does_not_win_and_conflicting_real_registration_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            source = '''
CommandMetadata old = new(
 InvocationName: "New-One",
 FullName: "Example.OldProcessor"
);
CommandMetadata current = new(
 InvocationName: "New-One",
 FullName: "Example.CurrentProcessor"
);
psCommandTable = new()
{
 // ["New-One"] = old,
 ["New-One"] = current
};
'''
            path = write(repo, "src/Extensions/Fresh/ExtensionIndex.cs", source)
            self.assertEqual(["Example.CurrentProcessor"], [c.processor_type for c in coverage.parse_commands(repo)])
            path.write_text(source.replace('// ["New-One"]', '["New-One"]'), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Duplicate registration"):
                coverage.parse_commands(repo)

    def test_unresolved_registration_reference_fails_instead_of_publishing_partial_inventory(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            source = '''
CommandMetadata known = new(
 InvocationName: "known",
 FullName: "Example.Known"
);
cliCommandTable = BuildCommandTable(known, unparsed);
'''
            write(repo, "src/Extensions/Example/ExtensionIndex.cs", source)
            with self.assertRaisesRegex(ValueError, "Unresolved registration"):
                coverage.parse_commands(repo)

    def test_resolved_factory_name_with_unknown_processor_stays_in_inventory_as_unknown(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/Extensions/Fresh/ExtensionIndex.cs", """
namespace Fresh {
class ExtensionIndex {
    static object cliTable = Build(true);
    static object Build(bool cli) {
        var table = new Dictionary<string, CommandMetadata>();
        Put<ExternalProcessor>("known-name");
        return table;
        void Put<T>(string name) {
            table.Add(name, new CommandMetadata(InvocationName: name, ProcessorType: typeof(T)));
        }
    }
}
}
""")
            commands = coverage.parse_commands(repo)
            self.assertEqual(["known-name"], [c.invocation_name for c in commands])
            self.assertEqual("Unknown", commands[0].status())

    def test_ambiguous_overloads_and_unwired_scripts_remain_diagnostic_not_behavior(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "tests/Flow.Tests/Ambiguous.cs", """
using Example;
namespace Flow {
class Tests {
    static SharedProcessor Make(int value) => new SharedProcessor();
    static SharedProcessor Make(string value) => new SharedProcessor();
    [Fact] public void AmbiguousFactory() { var p = Make(input); p.ProcessAsync(); Assert.True(true); }
}
}
""")
            write(repo, "tests/Scenario.Tests/Scripts/manual.ps1", "Get-AzExample\naz example")
            cli, ps = command(), command("PowerShell", "Get-AzExample")
            coverage.map_tests(repo, [cli, ps])
            self.assertFalse(cli.has_processor_behavior())
            self.assertFalse(ps.has_processor_behavior())
            self.assertEqual([], cli.script_scenario_tests)
            self.assertEqual([], ps.script_scenario_tests)
            self.assertIn("ambiguous-overload", cli.analysis["diagnosticCounts"])

    def test_throwing_lifecycle_cannot_grant_workload_or_test_execution(self):
        for directory_name, annotation, initializer in (
            ("Flow.Tests", "Fact", "public Tests() { throw new Exception(); }"),
            ("Benchmark", "Benchmark", "[GlobalSetup] public void Setup() { throw new Exception(); }"),
        ):
            with self.subTest(tier=directory_name), tempfile.TemporaryDirectory() as directory:
                repo = Path(directory)
                write(repo, f"tests/{directory_name}/Throws.cs", f"""
namespace Flow {{
class Tests {{
    {initializer}
    [{annotation}] public void NeverReached() {{ Core.Cli.Program.Main(["example"]); }}
}}
}}
""")
                candidate = command()
                coverage.map_tests(repo, [candidate])
                self.assertEqual([], candidate.unit_tests)
                self.assertEqual([], candidate.benchmarks)
                self.assertIn("unreachable-test-root", candidate.analysis["diagnosticCounts"])

    def test_qualified_inline_data_remains_usable_with_additional_unsupported_data(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "tests/Flow.Tests/Data.cs", """
namespace Flow {
class Tests {
    [Theory] [Xunit.InlineDataAttribute("example")] [MemberData(nameof(Dynamic))]
    public void Mixed(string name) { Core.Cli.Program.Main([name]); Assert.True(true); }
}
}
""")
            candidate = command()
            coverage.map_tests(repo, [candidate])
            self.assertEqual("mapped", candidate.execution_status())
            self.assertEqual("partially-resolved", candidate.analysis["roots"][0]["dataStatus"])
            self.assertIn("unresolved-test-data", candidate.analysis["diagnosticCounts"])

    def test_gap_indicators_use_full_inventory_and_unknown_is_never_green(self):
        good, partial, gap, unknown = command(name="good"), command(name="partial"), command(name="gap"), command(name="unknown", processor="")
        good.unit_tests.append(hit())
        partial.unit_tests.append(hit("registration"))
        self.assertEqual("\U0001f7e2", coverage.extension_indicator([good], "Example"))
        for commands, indicator in (
            ([good, partial], "\U0001f7e1"), ([good, unknown], "\U0001f7e1"),
            ([good, partial, gap, unknown], "\U0001f534"),
        ):
            self.assertEqual(indicator, coverage.extension_indicator(commands, "Example"))
            for render in (coverage.as_markdown, coverage.as_html):
                rendered = render(commands, "commit", {}, [])
                self.assertIn(coverage.INDICATOR_LEGEND, rendered)
                self.assertIn(indicator, rendered)
                self.assertNotIn("`good`", rendered)
            report = coverage.as_json(commands, "commit", {}, [])
            self.assertEqual("Example", report["extensions"][0]["name"])


class SharedBehaviorTests(unittest.TestCase):
    def test_automatic_sharing_preserves_provenance_without_surface_credit(self):
        cli, ps, alias = command(), command("PowerShell", "Get-Example"), command(name="example alias")
        cli.unit_tests.append(hit())
        unrelated, missing = command(processor="Other.SharedProcessor"), command(processor="")
        commands = [cli, ps, alias, unrelated, missing]
        coverage.infer_shared_processor_behavior(commands)
        for candidate in (ps, alias):
            self.assertEqual("inferred-shared", coverage.dimension_label(candidate, "processorBehavior"))
            self.assertEqual("no-mapped-signal", candidate.execution_status())
            self.assertFalse(candidate.dimension_hits()["binding"])
            self.assertFalse(candidate.dimension_hits()["registration"])
            self.assertEqual([], candidate.unit_tests)
            self.assertEqual(0, candidate.score())
            self.assertEqual(1, candidate.required_score())
            self.assertEqual("Partial", candidate.status())
            self.assertNotIn("no-mapped-processor-tests", candidate.gaps())
            tier, proof = candidate.inferred_processor_tests[0]
            self.assertEqual(("unit", "processor-behavior", "inferred-shared-processor", "CLI:example",
                              "ExecutesAndAsserts", 20),
                             (tier, proof.kind, proof.confidence, proof.shared_from, proof.method, proof.line))
            self.assertIn("Not an independent test", proof.reason)
        self.assertEqual([], unrelated.inferred_processor_tests)
        self.assertEqual([], missing.inferred_processor_tests)
        snapshot = copy.deepcopy(commands)
        coverage.infer_shared_processor_behavior(commands)
        self.assertEqual(snapshot, commands)

    def test_non_execution_and_benchmarks_do_not_trigger_sharing(self):
        for kind in ("processor-setup", "surface-reference", "processor-helper",
                     "processor-behavior", "registration", "binding"):
            with self.subTest(kind=kind):
                source, target = command(), command("PowerShell", "Get-Target")
                source.unit_tests.append(hit(kind))
                source.benchmarks.append(hit())
                coverage.infer_shared_processor_behavior([source, target])
                self.assertEqual([], target.inferred_processor_tests)

    def test_scenario_sharing_never_transfers_recordings_or_applicability(self):
        for tier in ("scenario_tests", "script_scenario_tests"):
            with self.subTest(tier=tier):
                source, target = command("PowerShell", "Get-Source"), command()
                proof = hit("engine-execution")
                proof.scenario_type = "record-playback"
                getattr(source, tier).append(proof)
                coverage.infer_shared_processor_behavior([source, target])
                self.assertTrue(target.has_processor_behavior())
                self.assertEqual([], target.scenario_hits())
                self.assertEqual("Unknown", target.scenario_label([]))
                self.assertNotIn("missing-scenario", target.gaps())
                self.assertIsNone(target.score_maximum())
                self.assertEqual(0, target.score())
                self.assertEqual(1, target.required_score())
                self.assertEqual("", target.inferred_processor_tests[0][1].scenario_type)

    def test_actual_powershell_proof_does_not_grant_cli_execution(self):
        source, target = command("PowerShell", "Get-Source"), command()
        source.unit_tests.append(hit("ps-execution"))
        coverage.infer_shared_processor_behavior([source, target])
        self.assertEqual("Partial", target.status())
        self.assertEqual("no-mapped-signal", target.execution_status())
        self.assertFalse(target.dimension_hits()["binding"])
        self.assertEqual([target], coverage.priority_gaps([target]))

    def test_direct_proof_precedes_inference_and_duplicate_sources_earn_no_points(self):
        source, direct, target = command(), command("PowerShell", "Get-Direct"), command("PowerShell", "Get-Target")
        source.unit_tests.extend([hit(), hit()])
        direct.unit_tests.append(hit("processor-behavior"))
        coverage.infer_shared_processor_behavior([source, direct, target])
        self.assertEqual([], direct.inferred_processor_tests)
        self.assertEqual(1, len(target.inferred_processor_tests))
        self.assertEqual(1, target.required_score())


class AutomaticReportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture = tempfile.TemporaryDirectory()
        cls.addClassCleanup(cls.fixture.cleanup)
        cls.repo = Path(cls.fixture.name)
        registrations = {
            "Accounts": [
                ("CLI", "profile list", "ProfileListProcessor"),
                ("CLI", "profile show", "ProfileShowProcessor"),
                ("CLI", "profile set", "ProfileSetProcessor"),
                ("CLI", "profile remove", "ProfileRemoveProcessor"),
                ("PowerShell", "Get-AzProfile_List", "ProfileListProcessor"),
                ("CLI", "survey", "OpenSurveyProcessor"),
                ("PowerShell", "Open-AzSurvey", "OpenSurveyProcessor"),
                ("PowerShell", "Get-AzAccessToken", "GetAccessTokenProcessor"),
            ],
            "Config": [
                *[("CLI", f"config {verb}", f"Config{verb.title()}Processor")
                  for verb in ("get", "set", "delete", "list", "info")],
                ("PowerShell", "Get-AzConfig", "ConfigGetProcessor"),
            ],
            "Network": [("PowerShell", "New-AzNetworkInterface", "NewNetworkInterfaceProcessor")],
            "Resources": [("CLI", "group create", "NewResourceGroupProcessor")],
        }
        for extension, entries in registrations.items():
            declarations, tables = [], {"CLI": [], "PowerShell": []}
            for i, (surface, name, processor) in enumerate(entries):
                declarations.append(
                    f'CommandMetadata command{i} = new(\n InvocationName: "{name}",\n'
                    f' FullName: "Microsoft.Azure.Azclips.Extensions.{extension}.Processors.{processor}"\n);'
                )
                tables[surface].append(f"command{i}")
            write(cls.repo, f"src/Extensions/{extension}/ExtensionIndex.cs",
                  "\n".join(declarations) + "\n" + "\n".join(
                      f"{'cli' if surface == 'CLI' else 'ps'}CommandTable = BuildCommandTable({', '.join(names)});"
                      for surface, names in tables.items() if names))
        write(cls.repo, "tests/Cli.Tests/ProfileCliTests.cs", """
            [Fact] public void ProfileList_ShouldReturnEmpty_WhenNoProfiles() {
                var result = fixture.ExecuteCli(["profile", "list"]); Assert.Empty(result);
            }
            [Fact] public void ProfileList_ShouldIndicateActiveProfile() {
                var result = fixture.ExecuteCli(["profile", "list"]); Assert.Contains("active", result);
            }
            [Fact] public void OtherOperations() {
                fixture.ExecuteCli(["profile", "show", "prod"]);
                fixture.ExecuteCli(["profile", "set", "prod"]);
                fixture.ExecuteCli(["profile", "remove", "prod"]);
            }
        """)
        write(cls.repo, "tests/Cli.Tests/Configuration/ConfigCliTests.cs", """
            [Fact] public void ConfigOperations() {
                fixture.ExecuteCli(["config", "get", "core.name"]);
                fixture.ExecuteCli(["config", "set", "core.name", "value"]);
                fixture.ExecuteCli(["config", "delete", "core.name"]);
                fixture.ExecuteCli(["config", "list"]);
                fixture.ExecuteCli(["config", "info"]);
            }
            [Fact] public void Metadata() {
                var metadata = index.GetCommandMetadata("Get-AzConfig", ClientType.PowerShell);
                Assert.NotNull(metadata);
            }
        """)
        write(cls.repo, "tests/Extensions.Accounts.Tests/OpenSurveyTests.cs", """
            using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
            [Fact] public void CreateStartInfo_UsesSharedSurveyUrlAndSystemShell() {
                var info = OpenSurveyProcessor.CreateStartInfo(); Assert.True(info.UseShellExecute);
            }
            [Theory]
            [InlineData(ClientType.CLI, "survey")]
            [InlineData(ClientType.PowerShell, "Open-AzSurvey")]
            public void CommandSurface_UsesOpenSurveyProcessor(ClientType clientType, string commandName) {
                var metadata = index.GetCommandMetadata(commandName, clientType);
                Assert.Equal(typeof(OpenSurveyProcessor), metadata.ProcessorType);
            }
        """)
        write(cls.repo, "tests/Cmdlets.Tests/GetAccessTokenCmdletTests.cs", """
            [Fact] public void PSBinding_MapsTenantId_OntoProcessorProperty() {
                var metadata = index.GetCommandMetadata("Get-AzAccessToken", ClientType.PowerShell);
                binder.BindArgumentsToProcessor(processor, args, metadata.Parameters);
                Assert.Equal(expected, processor.Tenant);
            }
        """)
        write(cls.repo, "tests/Scenario.Tests/NetworkInterfaceScenarioTests.cs", """
            [Fact] public void PowerShellCmdlets_ConvertTheirParameters_AndRouteParameterSets() {
                state.Commands.Add(new SessionStateCmdletEntry("New-AzNetworkInterface", typeof(NewNetworkInterfaceCmdlet), null));
                using var runspace = RunspaceFactory.CreateRunspace(state);
                runspace.Open();
                Assert.NotNull(Invoke(runspace, "New-AzNetworkInterface", parameters));
            }
            private static List<object> Invoke(Runspace runspace, string command, object parameters) {
                using var ps = PowerShell.Create();
                ps.Runspace = runspace;
                return ps.AddCommand(command).AddParameters(parameters).Invoke();
            }
        """)
        write(cls.repo, "tests/Scenario.Tests/ResourceGroupTests.cs",
              '[Fact] public void CreatesGroup() { host.RunCli(["group", "create"]); }')
        cls.commands = coverage.parse_commands(cls.repo)
        cls.keys = {(c.extension, c.surface, c.invocation_name, c.processor_type) for c in cls.commands}
        cls.unmapped = coverage.map_tests(cls.repo, cls.commands)
        cls.index = {(c.extension, c.surface, c.invocation_name): c for c in cls.commands}

    def test_survey_helper_and_registration_are_not_execution(self):
        for surface, name in (("CLI", "survey"), ("PowerShell", "Open-AzSurvey")):
            candidate = self.index[("Accounts", surface, name)]
            self.assertEqual({"registration", "processor-helper"}, {h.kind for h in candidate.unit_tests})
            self.assertFalse(candidate.has_processor_behavior())
            self.assertEqual("Partial", candidate.status())
            self.assertEqual("no-mapped-signal", candidate.execution_status())
            self.assertIn("no-mapped-processor-tests", candidate.gaps())
            self.assertNotIn("missing-unit", candidate.gaps())
            self.assertTrue(all(h.method and h.reason and h.line for h in candidate.unit_tests))

    def test_profile_and_config_share_behavior_but_not_ps_execution(self):
        for extension, cli_name, ps_name, path, method in (
            ("Accounts", "profile list", "Get-AzProfile_List", "tests/Cli.Tests/ProfileCliTests.cs",
             "ProfileList_ShouldReturnEmpty_WhenNoProfiles"),
            ("Config", "config get", "Get-AzConfig", "tests/Cli.Tests/Configuration/ConfigCliTests.cs",
             ""),
        ):
            with self.subTest(command=cli_name):
                cli, ps = self.index[(extension, "CLI", cli_name)], self.index[(extension, "PowerShell", ps_name)]
                self.assertEqual(cli.processor_type, ps.processor_type)
                self.assertEqual("Good", cli.status())
                self.assertEqual("mapped", cli.execution_status())
                self.assertEqual("inferred-shared", coverage.dimension_label(ps, "processorBehavior"))
                self.assertEqual("Partial", ps.status())
                self.assertEqual("no-mapped-signal", ps.execution_status())
                self.assertFalse(ps.dimension_hits()["binding"])
                self.assertIn("no-mapped-ps-execution-tests", ps.gaps())
                self.assertNotIn("no-mapped-processor-tests", ps.gaps())
                self.assertEqual(1, ps.required_score())
                self.assertTrue(any(h.path == path and (not method or h.method == method)
                                    for _, h in ps.inferred_processor_tests))
                self.assertIn(ps, coverage.priority_gaps([cli, ps]))
                self.assertNotIn(cli, coverage.priority_gaps([cli, ps]))
        self.assertEqual("Get-AzProfile (internal route: Get-AzProfile_List)",
                         self.index[("Accounts", "PowerShell", "Get-AzProfile_List")].display_name())

    def test_profile_crud_and_config_invocations_are_not_unresolved(self):
        for invocation in ("profile list", "profile show", "profile set", "profile remove"):
            self.assertTrue(any(h.path == "tests/Cli.Tests/ProfileCliTests.cs"
                                for h in self.index[("Accounts", "CLI", invocation)].unit_tests))
        for invocation in ("config get", "config set", "config delete", "config list", "config info"):
            self.assertTrue(any(h.path == "tests/Cli.Tests/Configuration/ConfigCliTests.cs"
                                for h in self.index[("Config", "CLI", invocation)].unit_tests))
        self.assertFalse(any("string[] args" in t.evidence for t in self.unmapped))
        self.assertFalse(any(t.evidence.startswith("CLI:profile ") for t in self.unmapped))

    def test_binding_and_runspace_execution_are_different(self):
        token = self.index[("Accounts", "PowerShell", "Get-AzAccessToken")]
        self.assertIn("PSBinding_MapsTenantId_OntoProcessorProperty",
                      [h.method for _, h in token.dimension_hits()["binding"]])
        self.assertEqual("no-mapped-signal", token.execution_status())
        nic = self.index[("Network", "PowerShell", "New-AzNetworkInterface")]
        self.assertIn(("scenario", "PowerShellCmdlets_ConvertTheirParameters_AndRouteParameterSets"),
                      [(tier, h.method) for tier, h in nic.dimension_hits()["surfaceExecution"]])
        self.assertEqual("mapped", nic.execution_status())
        self.assertTrue(nic.dimension_hits()["binding"])

    def test_applicability_requires_closed_source_not_a_manual_extension_exemption(self):
        for candidate in self.commands:
            self.assertIn(candidate.applicability["armScenario"], ("unassessed", "not-applicable", "applicable"))
            if candidate.recording_not_applicable():
                self.assertTrue(candidate.applicability["evidence"])
                self.assertEqual([], candidate.applicability["diagnostics"])
            self.assertNotIn("missing-scenario", candidate.gaps())
            self.assertIsNone(candidate.score_maximum())
            self.assertEqual("yes" if candidate.scenario_tests else "N/A" if candidate.recording_not_applicable() else "Unknown",
                             candidate.scenario_label(candidate.scenario_tests))
            self.assertTrue(all(value in ("unreviewed-no-mapped-signal", "informational")
                                for value in candidate.gap_assessments().values()))
        self.assertTrue(self.index[("Resources", "CLI", "group create")].scenario_hits())
        summary = coverage.command_summary(self.commands)
        self.assertEqual(sum(c.applicability["armScenario"] == "unassessed" for c in self.commands),
                         summary["scenarioUnassessed"])
        self.assertEqual(0, summary["scenarioGaps"])
        self.assertEqual(0, summary["reviewedExecutionGaps"])

    def test_inventory_has_no_dropped_or_duplicate_keys(self):
        self.assertEqual(len(self.commands), len(self.keys))
        report = coverage.as_json(self.commands, "test", {}, self.unmapped)
        self.assertEqual(self.keys, {(e["name"], c["surface"], c["invocationName"], c["processorType"])
                                     for e in report["extensions"] for c in e["commands"]})

    def test_benchmarks_are_informational_and_unresolved_processor_is_unknown(self):
        candidate = command()
        candidate.benchmarks.append(hit())
        self.assertEqual("Gap", candidate.status())
        candidate.unit_tests.append(hit("registration"))
        self.assertEqual("Partial", candidate.status())
        candidate.unit_tests.append(hit())
        self.assertEqual("Good", candidate.status())
        candidate.benchmarks.clear()
        self.assertEqual("Good", candidate.status())
        candidate.processor_type = ""
        self.assertEqual("Unknown", candidate.status())
        self.assertIn(candidate, coverage.priority_gaps([candidate]))

    def test_focused_views_hide_satisfied_commands_but_keep_unreviewed_findings(self):
        gap, partial, good, unknown = (command(name="gap-only"), command(name="partial-only"),
                                       command(name="good-hidden"), command(name="unknown-only", processor=""))
        partial.unit_tests.append(hit("processor-helper"))
        good.extension = "SatisfiedOnly"
        good.unit_tests.append(hit())
        commands = [gap, partial, good, unknown]
        unmapped = [coverage.UnmappedTest("unit", "Hidden", "tests/Hidden.cs", "UNMAPPED_HIDDEN")]
        snapshot = copy.deepcopy(commands)
        for render in (coverage.as_markdown, coverage.as_html):
            text = render(commands, "commit", {}, unmapped)
            self.assertIn("showing 3 of 4 commands", text)
            for name in ("gap-only", "partial-only", "unknown-only", "unreviewed", "Unknown"):
                self.assertIn(name, text)
            self.assertIn("SatisfiedOnly", text)
            self.assertIn("No potential gaps identified in mapped dimensions.", text)
            for name in ("good-hidden", "UNMAPPED_HIDDEN"):
                self.assertNotIn(name, text)
        self.assertEqual(snapshot, commands)
        report = coverage.as_json(commands, "test", {}, unmapped)
        self.assertEqual(4, sum(len(e["commands"]) for e in report["extensions"]))
        self.assertEqual("UNMAPPED_HIDDEN", report["unmappedTests"][0]["evidence"])

    def test_no_gap_view_has_explicit_empty_state_without_hidden_sections(self):
        good = command(name="good-hidden")
        good.extension = "SatisfiedOnly"
        good.unit_tests.append(hit())
        for commands in ([], [good]):
            for render in (coverage.as_markdown, coverage.as_html):
                text = render(commands, "test", {}, [])
                self.assertIn(coverage.PRIORITY_EMPTY_STATE, text)
                self.assertIn(f"showing 0 of {len(commands)} commands", text)
                self.assertEqual(bool(commands), "SatisfiedOnly" in text)
                self.assertNotIn("good-hidden", text)

    def test_complete_summary_counts_and_short_legend_with_gap_only_details(self):
        good, partial, gap, unknown = (command(name="good-hidden"), command(name="partial"),
                                       command(name="gap"), command(name="unknown", processor=""))
        good.unit_tests.append(hit())
        partial.unit_tests.append(hit("processor-helper"))
        satisfied = copy.deepcopy(good)
        satisfied.extension = "SatisfiedOnly"
        satisfied.invocation_name = "satisfied-hidden"
        commands = [good, partial, gap, unknown, satisfied]
        before = copy.deepcopy(commands)
        md = coverage.as_markdown(commands, "test", {}, [])
        html = coverage.as_html(commands, "test", {}, [])
        self.assertIn("| \U0001f534 [Example](#extension-example) | 4 | 1 | 1 | 1 | 1 |", md)
        self.assertIn("| \U0001f7e2 [SatisfiedOnly](#extension-satisfiedonly) | 1 | 1 | 0 | 0 | 0 |", md)
        self.assertIn('<tr><td>\U0001f534 <a href="#extension-example">Example</a></td><td>4</td><td>1</td><td>1</td><td>1</td><td>1</td></tr>', html)
        self.assertIn('<tr><td>\U0001f7e2 <a href="#extension-satisfiedonly">SatisfiedOnly</a></td><td>1</td><td>1</td><td>0</td><td>0</td><td>0</td></tr>', html)
        for text in (md, html):
            self.assertIn("showing 3 of 5 commands", text)
            self.assertIn("About this report", text)
            self.assertIn(coverage.HUMAN_REPORT_GUIDANCE, text)
            self.assertIn(coverage.INDICATOR_LEGEND, text)
            for area, meaning in coverage.REPORT_LEGEND:
                self.assertIn(area, text)
                self.assertIn(meaning if text == md else coverage.html.escape(meaning), text)
            header = text.split("Gap summary")[0]
            for noise in ("Excluded:", "coverageScore", "denominator", "compiler", "Analysis scope:"):
                self.assertNotIn(noise, header)
            for name in ("good-hidden", "satisfied-hidden"):
                self.assertNotIn(name, text)
        self.assertLess(len(coverage.HUMAN_REPORT_GUIDANCE.split()), 40)
        legend = dict(coverage.REPORT_LEGEND)
        self.assertIn("such as building a URL, not the full command", legend["Helper behavior"])
        self.assertIn("expected processor properties", legend["Binding"])
        self.assertIn("not just the shared engine", legend["Surface execution"])
        self.assertEqual(coverage.REPORT_GUIDANCE, coverage.as_json(commands, "test", {}, [])["guidance"])
        self.assertEqual(before, commands)

    def test_summary_links_have_unique_stable_targets_including_fully_good_extensions(self):
        commands = [command(name=f"hidden-{i}") for i in range(5)]
        for candidate, extension in zip(commands, ("A B", "A-B", "a_b", "A B-2", "...")):
            candidate.extension = extension
            candidate.unit_tests.append(hit())
        anchors = coverage.extension_anchors(commands)
        self.assertEqual(anchors, coverage.extension_anchors(list(reversed(commands))))
        self.assertEqual(len(commands), len(set(anchors.values())))
        md = coverage.as_markdown(commands, "test", {}, [])
        html = coverage.as_html(commands, "test", {}, [])
        self.assertEqual(set(anchors.values()), set(re.findall(r"\]\(#([^)]+)\)", md)))
        self.assertEqual(set(anchors.values()), set(re.findall(r'href="#([^"]+)"', html)))
        for anchor in anchors.values():
            self.assertEqual(1, md.count(f'id="{anchor}"'))
            self.assertEqual(1, html.count(f'id="{anchor}"'))
        for candidate in commands:
            self.assertNotIn(candidate.invocation_name, md)
            self.assertNotIn(candidate.invocation_name, html)
        self.assertEqual(len(commands), md.count("No potential gaps identified in mapped dimensions."))

    def test_evidence_is_deduplicated_declared_class_links_only_with_file_fallback(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            source = """
namespace Synthetic {
class ActualClass {
    [Fact] public void First() { Core.Cli.Program.Main(["example"]); Assert.True(true); }
    [Fact] public void Second() { Core.Cli.Program.Main(["example"]); Assert.True(true); }
}
class OtherClass {
    [Fact] public void Third() { Core.Cli.Program.Main(["example"]); Assert.True(true); }
}
}
"""
            path = "tests/Example.Tests/DifferentFile.cs"
            write(repo, path, source)
            candidate = command()
            coverage.map_tests(repo, [candidate])
            self.assertEqual(3, len(candidate.unit_tests))
            candidate.inferred_processor_tests.append(("unit", copy.deepcopy(candidate.unit_tests[0])))
            candidate.benchmarks.append(copy.deepcopy(candidate.unit_tests[0]))
            candidate.scenario_tests.append(coverage.TestHit("NotADeclaredClass", "tests/Fallback.cs", "fixture"))
            before = copy.deepcopy(candidate)
            md = coverage.evidence_description(candidate, "commit", "markdown")
            html = coverage.evidence_description(candidate, "commit", "html")
            for label in ("ActualClass", "OtherClass", "Fallback"):
                self.assertEqual(1, md.count(f"[{label}]("))
                self.assertEqual(1, html.count(f">{label}</a>"))
            self.assertIn("\u2022 [ActualClass]", md)
            self.assertIn("<br>", md)
            self.assertIn("<ul><li>", html)
            for label in ("ActualClass", "OtherClass"):
                line = next(i for i, value in enumerate(source.splitlines(), 1) if value == f"class {label} {{")
                self.assertIn(f"{path}#L{line}", md)
                self.assertIn(f"{path}#L{line}", html)
            for noise in ("processor-behavior", "unit/", "assertion", "Source call-path", "First", "Second", "Third", "NotADeclaredClass"):
                self.assertNotIn(noise, md)
                self.assertNotIn(noise, html)
            machine = coverage.as_json([candidate], "commit", {}, [])
            raw_hit = machine["extensions"][0]["commands"][0]["unitTests"][0]
            self.assertEqual("First", raw_hit["method"])
            self.assertIn("Source call-path", raw_hit["reason"])
            self.assertTrue(raw_hit["callPath"])
            self.assertTrue(raw_hit["sourceUrl"].endswith(f"#L{candidate.unit_tests[0].line}"))
            self.assertNotIn("source_classes", json.dumps(machine))
            self.assertEqual(before, candidate)

    def test_all_output_formats_and_summaries_agree(self):
        class TableRows(HTMLParser):
            def __init__(self):
                super().__init__()
                self.rows, self.row, self.cell = [], [], None

            def handle_starttag(self, tag, attrs):
                if tag == "tr":
                    self.row = []
                elif tag == "td":
                    self.cell = ""
                elif tag == "br" and self.cell is not None:
                    self.cell += "\n"

            def handle_data(self, data):
                if self.cell is not None:
                    self.cell += data

            def handle_endtag(self, tag):
                if tag == "td":
                    self.row.append(self.cell)
                    self.cell = None
                elif tag == "tr" and self.row:
                    self.rows.append(self.row)

        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            completed = subprocess.run(
                [sys.executable, "-B", str(SCRIPT), "--repo-root", str(self.repo),
                 "--out", str(output), "--commit", "automatic-baseline"],
                capture_output=True, text=True,
            )
            self.assertEqual(0, completed.returncode, completed.stderr)
            report = json.loads((output / "extension-test-coverage.json").read_text(encoding="utf-8"))
            data = (output / "data.js").read_text(encoding="utf-8")
            self.assertEqual(report, json.loads(data.removeprefix("window.EXTENSION_TEST_COVERAGE = ").removesuffix(";\n")))
            self.assertFalse(report["testsExecuted"])
            self.assertTrue(report["advisory"])
            self.assertEqual("automatic-discovery-only", report["analysisMode"])
            md = (output / "extension-test-coverage.md").read_text(encoding="utf-8")
            html = (output / "index.html").read_text(encoding="utf-8")
            parser = TableRows()
            parser.feed(html)
            matrix = [row for row in parser.rows if row[0] in ("CLI", "PowerShell")]
            visible = coverage.priority_gaps(self.commands)
            self.assertEqual(sorted(c.display_name() for c in visible), sorted(row[1] for row in matrix))
            for extension in report["extensions"]:
                commands = [c for c in self.commands if c.extension == extension["name"]]
                self.assertEqual(coverage.command_summary(commands), extension["summary"])
                self.assertEqual(len(commands), sum(extension["summary"][s] for s in ("Good", "Partial", "Gap", "Unknown")))
                cells = [
                    f"{coverage.extension_indicator(self.commands, extension['name'])} {extension['name']}",
                    str(len(commands)),
                    *[str(extension["summary"][s]) for s in ("Good", "Partial", "Gap", "Unknown")],
                ]
                self.assertIn(cells, parser.rows)
                cells[0] = f"{coverage.extension_indicator(self.commands, extension['name'])} [{extension['name']}](#{coverage.extension_anchors(self.commands)[extension['name']]})"
                self.assertIn("| " + " | ".join(cells) + " |", md)
                for row in extension["commands"]:
                    candidate = self.index[(extension["name"], row["surface"], row["invocationName"])]
                    self.assertEqual(candidate.status(), row["status"])
                    self.assertEqual(candidate.gaps(), row["gaps"])
                    self.assertEqual(candidate.gap_assessments(), row["gapAssessments"])
                    self.assertEqual(candidate.applicability, row["tierApplicability"])
                    for dimension, hits in candidate.dimension_hits().items():
                        actual = row["evidenceDimensions"][dimension]
                        self.assertEqual(coverage.dimension_label(candidate, dimension), actual["status"])
                        self.assertEqual([(tier, h.path, h.method, h.kind) for tier, h in hits],
                                         [(h["tier"], h["path"], h["method"], h["kind"]) for h in actual["evidence"]])
                    for tier, field in (("unit_tests", "unitTests"), ("scenario_tests", "scenarioTests"),
                                        ("script_scenario_tests", "scriptScenarioTests"), ("benchmarks", "benchmarks")):
                        self.assertEqual([(h.path, h.method, h.kind) for h in getattr(candidate, tier)],
                                         [(h["path"], h["method"], h["kind"]) for h in row[field]])
                    for text, key in ((md, f"`{candidate.display_name()}`"),
                                      (html, f"<code>{coverage.html.escape(candidate.display_name())}</code>")):
                        if candidate in visible:
                            self.assertIn(key, text)
                        else:
                            self.assertNotIn(key, text)
                    if candidate in visible:
                        self.assertIn([
                            candidate.surface, candidate.display_name(),
                            *[coverage.dimension_label(candidate, name) for name in (
                                "registration", "processorBehavior", "processorHelper", "binding", "surfaceExecution")],
                            row["scenarioLabel"], row["scriptScenarioLabel"],
                            coverage.yes_no(bool(candidate.benchmarks)), row["status"],
                            coverage.evidence_description(candidate),
                            coverage.html.unescape(re.sub(r"<[^>]*>", "", coverage.test_evidence_summary(candidate, "html"))),
                        ], matrix)
                        self.assertIn(coverage.test_evidence_summary(candidate, "markdown"), md)
                        self.assertIn(coverage.test_evidence_summary(candidate, "html"), html)
            for text in (md, html):
                for marker in ("Source discovery, not test results", "About this report", "Full evidence",
                               "Registration", "Processor behavior", "Surface execution"):
                    self.assertIn(marker, text)
                self.assertNotIn("Unmapped test evidence", text)


class TestEvidenceSummaryTests(unittest.TestCase):
    def test_three_equal_unit_areas_have_found_labels_and_unchanged_half_threshold(self):
        for kinds, areas, status in (
            ([], (0, 0, 0), "Not mapped"), (["processor-behavior"], (1, 0, 0), "Partial"),
            (["processor-behavior", "binding"], (1, 1, 0), "OK"), (["cli-execution"], (1, 1, 1), "OK"),
        ):
            with self.subTest(kinds=kinds):
                candidate = command()
                candidate.unit_tests.extend(hit(kind) for kind in kinds)
                expected = sum(areas)
                self.assertEqual(expected, sum(coverage.unit_evidence_checklist(candidate).values()))
                self.assertEqual(status, coverage.unit_test_evidence(candidate)["status"])
                labels = (
                    f"Unit test evidence:\n    - Processor: {'Found' if areas[0] else 'Not found'}\n"
                    f"    - Param Binding: {'Found' if areas[1] else 'Not found'}\n"
                    f"    - Command execution: {'Found' if areas[2] else 'Not found'}"
                )
                self.assertIn(labels, coverage.test_evidence_summary(candidate))
                for render, format in ((coverage.as_markdown, "markdown"), (coverage.as_html, "html")):
                    text = render([candidate], "test", {}, [])
                    self.assertIn("Found / Not found for mapped unit-tier signals", text)
                    self.assertNotIn("Behavior checklist", text)
                    self.assertNotRegex(text, r"(?:Processor|Binding|Command execution) [01]/1")
                    if candidate.status() != "Good":
                        self.assertEqual(1 if kinds else 2,
                                         text.count(coverage.test_evidence_summary(candidate, format)))
                self.assertIn("Unit tests: Found" if kinds else "Unit tests: Not found",
                              coverage.test_evidence_summary(candidate))
                if expected == 2:
                    self.assertEqual("Partial", candidate.status())

    def test_registration_helpers_setup_and_references_do_not_score_unit_areas(self):
        candidate = command()
        candidate.unit_tests.extend(hit(kind) for kind in (
            "registration", "processor-helper", "processor-setup", "surface-reference", "script-candidate",
        ))
        self.assertEqual({"processor": False, "binding": False, "execution": False},
                         coverage.unit_evidence_checklist(candidate))
        self.assertIn("Unit tests: Found", coverage.test_evidence_summary(candidate))
        self.assertIn("Unit test evidence:\n    - Processor: Not found\n    - Param Binding: Not found\n    - Command execution: Not found",
                      coverage.test_evidence_summary(candidate))
        self.assertEqual("Partial", candidate.status())

    def test_scenario_and_benchmark_evidence_never_scores_unit_checklist(self):
        candidate = command()
        candidate.scenario_tests.append(hit())
        candidate.benchmarks.append(hit())
        self.assertIn("Unit tests: Not found\n- Unit test evidence:\n    - Processor: Not found\n    - Param Binding: Not found\n    - Command execution: Not found",
                      coverage.test_evidence_summary(candidate))
        self.assertIn("Benchmarks: Found", coverage.test_evidence_summary(candidate))
        for format in ("text", "markdown", "html"):
            self.assertNotIn("Benchmarks: Found (1/1)", coverage.test_evidence_summary(candidate, format))
        self.assertEqual("Good", candidate.status())
        candidate.benchmarks.clear()
        self.assertEqual("Good", candidate.status())
        self.assertIn("Benchmarks: Not found", coverage.test_evidence_summary(candidate))
        for format in ("text", "markdown", "html"):
            self.assertNotIn("Benchmarks: Not found (0/1)", coverage.test_evidence_summary(candidate, format))

    def test_engine_and_cross_surface_shared_logic_do_not_grant_binding_or_execution(self):
        cli, ps = command(), command("PowerShell", "Get-Example")
        cli.unit_tests.append(hit())
        coverage.infer_shared_processor_behavior([cli, ps])
        self.assertEqual({"processor": True, "binding": False, "execution": False},
                         coverage.unit_evidence_checklist(ps))
        direct = command()
        direct.unit_tests.append(hit("engine-execution"))
        self.assertEqual(coverage.unit_evidence_checklist(ps), coverage.unit_evidence_checklist(direct))
        opposite = command()
        opposite.unit_tests.append(hit("ps-execution"))
        self.assertEqual(coverage.unit_evidence_checklist(direct), coverage.unit_evidence_checklist(opposite))
        ps.unit_tests.append(hit("ps-execution"))
        self.assertIn("Unit test evidence:\n    - Processor: Found\n    - Param Binding: Found\n    - Command execution: Found",
                      coverage.test_evidence_summary(ps))
        cli.unit_tests.clear()
        cli.scenario_tests.append(hit())
        ps.unit_tests.clear()
        coverage.infer_shared_processor_behavior([cli, ps])
        self.assertIn("Unit tests: Not found\n- Unit test evidence:\n    - Processor: Not found\n    - Param Binding: Not found\n    - Command execution: Not found",
                      coverage.test_evidence_summary(ps))

    def test_scenarios_have_no_fake_denominator_or_automatic_na(self):
        candidate = command()
        self.assertIn("ARM scenarios: Unknown", coverage.test_evidence_summary(candidate))
        candidate.scenario_tests.extend([hit(), hit("binding")])
        script = hit("script-candidate")
        script.path = "tests/Scenario.ps1"
        candidate.script_scenario_tests.append(script)
        summary = coverage.test_evidence_summary(candidate)
        self.assertIn("Scenario tests: Found (2 files)", summary)
        self.assertNotIn("N/A", summary)
        self.assertNotIn("Scenarios: OK", summary)

    def test_human_summaries_are_consistent_without_technical_gap_codes(self):
        candidate = command(name="example <value>")
        candidate.unit_tests.extend([hit("processor-behavior"), hit("binding")])
        for render, format in ((coverage.as_markdown, "markdown"), (coverage.as_html, "html")):
            text = render([candidate], "test", {}, [])
            self.assertEqual(1, text.count(coverage.test_evidence_summary(candidate, format)))
            self.assertIn("Test evidence", text)
            for code in candidate.gaps():
                self.assertNotIn(code, text)
            self.assertNotIn("unreviewed-no-mapped-signal", text)
        self.assertIn("example &lt;value&gt;", coverage.as_html([candidate], "test", {}, []))

    def test_bulleted_summaries_nest_only_the_three_unit_evidence_areas(self):
        candidate = command()
        candidate.unit_tests.append(hit("registration"))
        candidate.applicability.update(armScenario="not-applicable", nonArmScenario="not-applicable")
        expected = [
            "Unit tests: Found", "Unit test evidence:",
            "Processor: Not found", "Param Binding: Not found", "Command execution: Not found",
            "Benchmarks: Not found", "ARM scenarios: N/A",
        ]
        self.assertEqual("\n".join(
            ("    - " if 2 <= index <= 4 else "- ") + label for index, label in enumerate(expected)
        ), coverage.test_evidence_summary(candidate))
        markdown = coverage.test_evidence_summary(candidate, "markdown")
        self.assertEqual("<br>".join(
            ("&nbsp;&nbsp;&nbsp;&nbsp;" if 2 <= index <= 4 else "") + "\u2022 " + label
            for index, label in enumerate(expected)
        ), markdown)
        self.assertNotIn("\n", markdown)
        self.assertNotIn("|", markdown)
        html = coverage.test_evidence_summary(candidate, "html")
        self.assertEqual(
            '<ul class="test-evidence"><li>Unit tests: Found</li><li>Unit test evidence:'
            '<ul><li>Processor: Not found</li><li>Param Binding: Not found</li><li>Command execution: Not found</li></ul>'
            '</li><li>Benchmarks: Not found</li><li>ARM scenarios: N/A</li></ul>',
            html,
        )
        for render, summary in ((coverage.as_markdown, markdown), (coverage.as_html, html)):
            self.assertEqual(1, render([candidate], "test", {}, []).count(summary))

    def test_documentation_tracking_is_removed_and_schema_version_is_explicit(self):
        candidate = command()
        report = coverage.as_json([candidate], "test", {}, [])
        self.assertEqual(2, report["schemaVersion"])
        for field in ("docsPresent", "docsPath", "docsTests", "docsTestsAnalyzed"):
            self.assertNotIn(field, json.dumps(report))
        self.assertFalse(hasattr(candidate, "docs_present"))
        self.assertFalse(hasattr(coverage, "docs_file_for_command"))
        for render in (coverage.as_markdown, coverage.as_html):
            text = render([candidate], "test", {}, [])
            self.assertNotIn("Docs present", text)
            self.assertNotIn("Docs test", text)
            self.assertNotIn("docs/help", text)


class ReportFocusTests(unittest.TestCase):
    @staticmethod
    def priority_section(text):
        return re.split(r"(?:\n## |<h2)", text.split("Highest priority mapped-signal gaps", 1)[1], maxsplit=1)[0]

    @staticmethod
    def benchmark_section(text):
        return re.split(r"(?:\n## |<h2)", text.split("Benchmark gap summary", 1)[1], maxsplit=1)[0]

    def test_highest_priority_requires_both_absent_unit_presence_and_unknown_arm(self):
        eligible = command(name="eligible")
        unknown_processor = command(name="unresolved", processor="")
        candidates = [eligible, unknown_processor]
        excluded = []
        for kind in ("registration", "processor-helper", "processor-setup", "binding", "processor-behavior"):
            candidate = command(name=kind)
            candidate.unit_tests.append(hit(kind))
            excluded.append(candidate)
        shared = command(name="shared-unit")
        shared.inferred_processor_tests.append(("unit", hit("processor-behavior")))
        excluded.append(shared)
        for state in ("not-applicable", "applicable"):
            candidate = command(name=state)
            candidate.applicability["armScenario"] = state
            excluded.append(candidate)
        scenario = command(name="scenario-not-proof-of-arm")
        scenario.scenario_tests.append(hit("processor-behavior"))
        candidates.append(scenario)
        good = command(name="good-scenario")
        good.scenario_tests.append(hit())
        excluded.append(good)
        commands = candidates + excluded
        before = copy.deepcopy(commands)
        self.assertEqual({c.invocation_name for c in candidates},
                         {c.invocation_name for c in coverage.highest_priority_gaps(commands)})
        for render, format in ((coverage.as_markdown, "markdown"), (coverage.as_html, "html")):
            text = render(commands, "test", {}, [])
            section = self.priority_section(text)
            for candidate in candidates:
                self.assertIn(candidate.invocation_name, section)
            for candidate in excluded:
                self.assertNotIn(f"`{candidate.invocation_name}`" if format == "markdown" else
                                 f"<code>{candidate.invocation_name}</code>", section)
            self.assertEqual(2, render([eligible], "test", {}, []).count(
                coverage.test_evidence_summary(eligible, format)))
            self.assertIn("Scenario tests: Found (1 files)", section)
        self.assertEqual(before, commands)

    def test_priority_filter_precedes_limit_and_preserves_other_detail_rows(self):
        excluded = [command(name=f"a-with-unit-{i:02}") for i in range(60)]
        for candidate in excluded:
            candidate.unit_tests.append(hit("registration"))
        included = [command(name=f"z-eligible-{i:02}") for i in range(60)]
        commands = excluded + included
        for render in (coverage.as_markdown, coverage.as_html):
            text = render(commands, "test", {}, [])
            section = self.priority_section(text)
            self.assertNotIn("a-with-unit-", section)
            self.assertEqual(50, section.count("z-eligible-"))
            self.assertIn("z-eligible-49", section)
            self.assertNotIn("z-eligible-50", section)
            self.assertIn("a-with-unit-00", text)
            self.assertIn("z-eligible-59", text)
            self.assertIn("showing 120 of 120 commands", text)

    def test_empty_priority_filter_does_not_claim_no_other_gaps(self):
        candidate = command(name="retained-detail")
        candidate.unit_tests.append(hit("processor-helper"))
        for render in (coverage.as_markdown, coverage.as_html):
            text = render([candidate], "test", {}, [])
            section = self.priority_section(text)
            self.assertIn(coverage.PRIORITY_EMPTY_STATE, section)
            self.assertNotIn("retained-detail", section)
            self.assertIn("retained-detail", text)
            self.assertIn("showing 1 of 1 commands", text)

    def test_benchmark_summary_counts_full_inventory_without_changing_status_or_machine_evidence(self):
        cli = command(name="missing-cli")
        ps = command("PowerShell", "missing-ps")
        good = command(name="good-hidden")
        good.extension = "GoodOnly"
        good.unit_tests.append(hit())
        found = command(name="has-benchmark")
        found.benchmarks.extend([hit("benchmark"), hit("benchmark")])
        covered = copy.deepcopy(found)
        covered.extension = "Covered"
        commands = [cli, ps, good, found, covered]
        before = coverage.as_json(commands, "test", {}, [])
        self.assertEqual([("Example", 3, 1, 2), ("GoodOnly", 1, 0, 1)],
                         coverage.benchmark_gap_summary(commands))
        extension_only = {"Example": {"CLI": [hit("benchmark")]}}
        for render in (coverage.as_markdown, coverage.as_html):
            text = render(commands, "test", extension_only, [])
            section = self.benchmark_section(text)
            self.assertLess(text.index("Gap summary"), text.index("Benchmark gap summary"))
            self.assertLess(text.index("Benchmark gap summary"), text.index("Highest priority"))
            self.assertIn("GoodOnly", section)
            self.assertNotIn("Covered", section)
            self.assertNotIn("good-hidden", text)
        md = coverage.as_markdown(commands, "test", extension_only, [])
        self.assertIn("| [Example](#extension-example) | 3 | 1 | 2 |", md)
        self.assertIn("| [GoodOnly](#extension-goodonly) | 1 | 0 | 1 |", md)
        html = coverage.as_html(commands, "test", extension_only, [])
        self.assertIn('<td>3</td><td>1</td><td>2</td>', self.benchmark_section(html))
        after = coverage.as_json(commands, "test", {}, [])
        before.pop("generatedAtUtc")
        after.pop("generatedAtUtc")
        self.assertEqual(before, after)
        self.assertEqual("Good", good.status())

    def test_benchmark_summary_has_an_empty_state_and_escapes_extension_names(self):
        covered = command()
        covered.benchmarks.append(hit("benchmark"))
        for commands in ([], [covered]):
            self.assertEqual([], coverage.benchmark_gap_summary(commands))
            for render in (coverage.as_markdown, coverage.as_html):
                section = self.benchmark_section(render(commands, "test", {}, []))
                self.assertIn("No unmapped benchmark gaps identified.", section)
        missing = command()
        missing.extension = "Example | <special>"
        md = self.benchmark_section(coverage.as_markdown([missing], "test", {}, []))
        html = self.benchmark_section(coverage.as_html([missing], "test", {}, []))
        self.assertIn(r"Example \| <special>", md)
        self.assertIn("Example | &lt;special&gt;", html)


class ArmScenarioApplicabilityTests(unittest.TestCase):
    def infer(self, body, extra="", imports=""):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/Example/Processors.cs", """
                using System; using System.Threading.Tasks;
            """ + imports + """
                namespace Example {
                    class ArbitrarilyNamedProcessor {
            """ + body + "\n} " + extra + "\n}")
            index = Analysis(repo, [])
            local = LocalScenarioAnalysis(index).infer("Example.ArbitrarilyNamedProcessor")
            return ArmScenarioAnalysis(index).infer("Example.ArbitrarilyNamedProcessor", local), local

    def test_non_arm_protocols_are_independent_of_general_request_applicability(self):
        for body in (
            'public Task ProcessAsync(){NuGet.Versioning.NuGetVersion.Parse("1.0").ToNormalizedString();'
            'return Task.CompletedTask;}',
            'Microsoft.Identity.Client.IPublicClientApplication auth;'
            'public async Task ProcessAsync(){await auth.AcquireTokenSilent(["scope"], null).ExecuteAsync();}',
        ):
            with self.subTest(body=body):
                arm, local = self.infer(body)
                self.assertEqual("not-applicable", arm["status"], arm["diagnostics"])
                self.assertEqual("unassessed", local["nonArmScenario"])
                self.assertTrue(arm["evidence"])

    def test_framework_configuration_registry_and_discovery_operation_contracts(self):
        cases = (
            ("Core.Abstractions.IConfigurationService", 'service.GetConfiguration("key")'),
            ("Core.Extensions.IExtensionRegistryStore", "service.Load()"),
            ("Core.Extensions.IExtensionRegistryStore", "service.Save(null)"),
            ("Core.Abstractions.IEngine", "service.OpenDiscoverySession(null).RunAsync()"),
        )
        for service, call in cases:
            with self.subTest(service=service, call=call):
                with tempfile.TemporaryDirectory() as directory:
                    repo = Path(directory)
                    write(repo, "src/Core/Contracts.cs", """
                        namespace Core.Abstractions {
                            interface IConfigurationService {object GetConfiguration(string key);}
                            interface IEngine {Session OpenDiscoverySession(object args);}
                            class Session {public System.Threading.Tasks.Task RunAsync()=>null;}
                        }
                        namespace Core.Extensions {
                            interface IExtensionRegistryStore {object Load();void Save(object value);}
                        }
                    """)
                    write(repo, "src/Example/P.cs",
                          "using System.Threading.Tasks; namespace Example; class P {"
                          f"{service} service; public Task ProcessAsync(){{{call};return Task.CompletedTask;}}}}")
                    arm = ArmScenarioAnalysis(Analysis(repo, [])).infer("Example.P")
                self.assertEqual("not-applicable", arm["status"], arm["diagnostics"])

    def test_arm_sdk_requests_and_mixed_authentication_do_not_earn_na(self):
        for body in (
            "Azure.ResourceManager.Resources.ResourceGroupResource resource;"
            "public Task ProcessAsync()=>resource.GetAsync();",
            "System.Net.Http.HttpClient http;"
            'public Task ProcessAsync()=>http.GetAsync("https://management.azure.com/subscriptions/x");',
            "Microsoft.Identity.Client.IPublicClientApplication auth;"
            "Azure.ResourceManager.Resources.ResourceGroupResource resource;"
            "public async Task ProcessAsync(){await auth.AcquireTokenSilent([],null).ExecuteAsync();"
            "await resource.GetAsync();}",
            'public Task ProcessAsync(){var request=new System.Net.Http.HttpRequestMessage('
            'System.Net.Http.HttpMethod.Get,$"https://management.azure.com/subscriptions/{id}");'
            'return Task.CompletedTask;}',
        ):
            with self.subTest(body=body):
                arm, _ = self.infer(body)
                self.assertEqual("applicable", arm["status"], arm["diagnostics"])
                self.assertTrue(arm["armOperations"])

    def test_arm_endpoint_strings_alone_are_not_request_evidence(self):
        arm, _ = self.infer(
            'public Task ProcessAsync(){string url="https://management.azure.com/path";'
            'bool same=string.Equals(url,url);return Task.CompletedTask;}')
        self.assertEqual("not-applicable", arm["status"], arm["diagnostics"])
        self.assertEqual([], arm["armOperations"])

    def test_imported_framework_namespaces_do_not_turn_unknown_calls_into_protocol_apis(self):
        for imports in ("using System.IO;", "using NuGet.Common;", "using Microsoft.Identity.Client;"):
            with self.subTest(imports=imports):
                arm, _ = self.infer("public Task ProcessAsync(){Remote.Send();return Task.CompletedTask;}",
                                    imports=imports)
                self.assertEqual("unassessed", arm["status"], arm["diagnostics"])

    def test_members_resolve_in_their_declaration_namespace_not_the_callers_imports(self):
        arm, _ = self.infer(
            'Foreign.Holder state;public Task ProcessAsync(){state.Target.CreateDirectory("x");'
            'return Task.CompletedTask;}',
            "namespace Foreign {"
            "class Holder{public Directory Target;}"
            "class Directory{public void CreateDirectory(string path){Remote.Send();}}}",
            imports="using System.IO;")
        self.assertEqual("unassessed", arm["status"], arm["diagnostics"])

    def test_output_delegate_property_is_an_infrastructure_boundary_not_an_opaque_service(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/Core/Context.cs", """
                namespace Core.Abstractions.Runtime {
                    public delegate System.Threading.Tasks.Task OutputStreamDelegate(object value);
                    public record ProcessorContext(Handler.IOutputHandler OutputHandler);
                }
                namespace Core.Abstractions.Runtime.Handler {
                    public interface IOutputHandler {OutputStreamDelegate WriteOutput{get;}}
                }
            """)
            write(repo, "src/Example/P.cs", """
                using Core.Abstractions.Runtime;
                using System.Threading.Tasks;
                namespace Example;
                class P {
                    OutputStreamDelegate output;
                    public void Init(ProcessorContext context){output=context.OutputHandler.WriteOutput;}
                    public Task ProcessAsync()=>output(null);
                }
            """)
            result = ArmScenarioAnalysis(Analysis(repo, [])).infer("Example.P")
            self.assertEqual("not-applicable", result["status"], result["diagnostics"])

    def test_opaque_services_http_callbacks_operators_and_control_flow_remain_unknown(self):
        for body, extra in (
            ("IRemote service;public Task ProcessAsync()=>service.SendAsync();",
             "interface IRemote{Task SendAsync();}"),
            ("IRemote service;public Task ProcessAsync(){var x=service.Value;return Task.CompletedTask;}",
             "interface IRemote{string Value{get;}}"),
            ("System.Net.Http.HttpClient http;"
             'public Task ProcessAsync()=>http.GetAsync("https://example.test");', ""),
            ("Action callback;public Task ProcessAsync(){callback();return Task.CompletedTask;}", ""),
            ("public async Task ProcessAsync(){await new Awaiter();}",
             "class Awaiter{public Awaiter GetAwaiter()=>this;public void GetResult(){Remote.Send();}}"),
            ('Converted value="x";public Task ProcessAsync()=>Task.CompletedTask;',
             "class Converted{public static implicit operator Converted(string s)=>Remote.Convert(s);}"),
            ("public Task ProcessAsync(){while(flag){Remote.Send();}return Task.CompletedTask;}", ""),
            ("public Task ProcessAsync(){using var scope=new Scope();return Task.CompletedTask;}",
             "class Scope{public void Dispose(){Remote.Send();}}"),
            ("public Task ProcessAsync(){System.Array.Sort([],new Comparer());return Task.CompletedTask;}",
             "class Comparer{public int Compare(object a,object b){Remote.Send();return 0;}}"),
            ("public Task ProcessAsync(){System.Reflection.Assembly.LoadFrom(path);return Task.CompletedTask;}", ""),
            ("Type target;public Task ProcessAsync(){target.InvokeMember(name,flags,null,null,[]);"
             "return Task.CompletedTask;}", ""),
            ("string Value=>Remote.Get();public Task ProcessAsync(){var x=Value;return Task.CompletedTask;}", ""),
            ("string Value{get;set{Remote.Send();}}"
             'public Task ProcessAsync(){Value="x";return Task.CompletedTask;}', ""),
        ):
            with self.subTest(body=body):
                arm, _ = self.infer(body, extra)
                self.assertEqual("unassessed", arm["status"], arm["diagnostics"])
                self.assertTrue(arm["diagnostics"])

    def test_virtual_lifecycle_override_is_not_replaced_by_a_pure_base_method(self):
        arm, _ = self.infer(
            "public void Init(){Parent.DoWork();}public Task ProcessAsync()=>Task.CompletedTask;",
            "class Parent{public static void DoWork(){new Child().Hook();}}"
            "class Base{public virtual void Hook(){}}"
            "class Child:Base{public override void Hook(){Remote.Send();}}")
        self.assertEqual("unassessed", arm["status"], arm["diagnostics"])

    def test_source_overloads_and_metadata_syntax_are_closed_conservatively(self):
        arm, _ = self.infer(
            'public Task ProcessAsync(){var values=new System.Collections.Generic.Dictionary<string,string>();'
            'values.TryGetValue("x",out var value);'
            'foreach(var item in values){Helper(value);}'
            'return Task.CompletedTask;}'
            'static string Helper(string value)=>value.Trim().ToLowerInvariant();'
            'static string Helper(int value)=>value.ToString();')
        self.assertEqual("not-applicable", arm["status"], arm["diagnostics"])
        arm, _ = self.infer(
            "public Task ProcessAsync(){Helper(null);return Task.CompletedTask;}"
            "static void Helper(string x){}static void Helper(int x){Remote.Send();}")
        self.assertEqual("unassessed", arm["status"], arm["diagnostics"])

    def test_na_keeps_non_arm_scenario_evidence_and_scoring_in_every_output(self):
        candidate = command()
        candidate.applicability = {
            "armScenario": "not-applicable", "nonArmScenario": "unassessed",
            "scope": "arm-record-playback", "reason": "Known non-ARM protocol",
            "evidence": [], "diagnostics": [],
        }
        candidate.scenario_tests.append(hit("processor-behavior"))
        before = (candidate.score(), candidate.status(), coverage.unit_test_evidence(candidate))
        self.assertTrue(candidate.recording_not_applicable())
        self.assertEqual(0, coverage.command_summary([candidate])["scenarioUnassessed"])
        for render in (coverage.as_markdown, coverage.as_html):
            output = render([candidate], "commit", {}, [])
            self.assertIn("ARM scenarios: N/A", output)
            self.assertIn("Scenario tests: Found (1 files)", output)
            self.assertNotIn("ARM scenarios: Found", output)
            self.assertNotIn("N/A=", output)
        row = coverage.as_json([candidate], "commit", {}, [])["extensions"][0]["commands"][0]
        self.assertEqual(1, len(row["scenarioTests"]))
        self.assertEqual(candidate.applicability, row["tierApplicability"])
        self.assertEqual(before, (candidate.score(), candidate.status(), coverage.unit_test_evidence(candidate)))
        candidate.applicability["armScenario"] = "unassessed"
        self.assertIn("ARM scenarios: Unknown", coverage.test_evidence_summary(candidate))
        self.assertEqual(1, coverage.command_summary([candidate])["scenarioUnassessed"])

    def test_generator_wires_separate_assessments_without_command_name_policy(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/Example/P.cs", """
                using System.Threading.Tasks;
                namespace Example;
                class P {
                    Microsoft.Identity.Client.IPublicClientApplication auth;
                    public async Task ProcessAsync(){
                        await auth.AcquireTokenSilent([],null).ExecuteAsync();
                    }
                }
            """)
            candidate = command(name="arbitrary workload", processor="Example.P")
            coverage.map_tests(repo, [candidate])
            self.assertEqual("not-applicable", candidate.applicability["armScenario"])
            self.assertEqual("unassessed", candidate.applicability["nonArmScenario"])
            self.assertEqual("arm-record-playback", candidate.applicability["scope"])
            self.assertEqual("not-applicable", candidate.applicability["armAssessment"]["status"])
            self.assertEqual("unassessed", candidate.applicability["nonArmAssessment"]["status"])
            self.assertEqual(candidate.applicability["reason"], candidate.applicability["armAssessment"]["reason"])
            self.assertIn("ARM scenarios: N/A", coverage.as_markdown([candidate], "commit", {}, []))
            self.assertEqual(0, coverage.command_summary([candidate])["scenarioUnassessed"])


class ScenarioApplicabilityTests(unittest.TestCase):
    def test_external_unimported_types_are_not_guessed_as_bcl_browser_operations(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/App/External.cs", """
                using Vendor;
                namespace Example;
                class External {
                    static System.Diagnostics.ProcessStartInfo Info() =>
                        new("https://example.test"){UseShellExecute=true};
                    public System.Threading.Tasks.Task ProcessAsync(){
                        Process.Start(Info());
                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                }
            """)
            result = LocalScenarioAnalysis(Analysis(repo, [])).infer("Example.External")
            self.assertEqual("unassessed", result["armScenario"])
            self.assertTrue(result["diagnostics"])

    def test_browser_version_helper_record_context_and_inherited_local_init_are_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/App/Context.cs", """
                namespace Example.Child;
                public record Context(string Name);
                public class Parent {
                    public void Init(Context context){BeforeInit();var name=context.Name;}
                    protected virtual void BeforeInit(){}
                }
            """)
            write(repo, "src/App/Links.cs", """
                using System; using System.Reflection;
                namespace Example;
                static class Links {
                    public static readonly string Url=$"https://example.test/{Uri.EscapeDataString(Version())}";
                    static string Version(){
                        Assembly a=typeof(Links).Assembly;
                        string v=a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            ?? a.GetName().Version?.ToString() ?? "0";
                        int at=v.IndexOf('+');
                        return at>=0 ? v[..at] : v;
                    }
                }
            """)
            write(repo, "src/App/Browser.cs", """
                using System; using System.Diagnostics; using System.Threading.Tasks;
                namespace Example.Child;
                class Browser:Parent {
                    static ProcessStartInfo Info()=>new(Links.Url){UseShellExecute=true};
                    public Task ProcessAsync(){
                        using Process p=Process.Start(Info()) ?? throw new InvalidOperationException();
                        return Task.CompletedTask;
                    }
                }
            """)
            result = LocalScenarioAnalysis(Analysis(repo, [])).infer("Example.Child.Browser")
            self.assertEqual([], result["diagnostics"])
            self.assertEqual("not-applicable", result["armScenario"])
            self.assertEqual({"Init", "BeforeInit", "ProcessAsync", "Info", "Version"},
                             {r["method"] for r in result["evidence"]})

    def infer(self, body, extra="", owner="Example.SharedProcessor"):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "src/Example/Processors.cs", """
                using System; using System.IO; using System.Diagnostics;
                using System.Threading.Tasks;
                namespace Example;
                public class SharedProcessor {
            """ + body + "\n}\n" + extra)
            return LocalScenarioAnalysis(Analysis(repo, [])).infer(owner)

    def test_closed_local_file_paths_need_no_request_recording_without_using_command_names(self):
        for operation in ('File.WriteAllText("settings", "value");', 'File.Delete("profile");'):
            result = self.infer(f"public Task ProcessAsync() {{ {operation} return Task.CompletedTask; }}")
            self.assertEqual("not-applicable", result["armScenario"])
            self.assertEqual("not-applicable", result["nonArmScenario"])
            self.assertEqual([], result["diagnostics"])
            self.assertIn("ProcessAsync", [row["method"] for row in result["evidence"]])

    def test_injected_remote_or_unresolved_operations_never_become_na(self):
        for body, extra in (
            ("IRemote service; public Task ProcessAsync() => service.SendAsync();",
             "public interface IRemote { Task SendAsync(); }"),
            ("IRemote service; public Task ProcessAsync() { var v=service.Value; return Task.CompletedTask; }",
             "public interface IRemote { string Value {get;} }"),
            ("public Task ProcessAsync() => auth.ResolveSubscriptionsAsync();", ""),
            ("public Task ProcessAsync() => nuget.InstallAsync();", ""),
            ("public Task ProcessAsync() { dynamic d = GetRemote(); d.Run(); return Task.CompletedTask; }", ""),
            ("public SharedProcessor() { Remote.Start(); } public Task ProcessAsync()=>Task.CompletedTask;", ""),
            ("public async Task ProcessAsync(){await new Awaiter();}", "class Awaiter {}"),
            ("Converted value=\"https://example.test\"; public Task ProcessAsync()=>Task.CompletedTask;",
             "class Converted {public static implicit operator Converted(string s)=>Remote.Convert(s);}"),
            ("public Task ProcessAsync()=>Local.Go();",
             "static class Local { static string s=Remote.Get(); public static Task Go()=>Task.CompletedTask; }"),
            ("public Task ProcessAsync()=>Task.CompletedTask;",
             "public class Other:ExternalBase {public Task ProcessAsync()=>Task.CompletedTask;}"),
        ):
            with self.subTest(body=body, extra=extra):
                owner = "Example.Other" if "ExternalBase" in extra else "Example.SharedProcessor"
                result = self.infer(body, extra, owner)
                self.assertEqual("unassessed", result["armScenario"])
                self.assertTrue(result["diagnostics"])

    def test_browser_proof_rejects_mutation_alternate_returns_and_shadowed_types(self):
        for helper, extra, expected in (
            ('static ProcessStartInfo Info()=>new("https://example.test"){UseShellExecute=true};', "", "not-applicable"),
            ('static ProcessStartInfo Info()=>new("https://example.test"){UseShellExecute=true,FileName="cmd"};', "", "unassessed"),
            ('static ProcessStartInfo Info(){if(flag)return new("cmd"){UseShellExecute=true};'
             'return new("https://example.test"){UseShellExecute=true};}', "", "unassessed"),
            ('static ProcessStartInfo Info()=>new(Url()){UseShellExecute=true};'
             'static string Url(){var s="https://example.test";s="cmd";return s;}', "", "unassessed"),
            ('static ProcessStartInfo Info()=>new(Url()){UseShellExecute=true};'
             'static string Url(){var s="https://example.test";Change(ref s);return s;}'
             'static void Change(ref string s){s="cmd";}', "", "unassessed"),
            ('static ProcessStartInfo Info()=>new("https://example.test"){UseShellExecute=true};',
             'class ProcessStartInfo { public ProcessStartInfo(string s){} public bool UseShellExecute {get;set;} }',
             "unassessed"),
        ):
            with self.subTest(helper=helper, extra=extra):
                result = self.infer(
                    "public Task ProcessAsync(){Process.Start(Info());return Task.CompletedTask;}" + helper, extra)
                self.assertEqual(expected, result["armScenario"])

    def test_virtual_lifecycle_overrides_do_not_inherit_a_pure_base_body(self):
        result = self.infer("", """
            class Parent {
                public void Init(){Hook();}
                protected virtual void Hook(){}
                public virtual Task ProcessAsync()=>Task.CompletedTask;
            }
            class Other:Parent {protected override void Hook(){Remote.Send();}}
        """, "Example.Other")
        self.assertEqual("unassessed", result["armScenario"])
        self.assertTrue(any("Virtual method" in d["reason"] for d in result["diagnostics"]))

    def test_scenario_evidence_takes_precedence_over_recording_na_in_all_outputs(self):
        candidate = command()
        candidate.applicability = self.infer("public Task ProcessAsync()=>Task.CompletedTask;")
        self.assertEqual("N/A", candidate.scenario_label([]))
        self.assertIn("ARM scenarios: N/A", coverage.test_evidence_summary(candidate))
        for render in (coverage.as_markdown, coverage.as_html):
            text = render([candidate], "commit", {}, [])
            self.assertIn("ARM scenarios: N/A", text)
            self.assertNotIn("N/A=", text)
            self.assertNotIn("Not assessed", text)
        candidate.scenario_tests.append(hit())
        self.assertEqual("yes", candidate.scenario_label(candidate.scenario_tests))
        self.assertIn("Scenario tests: Found (1 files)", coverage.test_evidence_summary(candidate))
        report = coverage.as_json([candidate], "commit", {}, [])
        row = report["extensions"][0]["commands"][0]
        self.assertEqual("not-applicable", row["tierApplicability"]["armScenario"])
        self.assertEqual(1, len(row["scenarioTests"]))
        self.assertEqual("unassessed", command().applicability["armScenario"])
        self.assertEqual("Unknown", command().scenario_label([]))

    def test_survey_unit_presence_is_distinct_from_zero_behavior_checklist(self):
        candidate = command()
        candidate.unit_tests.extend(hit(kind) for kind in ("registration", "processor-helper"))
        expected = "Unit tests: Found\n- Unit test evidence:\n    - Processor: Not found\n    - Param Binding: Not found\n    - Command execution: Not found"
        self.assertIn(expected, coverage.test_evidence_summary(candidate))
        row = coverage.as_json([candidate], "commit", {}, [])["extensions"][0]["commands"][0]
        self.assertTrue(row["unitEvidence"]["found"])
        self.assertEqual(0, row["unitEvidence"]["mappedAreas"])
        for render, format in ((coverage.as_markdown, "markdown"), (coverage.as_html, "html")):
            text = render([candidate], "commit", {}, [])
            self.assertIn(coverage.test_evidence_summary(candidate, format), text)
            self.assertNotIn("Unit tests: Not found", text)
        candidate.unit_tests = [hit("binding")]
        self.assertIn("Unit test evidence:\n    - Processor: Not found\n    - Param Binding: Found\n    - Command execution: Not found",
                      coverage.test_evidence_summary(candidate))


class PythonSyntaxTests(unittest.TestCase):
    def test_resource_scopes_preserve_cli_and_engine_call_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "tests/Flow.Tests/Scopes.cs", """
                [Fact] public void ResourceScopes() {
                    using(var cli=new Driver()){cli.RunCli(["example"]);}
                    using(var ps=new Driver()){ps.RunPowerShell("Get-Example",new object());}
                    Assert.True(true);
                }
            """)
            cli, ps = command(), command("PowerShell", "Get-Example")
            coverage.map_tests(repo, [cli, ps])
            self.assertEqual({"cli-execution"}, {h.kind for h in cli.unit_tests})
            self.assertEqual({"engine-execution"}, {h.kind for h in ps.unit_tests})
            self.assertFalse(ps.dimension_hits()["surfaceExecution"])

    def test_nested_conditional_and_anonymous_exception_callback_keep_supported_paths(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "tests/Flow.Tests/Conditions.cs", """
                [Fact] public void Nested() {
                    var p=true ? false ? new Other.SharedProcessor() : new SharedProcessor() : new Other.SharedProcessor();
                    p.ProcessAsync();Assert.True(true);
                }
                [Fact] public void Anonymous() {
                    Assert.Throws<Exception>(delegate(){new SharedProcessor().ProcessAsync();});
                }
            """)
            own, other = command(), command(name="other", processor="Other.SharedProcessor")
            coverage.map_tests(repo, [own, other])
            self.assertEqual({"Nested", "Anonymous"}, {h.method for _,h in own.dimension_hits()["processorBehavior"]})
            self.assertFalse(other.has_processor_behavior())

    def test_unsupported_query_is_opaque_and_explicitly_diagnosed(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            write(repo, "tests/Flow.Tests/Unknown.cs", """
                [Fact] public void Query() {
                    var unused=from item in values select new SharedProcessor().ProcessAsync();
                    Assert.True(true);
                }
            """)
            candidate = command()
            coverage.map_tests(repo, [candidate])
            self.assertFalse(candidate.has_processor_behavior())
            self.assertIn("unsupported-source-syntax", candidate.analysis["diagnosticCounts"])
            self.assertTrue(candidate.analysis["unsupportedSyntax"])

    def test_preprocessor_and_raw_strings_do_not_create_test_roots_or_comments(self):
        source = b'''
            #define LOCAL
            class Roots {
            #if LOCAL && !MISSING
                [Fact] void Active(){var s="""// string content""";}
            #else
                [Fact] void Disabled(){new Processor().ProcessAsync();}
            #endif
            }
        '''
        root = syntax.Parser().parse(source).root_node
        methods = [n for n in descendants(root) if n.type == "method_declaration"]
        self.assertEqual(["Active"], [n.child_by_field_name("name").text.decode() for n in methods])
        self.assertFalse(any(n.type == "comment" for n in descendants(root)))

    def test_parser_requires_no_runtime_compiler_or_subprocess(self):
        with mock.patch.object(subprocess, "Popen", side_effect=AssertionError("Unexpected external process")):
            root = syntax.Parser().parse(b"class Local { void Run() {} }").root_node
            self.assertFalse(root.has_error)
            self.assertEqual("python-stdlib", syntax.parser_metadata()["implementation"])
        self.assertTrue(syntax.Parser().parse(b"class Incomplete {").root_node.has_error)

    def test_bom_unicode_and_comments_preserve_evidence_spans(self):
        source = '\ufeffclass Café { string s = "🟢 // not a comment";\n// actual café comment\nvoid Run() {} }'
        root = syntax.Parser().parse(source.encode("utf-8")).root_node
        nodes = list(descendants(root))
        method = next(n for n in nodes if n.type == "method_declaration")
        self.assertEqual("void Run() {}", method.text.decode())
        self.assertEqual(3, method.start_point.row + 1)
        comments = [n.text.decode() for n in nodes if n.type == "comment"]
        self.assertEqual(["// actual café comment"], comments)

    def test_concurrent_and_isolated_process_parses_need_no_compiler_or_temp_outputs(self):
        parser = syntax.Parser()
        with ThreadPoolExecutor(max_workers=4) as pool:
            roots = list(pool.map(lambda n: parser.parse(f"class C{n} {{}}".encode()).root_node.text,
                                  range(8)))
        self.assertEqual([f"class C{n} {{}}".encode() for n in range(8)], roots)
        with tempfile.TemporaryDirectory() as directory:
            code = (
                "import sys,json,os;sys.path.insert(0,sys.argv[1]);os.environ['PATH']='';"
                "import extension_coverage_analysis as s;s.Parser().parse(b'class Cold {}');"
                "print(json.dumps(s.parser_metadata()))"
            )
            def run(_):
                result = subprocess.run(
                    [sys.executable, "-S", "-B", "-c", code, str(SCRIPT.parent)],
                    capture_output=True, text=True, timeout=120)
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual("", result.stderr)
                return json.loads(result.stdout)
            with ThreadPoolExecutor(max_workers=2) as pool:
                results = list(pool.map(run, range(2)))
            self.assertEqual([], list(Path(directory).iterdir()))
            for result in results:
                self.assertEqual("python-stdlib", result["implementation"])

    def test_tool_imports_require_only_stdlib_and_local_sources(self):
        import ast
        local = {"extension_coverage_analysis"}
        for path in (SCRIPT, SCRIPT.with_name("extension_coverage_analysis.py")):
            for node in ast.walk(ast.parse(path.read_text(encoding="utf-8"))):
                imports = [a.name for a in node.names] if isinstance(node, ast.Import) else (
                    [node.module] if isinstance(node, ast.ImportFrom) else [])
                for module in imports:
                    self.assertIn(module.split(".")[0], sys.stdlib_module_names | local)
        self.assertFalse(SCRIPT.with_name("requirements-extension-test-coverage.txt").exists())
        workflow = (SCRIPT.parents[2] / ".github/workflows/extension-test-coverage.yml").read_text()
        self.assertNotIn("pip install", workflow)
        self.assertNotIn("setup-dotnet", workflow)
        self.assertFalse(SCRIPT.with_name("ExtensionCoverageSyntax.cs").exists())
        self.assertFalse(SCRIPT.with_name("extension_coverage_syntax.py").exists())


class LifecycleTests(unittest.TestCase):
    def test_current_repository_inventory_needs_no_fixed_command_baseline(self):
        repo = SCRIPT.parents[2]
        commands = coverage.parse_commands(repo)
        self.assertTrue(commands)
        keys = {(c.extension, c.surface, c.invocation_name, c.processor_type) for c in commands}
        self.assertEqual(len(commands), len(keys))
        unmapped = coverage.map_tests(repo, commands)
        report = coverage.as_json(commands, "current", {}, unmapped)
        self.assertEqual(keys, {(e["name"], c["surface"], c["invocationName"], c["processorType"])
                                for e in report["extensions"] for c in e["commands"]})
        self.assertEqual(len(commands),
                         sum(sum(e["summary"][s] for s in ("Good", "Partial", "Gap", "Unknown"))
                             for e in report["extensions"]))

    def test_added_renamed_and_removed_extensions_need_no_policy(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            source = """
                CommandMetadata cli = new(
                    InvocationName: "probe",
                    FullName: "Example.ProbeProcessor"
                );
                cliCommandTable = BuildCommandTable(cli);
            """
            index = write(repo, "src/Extensions/Example/ExtensionIndex.cs", source)
            write(repo, "tests/Example.Tests/Tests.cs",
                  '[Fact] public void Executes() { host.ExecuteCli(["probe", "value"]); }')
            commands = coverage.parse_commands(repo)
            self.assertEqual(["probe"], [c.invocation_name for c in commands])
            coverage.map_tests(repo, commands)
            self.assertEqual("Good", commands[0].status())
            second = write(repo, "src/Extensions/NewExtension/ExtensionIndex.cs",
                           source.replace("probe", "new-command").replace(
                               "Example.ProbeProcessor", "NewExtension.FreshProcessor"))
            commands = coverage.parse_commands(repo)
            coverage.map_tests(repo, commands)
            self.assertEqual({"probe", "new-command"}, {c.invocation_name for c in commands})
            self.assertEqual("Gap", next(c for c in commands if c.invocation_name == "new-command").status())
            second.write_text(source.replace("probe", "renamed-command"), encoding="utf-8")
            index.unlink()
            commands = coverage.parse_commands(repo)
            self.assertEqual(["renamed-command"], [c.invocation_name for c in commands])
            self.assertTrue(any(t.evidence == "CLI:probe value" for t in coverage.map_tests(repo, commands)))
            output = repo / "output"
            result = subprocess.run(
                [sys.executable, "-S", "-B", str(SCRIPT), "--repo-root", str(repo), "--out", str(output)],
                capture_output=True, text=True,
            )
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertTrue((output / "extension-test-coverage.json").is_file())
            (repo / "tests/Example.Tests/Tests.cs").unlink()
            self.assertEqual([], coverage.map_tests(repo, coverage.parse_commands(repo)))

    def test_empty_inventory_is_an_explicit_error_not_a_publishable_empty_report(self):
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run(
                [sys.executable, "-S", "-B", str(SCRIPT), "--repo-root", directory, "--out", str(Path(directory) / "output")],
                capture_output=True, text=True,
            )
            self.assertEqual(2, result.returncode)
            self.assertIn("No registered commands discovered", result.stderr)
            self.assertFalse((Path(directory) / "output" / "extension-test-coverage.json").exists())

    def test_ci_is_advisory_but_only_publishes_valid_fresh_reports(self):
        workflow = (SCRIPT.parents[2] / ".github/workflows/extension-test-coverage.yml").read_text(encoding="utf-8")
        self.assertNotIn("extension-test-coverage-policy.json", workflow)
        self.assertFalse(SCRIPT.with_name("extension-test-coverage-policy.json").exists())
        self.assertNotIn("apply_review_policy", SCRIPT.read_text(encoding="utf-8"))
        for path in (".github/scripts/test_extension_test_coverage.py", ".github/skills/extension-test-coverage/**"):
            self.assertEqual(2, workflow.count(f"      - '{path}'"))
        self.assertLess(workflow.index("- name: Test report attribution"), workflow.index("- name: Generate report"))
        self.assertIn("python3 -S -B -m unittest discover -s .github/scripts -p test_extension_test_coverage.py", workflow)
        self.assertEqual(2, workflow.splitlines().count("    continue-on-error: true"))
        for step in workflow[workflow.index("    steps:"):].split("      - ")[1:]:
            self.assertIn("continue-on-error: true", step)
        for guard in ("steps.tests.outcome == 'success'", "steps.generate.outcome == 'success'",
                      "steps.upload.outcome == 'success'", "needs.report.outputs.ready == 'true'",
                      "github.event_name == 'push' && github.ref == 'refs/heads/main'",
                      "::warning::", "if-no-files-found: error"):
            self.assertIn(guard, workflow)


if __name__ == "__main__":
    unittest.main()
