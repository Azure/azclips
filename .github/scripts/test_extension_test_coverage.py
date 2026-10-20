"""Regression tests for command-surface test signal attribution."""

import importlib.util
from pathlib import Path
import sys
import tempfile
import unittest


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
                        invocations, unresolved = coverage.invocations_from_csharp(
                            f"{receiver}{helper}({args});"
                        )
                        self.assertEqual({"CLI:profile show prod"}, invocations)
                        self.assertEqual([], unresolved)

    def test_argument_helpers(self):
        for helper in CLI_HELPERS:
            for declaration, argument in (
                ('string[] Args() => ["profile", "show", "prod"];', "Args()"),
                ('var args = new[] { "profile", "show", "prod" };', "args"),
                ('string[] args = ["profile", "show", "prod"];', "args"),
            ):
                with self.subTest(helper=helper, declaration=declaration):
                    invocations, unresolved = coverage.invocations_from_csharp(
                        declaration + f"\nfixture.{helper}({argument});"
                    )
                    self.assertEqual({"CLI:profile show prod"}, invocations)
                    self.assertEqual([], unresolved)

    def test_local_fallback_uses_actual_helper_name(self):
        for helper in CLI_HELPERS:
            with self.subTest(helper=helper):
                invocations, unresolved = coverage.invocations_from_csharp(
                    f'int {helper}() => host.RunCli(["profile", "show", "prod"]);\n'
                    f"{helper}();"
                )
                self.assertEqual({"CLI:profile show prod"}, invocations)
                self.assertEqual([], unresolved)

    def test_unresolved_calls_preserve_helper_names(self):
        for helper in CLI_HELPERS:
            with self.subTest(helper=helper):
                invocations, unresolved = coverage.invocations_from_csharp(
                    f"fixture.{helper}(dynamicArgs);"
                )
                self.assertEqual(set(), invocations)
                self.assertEqual([f"{helper}(dynamicArgs)"], unresolved)

    def test_declaration_detection_does_not_match_quoted_arguments(self):
        for value in ("params", "string[] args", "ScenarioHost host"):
            with self.subTest(value=value):
                self.assertEqual(
                    ({f"CLI:profile show {value.lower()}"}, []),
                    coverage.invocations_from_csharp(
                        f'ExecuteCli(["profile", "show", "{value}"]);'
                    ),
                )

    def test_other_helpers_do_not_resolve_unknown_or_receiver_calls(self):
        invocations, unresolved = coverage.invocations_from_csharp(
            'int RunCli() => host.RunCli(["profile", "list"]);\n'
            "ExecuteCli(dynamicArgs);\nfixture.RunCli(dynamicArgs);"
        )
        self.assertEqual({"CLI:profile list"}, invocations)
        self.assertEqual(
            ["ExecuteCli(dynamicArgs)", "RunCli(dynamicArgs)"], unresolved
        )

    def test_declarations_are_not_calls(self):
        for helper in CLI_HELPERS:
            for parameter in (
                "string[] args",
                "string [] args",
                "params string[] args",
                "string command",
                "ScenarioHost host, string[] args",
                "IReadOnlyList<string> args",
            ):
                with self.subTest(helper=helper, parameter=parameter):
                    self.assertEqual(
                        (set(), []),
                        coverage.invocations_from_csharp(
                            f"public int {helper}({parameter}) {{ return 0; }}"
                        ),
                    )

    def test_powershell_helpers_keep_exact_keys(self):
        invocations, unresolved = coverage.invocations_from_csharp(
            'host.RunPowerShell("Get-AzProfile");\n'
            'host.RunPowerShellCommand("Get-AzProfile prod");\n'
            'pipeline.AddCommand("Get-AzProfile_Detail");'
        )
        self.assertEqual(
            {
                "PowerShell:get-azprofile",
                "PowerShell:get-azprofile prod",
                "PowerShell:get-azprofile_detail",
            },
            invocations,
        )
        self.assertEqual([], unresolved)


class ResolutionTests(unittest.TestCase):
    def test_longest_cli_prefix_at_token_boundary_and_exact_powershell(self):
        commands = [
            coverage.CommandCoverage("Accounts", "CLI", name)
            for name in ("profile", "profile show", "profile show details")
        ] + [coverage.CommandCoverage("Accounts", "PowerShell", "Get-AzProfile")]
        index = {
            coverage.command_key(command.surface, command.invocation_name): command
            for command in commands
        }
        for invocation, expected in (
            ("CLI:profile show", commands[1]),
            ("CLI:profile show prod", commands[1]),
            ("CLI:profile show details prod", commands[2]),
            ("CLI:profile showcase prod", commands[0]),
            ("CLI:profiles show prod", None),
            ("CLI:unknown show", None),
            ("PowerShell:get-azprofile", commands[3]),
            ("PowerShell:get-azprofile prod", None),
            ("PowerShell:get-azprofile_detail", None),
        ):
            with self.subTest(invocation=invocation):
                self.assertIs(expected, coverage.resolve_invocation(invocation, index))


