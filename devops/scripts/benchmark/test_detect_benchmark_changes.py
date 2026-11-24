#!/usr/bin/env python3

import http.client
import io
import os
import subprocess
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

import detect_benchmark_changes as detect
from benchmark_fixtures import captured_warnings

BENCHMARK_FILES = {
    "tests/Benchmark/CLI/VirtualMachineBenchmarks.cs":
        '[BenchmarkExtension("Compute")]\npublic class VirtualMachineBenchmarks {}\n',
    "tests/Benchmark/PS/KeyVaultBenchmarks.cs":
        '    [BenchmarkExtension("KeyVault")]\npublic class KeyVaultBenchmarks {}\n',
    "tests/Benchmark/BenchmarkBase.cs": '/// <c>[BenchmarkExtension("Name")]</c> marks a benchmark class.\n',
}
FILES = {
    "tests/Benchmark/CLI/VirtualMachineBenchmarks.cs": "compute",
    "tests/Benchmark/PS/KeyVaultBenchmarks.cs": "keyvault",
}
EVERYTHING = detect.Selection(("compute", "keyvault"), True)


class PathRuleTests(unittest.TestCase):
    def test_shared_paths_run_everything(self):
        for path in ("src/Core/Engine/Runner.cs", "src/Extensions/ExtensionRuntime/Loader.cs",
                     "src/CLI/Program.cs", "src/CLI/Commands/Group.cs", "src/CLI/Help/HelpPrinter.cs",
                     "src/CLI/Help/Rendering/Table.cs", "tests/Benchmark/Program.cs",
                     "tests/Benchmark/Benchmark.csproj", "tests/Benchmark/Telemetry/TraceSampler.cs",
                     "tests/Benchmark/PS/PSBenchmarkBase.cs", "tests/Common/Stubs/StubResponseMappings.cs",
                     "global.json", "azclips.slnx", "Shared.props", "NuGet.config", "Directory.Build.targets",
                     "src/Directory.Build.props", "src/Extensions/Directory.Build.targets",
                     "src/Extensions/Targets/ExtensionSidecar.targets", "tests/Directory.Build.props",
                     "src/PS/Common/CmdletOutputStreamsFactory.cs",
                     ".azure-pipelines/templates/dist/jobs/benchmark-suite.yml",
                     ".azure-pipelines/templates/dist/steps/run-benchmark-leg.yml",
                     "devops/scripts/benchmark/compare_benchmarks.py"):
            with self.subTest(path=path):
                self.assertEqual(detect.select([path], FILES), EVERYTHING)

    def test_help_docs_and_unrelated_paths_run_nothing(self):
        for path in ("src/CLI/Help/Docs/az_vm.md", "tests/Benchmark/README.md", "docs/README.md",
                     "src/Extensions/Storage/Commands.cs", ".github/workflows/build-and-test.yml",
                     "src/PS/Az/Az.psd1", "src/Extensions/Extension/List.cs"):
            with self.subTest(path=path):
                self.assertEqual(detect.select([path], FILES), detect.NOTHING)

    def test_extension_changes_run_only_that_extension(self):
        for path, extension in (("src/Extensions/Compute/VmCommands.cs", "compute"),
                                ("src/PS/KeyVault/Cmdlets.cs", "keyvault"),
                                ("tests/Benchmark/PS/KeyVaultBenchmarks.cs", "keyvault")):
            with self.subTest(path=path):
                self.assertEqual(detect.select([path], FILES), detect.Selection((extension,), False))

    def test_startup_changes_run_only_startup(self):
        self.assertEqual(detect.select(["tests/Benchmark/Startup/CliWarmStartupBenchmarks.cs"], FILES),
                         detect.Selection((), True))

    def test_build_reasons(self):
        cases = [
            (("PullRequest", "refs/pull/7/merge", "main"), "pull-request"),
            (("PullRequest", "refs/pull/7/merge", "refs/heads/main"), "pull-request"),
            (("PullRequest", "refs/pull/7/merge", "release/1.0"), "none"),
            (("BatchedCI", "refs/heads/main", ""), "main"),
            (("IndividualCI", "refs/heads/main", ""), "main"),
            (("IndividualCI", "refs/heads/feature", ""), "none"),
            (("Manual", "refs/heads/main", ""), "main"),
            (("Manual", "refs/heads/feature", ""), "all"),
            (("Schedule", "refs/heads/main", ""), "none"),
        ]
        for arguments, expected in cases:
            with self.subTest(arguments=arguments):
                self.assertEqual(detect.plan_mode(*arguments), expected)

    def test_outputs_keep_a_placeholder_matrix_leg(self):
        self.assertEqual(detect.output_lines(detect.NOTHING, "the pull request target"), [
            "##vso[task.setvariable variable=hasExtensionWork;isOutput=true]false",
            "##vso[task.setvariable variable=runStartup;isOutput=true]false",
            '##vso[task.setvariable variable=extensionMatrix;isOutput=true]{"none":{"Extension":"none"}}',
            "##vso[task.setvariable variable=extensions;isOutput=true]",
            "##vso[task.setvariable variable=changeBase;isOutput=true]the pull request target",
        ])
        self.assertIn('{"compute":{"Extension":"compute"},"keyvault":{"Extension":"keyvault"}}',
                      detect.output_lines(EVERYTHING, "x")[2])


class RepositoryTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.repo = Path(temp.name)
        settings = {
            "GIT_CONFIG_GLOBAL": os.devnull, "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_AUTHOR_NAME": "Test", "GIT_AUTHOR_EMAIL": "test@example.com",
            "GIT_COMMITTER_NAME": "Test", "GIT_COMMITTER_EMAIL": "test@example.com",
        }
        environment = mock.patch.dict(os.environ, settings)
        environment.start()
        self.addCleanup(environment.stop)
        # Hooks and `git rebase --exec` export GIT_DIR, which would point these commands at the caller's repository.
        for name in [name for name in os.environ if name.startswith("GIT_") and name not in settings]:
            del os.environ[name]
        self.git("init", "--quiet", "--initial-branch=main")
        self.commit("base", BENCHMARK_FILES)

    def git(self, *args):
        return subprocess.run(["git", "-C", str(self.repo), *args],
                              check=True, capture_output=True, text=True).stdout.strip()

    def commit(self, message, files):
        for path, content in files.items():
            target = self.repo / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding="utf-8")
        self.git("add", "--all")
        self.git("commit", "--quiet", "-m", message)
        return self.git("rev-parse", "HEAD")

    def test_discovers_extensions_from_the_attribute(self):
        self.assertEqual(detect.discover_extensions(self.repo), FILES)

    def test_rejects_extension_names_that_cannot_be_matrix_legs(self):
        for name in ("Key-Vault", "KeyVault\n"):
            with self.subTest(name=name):
                self.commit("bad", {"tests/Benchmark/CLI/BadBenchmarks.cs": f'[BenchmarkExtension("{name}")]\n'})
                with self.assertRaisesRegex(ValueError, "letters and digits"):
                    detect.discover_extensions(self.repo)

    def test_pull_requests_compare_the_merge_commit_with_its_target(self):
        self.git("checkout", "--quiet", "-b", "feature")
        self.commit("compute change", {"src/Extensions/Compute/VmCommands.cs": "// change\n"})
        self.git("checkout", "--quiet", "main")
        self.commit("main moves on", {"src/PS/KeyVault/Cmdlets.cs": "// main\n"})
        self.git("merge", "--quiet", "--no-ff", "-m", "merge", "feature")
        environ = {"BUILD_REASON": "PullRequest", "SYSTEM_PULLREQUEST_TARGETBRANCH": "main"}

        selection, base = detect.decide(environ, self.repo)

        self.assertEqual(selection, detect.Selection(("compute",), False))
        self.assertEqual(base, "the pull request target")

    def test_a_pull_request_without_a_merge_commit_runs_everything(self):
        self.commit("change", {"src/Extensions/Compute/VmCommands.cs": "// change\n"})
        environ = {"BUILD_REASON": "PullRequest", "SYSTEM_PULLREQUEST_TARGETBRANCH": "main"}

        with captured_warnings() as warnings:
            selection, _ = detect.decide(environ, self.repo)

        self.assertEqual(selection, EVERYTHING)
        self.assertIn("not a merge commit", warnings.getvalue())

    def test_main_compares_with_the_last_successful_run(self):
        base = self.git("rev-parse", "HEAD")
        self.commit("keyvault", {"src/PS/KeyVault/Cmdlets.cs": "// change\n"})
        self.commit("startup", {"tests/Benchmark/Startup/CliWarmStartupBenchmarks.cs": "// change\n"})
        for reason in ("BatchedCI", "Manual"):
            with self.subTest(reason=reason):
                environ = {"BUILD_REASON": reason, "BUILD_SOURCEBRANCH": "refs/heads/main"}
                find_base = mock.Mock(return_value=base)

                selection, description = detect.decide(environ, self.repo, find_base=find_base)

                find_base.assert_called_once_with(environ)
                self.assertEqual(selection, detect.Selection(("keyvault",), True))
                self.assertEqual(description, f"the last successful main run ({base[:7]})")

    def test_main_runs_everything_without_a_usable_base(self):
        self.git("checkout", "--quiet", "--orphan", "unrelated")
        unrelated = self.commit("unrelated history", {"other.txt": "x\n"})
        self.git("checkout", "--quiet", "main")
        environ = {"BUILD_REASON": "IndividualCI", "BUILD_SOURCEBRANCH": "refs/heads/main"}

        for label, find_base in (("no run", lambda _: None), ("not an ancestor", lambda _: unrelated),
                                 ("API failure", mock.Mock(side_effect=OSError("network down"))),
                                 ("truncated response", mock.Mock(side_effect=http.client.IncompleteRead(b""))),
                                 ("response shape", mock.Mock(side_effect=AttributeError("no attribute 'get'")))):
            with self.subTest(label), captured_warnings() as warnings:
                self.assertEqual(detect.decide(environ, self.repo, find_base=find_base)[0], EVERYTHING)
                lines = warnings.getvalue().splitlines()
                self.assertEqual(len(lines), 1)
                self.assertIn("every benchmark runs", lines[0])

    def test_main_prints_the_output_variables(self):
        stdout = io.StringIO()
        # Pinned because CI builds of main set BUILD_SOURCEBRANCH, and a manual run of main queries the build API.
        environ = {"BUILD_REASON": "Manual", "BUILD_SOURCEBRANCH": "refs/heads/feature"}
        with mock.patch.dict(os.environ, environ), \
                mock.patch.object(detect, "REPO_ROOT", self.repo), redirect_stdout(stdout):
            self.assertEqual(detect.main(), 0)

        self.assertIn("##vso[task.setvariable variable=runStartup;isOutput=true]true", stdout.getvalue())
        self.assertIn("Benchmarks: extensions=compute,keyvault, startup=yes", stdout.getvalue())


if __name__ == "__main__":
    unittest.main()