class ReportTests(unittest.TestCase):
    def write(self, repo, relative, text):
        path = repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def test_all_tiers_share_prefix_resolution(self):
        for helper in CLI_HELPERS:
            for script_name, script in (
                ("profile.sh", "az profile show details prod --verbose"),
                ("profile.ps1", "& $az profile show details prod --verbose"),
            ):
                with self.subTest(helper=helper, script=script_name):
                    with tempfile.TemporaryDirectory() as directory:
                        repo = Path(directory)
                        commands = [
                            coverage.CommandCoverage("Accounts", "CLI", name)
                            for name in ("profile", "profile show", "profile show details")
                        ]
                        commands.append(
                            coverage.CommandCoverage("Accounts", "PowerShell", "Get-AzProfile")
                        )
                        text = (
                            f'{helper}(["profile", "show", "details", "prod"]);\n'
                            f"{helper}(dynamicArgs);\n"
                            'AddCommand("Get-AzProfile");\n'
                            'AddCommand("Get-AzProfile extra");'
                        )
                        self.write(repo, "tests/Unit.Tests/ProfileTests.cs", "[Fact]\n" + text)
                        self.write(
                            repo, "tests/Scenario.Tests/ProfileScenario.cs",
                            text + f'\nScriptScenarioRunner.RunPlayback("{script_name}");',
                        )
                        self.write(
                            repo, f"tests/Scenario.Tests/Scripts/{script_name}",
                            script + "\naz unknown show" if script_name.endswith(".sh")
                            else script + "\nGet-AzProfile -Name prod\n& $az unknown show",
                        )
                        self.write(
                            repo, "tests/Benchmark/CLI/ProfileBenchmarks.cs",
                            '[BenchmarkExtension("Accounts")]\n' + text,
                        )
                        self.write(
                            repo, "tests/Benchmark/CLI/OtherBenchmarks.cs",
                            '[BenchmarkExtension("Other")]\n' + text,
                        )
                        for folder, prefix in (
                            ("Unit.Tests", "[Fact]"),
                            ("Scenario.Tests", ""),
                            ("Benchmark/PS", '[BenchmarkExtension("Accounts")]'),
                        ):
                            self.write(
                                repo, f"tests/{folder}/UnmatchedPowerShell.cs",
                                prefix + '\nAddCommand("Get-AzProfile extra");',
                            )

                        unmapped = coverage.map_tests(repo, commands)
                        for command in commands[:2]:
                            self.assertEqual([], command.unit_tests)
                            self.assertEqual([], command.scenario_tests)
                            self.assertEqual([], command.script_scenario_tests)
                            self.assertEqual([], command.benchmarks)
                        mapped = commands[2]
                        for hits in (
                            mapped.unit_tests, mapped.scenario_tests,
                            mapped.script_scenario_tests, mapped.benchmarks,
                        ):
                            self.assertEqual(1, len(hits))
                        self.assertEqual("Good", mapped.status())
                        self.assertEqual(4, mapped.score())
                        self.assertEqual(
                            {"unit", "scenario", "benchmark"},
                            {test.tier for test in unmapped
                             if test.evidence == f"{helper}(dynamicArgs)"},
                        )
                        self.assertEqual(
                            {"unit", "scenario"},
                            {test.tier for test in unmapped
                             if test.evidence == "PowerShell:get-azprofile extra"},
                        )
                        self.assertTrue(any(
                            test.tier == "script-scenario"
                            and test.evidence == "CLI:unknown show" for test in unmapped
                        ))
                        ps_command = commands[3]
                        self.assertEqual(1, len(ps_command.unit_tests))
                        self.assertEqual(1, len(ps_command.scenario_tests))
                        self.assertEqual(1, len(ps_command.benchmarks))
                        self.assertEqual(
                            int(script_name.endswith(".ps1")),
                            len(ps_command.script_scenario_tests),
                        )
                        inventory = coverage.benchmark_inventory(repo)
                        report = coverage.as_json(commands, "test", inventory, unmapped)
                        row = next(
                            command for command in report["extensions"][0]["commands"]
                            if command["invocationName"] == "profile show details"
                        )
                        for tier in ("unitTests", "scenarioTests", "scriptScenarioTests", "benchmarks"):
                            self.assertEqual(1, len(row[tier]))
                        self.assertEqual("Good", row["status"])
                        for render in (coverage.as_markdown, coverage.as_html):
                            self.assertIn(
                                "not proof that no tests exist",
                                render(commands, "test", inventory, unmapped),
                            )

    def test_real_profile_and_config_sources_map_registered_commands(self):
        source_repo = SCRIPT.parents[2]
        commands = coverage.parse_commands(source_repo)
        sources = (
            "tests/Cli.Tests/ProfileCliTests.cs",
            "tests/Cli.Tests/Configuration/ConfigCliTests.cs",
        )
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            for source in sources:
                self.write(repo, source, (source_repo / source).read_text(encoding="utf-8-sig"))
            unmapped = coverage.map_tests(repo, commands)
        index = {
            (command.surface, command.invocation_name): command for command in commands
        }
        for invocation in ("profile list", "profile show", "profile set", "profile remove"):
            with self.subTest(invocation=invocation):
                self.assertIn(sources[0], [hit.path for hit in index[("CLI", invocation)].unit_tests])
        for invocation in ("config get", "config set", "config delete", "config list", "config info"):
            with self.subTest(invocation=invocation):
                self.assertIn(sources[1], [hit.path for hit in index[("CLI", invocation)].unit_tests])
        self.assertFalse(any("string[] args" in test.evidence for test in unmapped))
        self.assertFalse(any(test.evidence.startswith("CLI:profile ") for test in unmapped))


if __name__ == "__main__":
    unittest.main()
