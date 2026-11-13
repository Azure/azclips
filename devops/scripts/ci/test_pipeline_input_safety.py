#!/usr/bin/env python3

import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import textwrap
import unittest


class PipelineInputSafetyTests(unittest.TestCase):
    payloads = (
        "bad'; printf INJECTION_EXECUTED >&2; #",
        'bad"; printf INJECTION_EXECUTED >&2; #',
        "$(printf INJECTION_EXECUTED >&2)",
        "`printf INJECTION_EXECUTED >&2`",
        "bad\nprintf INJECTION_EXECUTED >&2",
    )
    logging_payloads = (
        "##vso[task.complete result=Failed;]forced-failure",
        "prefix ##vso[task.setvariable variable=Injected]yes",
        "safe\n##vso[build.addbuildtag]injected",
        "##vso##vso[[task.complete result=Failed;]nested",
    )

    def assert_logging_commands(self, stdout, expected=()):
        commands = [line for line in stdout.splitlines() if "##vso[" in line]
        self.assertEqual(commands, list(expected), stdout)

    @classmethod
    def setUpClass(cls):
        cls.bash = shutil.which("bash")
        if not cls.bash:
            raise RuntimeError("Bash is required to test pipeline input safety.")
        cls.pipeline_root = Path(__file__).resolve().parents[3] / ".azure-pipelines"

    def load_step(self, filename, display_name, replacements):
        pipeline = (self.pipeline_root / filename).read_text(encoding="utf-8")
        end = pipeline.index(f"displayName: {display_name}\n")
        marker = "- bash: |\n"
        start = pipeline.rindex(marker, 0, end) + len(marker)
        script_end = pipeline.rfind("\n", start, end)
        script = textwrap.dedent(pipeline[start:script_end])
        env_match = re.match(
            r"(?: *condition: [^\n]+\n)? *env:\n(?P<entries>(?: +[A-Z][A-Z0-9_]*: [^\n]+\n)+)",
            pipeline[end + len(f"displayName: {display_name}\n"):],
        )
        self.assertIsNotNone(env_match, f"Missing task environment for {display_name}")
        environment = {}
        for entry in textwrap.dedent(env_match.group("entries")).splitlines():
            name, value = entry.split(": ", 1)
            self.assertIn(value, replacements, f"Unexpected environment mapping: {entry}")
            environment[name] = replacements[value]
        # Model Azure substitution in script source too, so restoring an unsafe
        # inline expression causes the execution assertions below to fail.
        for expression, value in replacements.items():
            script = script.replace(expression, value)
        return script, environment

    def run_step(self, script, environment, directory):
        result = subprocess.run(
            [self.bash, "--noprofile", "--norc", "-s"],
            input=script,
            cwd=directory,
            env={"PATH": os.defpath, "HOME": str(directory), "LANG": "C.UTF-8", **environment},
            capture_output=True,
            text=True,
            timeout=30,
        )
        self.assertNotIn("INJECTION_EXECUTED", result.stderr)
        return result

    def prepare_release(self, bump="patch", custom="x.y.z", branch="main"):
        script, environment = self.load_step(
            "azclips-trigger-stable-release.yml",
            "Prepare Stable Release Branch",
            {
                "${{ parameters.bump_type }}": bump,
                "${{ parameters.custom_version }}": custom,
                "${{ parameters.target_branch }}": branch,
            },
        )
        self.assertNotIn("GH_TOKEN", environment)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            project = root / "src/CLI/Core.Cli.csproj"
            project.parent.mkdir(parents=True)
            original = (
                "<Project>\n<PropertyGroup>\n<Version>1.2.3</Version>\n"
                "<AssemblyVersion>1.2.3.0</AssemblyVersion>\n"
                "<InformationalVersion>1.2.3-beta</InformationalVersion>\n"
                "<FileVersion>1.2.3.0</FileVersion>\n</PropertyGroup>\n</Project>\n"
            )
            project.write_text(original, encoding="utf-8")
            bin_directory = root / "bin"
            bin_directory.mkdir()
            git = bin_directory / "git"
            git.write_text(
                '#!/bin/bash\nprintf "%s\\n" "$@" >> git-calls\n'
                'if [[ "$1" == "ls-remote" ]]; then exit 2; fi\n',
                encoding="utf-8",
            )
            git.chmod(0o755)
            gh = bin_directory / "gh"
            gh.write_text("#!/bin/bash\nexit 0\n", encoding="utf-8")
            gh.chmod(0o755)
            environment["PATH"] = f"{bin_directory}{os.pathsep}{os.defpath}"
            result = self.run_step(script, environment, root)
            calls = root / "git-calls"
            return result, project.read_text(encoding="utf-8"), calls.exists(), original

    def test_normal_version_bumps(self):
        for bump, custom, expected in (
            ("patch", "x.y.z", "1.2.4"),
            ("minor", "x.y.z", "1.3.0"),
            ("major", "x.y.z", "2.0.0"),
            ("custom", " 2.3.4 ", "2.3.4"),
        ):
            with self.subTest(bump=bump):
                result, project, calls, _ = self.prepare_release(bump, custom)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertTrue(calls)
                self.assertIn(f"<Version>{expected}</Version>", project)
                self.assertIn(f"<AssemblyVersion>{expected}.0</AssemblyVersion>", project)
                self.assertIn(f"<InformationalVersion>{expected}-beta</InformationalVersion>", project)
                self.assertIn(f"<FileVersion>{expected}.0</FileVersion>", project)
                self.assertIn("variable=StableTargetBranch]main", result.stdout)

    def test_custom_version_remains_inert_for_every_bump_type(self):
        for bump in ("patch", "minor", "major", "custom"):
            for payload in self.payloads:
                with self.subTest(bump=bump, payload=payload):
                    result, project, _, original = self.prepare_release(bump, payload)
                    if bump == "custom":
                        self.assertNotEqual(result.returncode, 0)
                        self.assertEqual(project, original)
                    else:
                        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_invalid_target_branch_is_rejected_before_git(self):
        for payload in self.payloads:
            with self.subTest(payload=payload):
                result, project, calls, original = self.prepare_release(branch=payload)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("ERROR: target_branch contains unsupported characters", result.stdout)
                self.assertFalse(calls)
                self.assertEqual(project, original)

    def test_rejected_release_inputs_cannot_emit_logging_commands(self):
        for payload in self.logging_payloads:
            with self.subTest(input="custom_version", payload=payload):
                result, project, _, original = self.prepare_release("custom", payload)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("ERROR: custom_version must be stable SemVer", result.stdout)
                self.assertEqual(project, original)
                self.assert_logging_commands(result.stdout)
            with self.subTest(input="target_branch", payload=payload):
                result, project, calls, original = self.prepare_release(branch=payload)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("ERROR: target_branch contains unsupported characters", result.stdout)
                self.assertFalse(calls)
                self.assertEqual(project, original)
                self.assert_logging_commands(result.stdout)

    def run_beta_tag(self, allowed="refs/heads/main", branch="refs/heads/main", project="public"):
        script, environment = self.load_step(
            "azclips-beta-build.yml",
            "Add sign-beta tag for release-project signing trigger",
            {
                "${{ parameters.allowed_signing_source_branch }}": allowed,
                "$(Build.SourceBranch)": branch,
                "$(System.TeamProject)": project,
            },
        )
        with tempfile.TemporaryDirectory() as directory:
            return self.run_step(script, environment, Path(directory))

    def test_beta_tag_input_is_inert(self):
        for payload in self.payloads:
            with self.subTest(payload=payload):
                result = self.run_beta_tag(allowed=payload)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertIn("##vso[build.addbuildtag]sign-beta", result.stdout)

    def test_beta_tag_source_guards(self):
        for branch, allowed, project, succeeds in (
            ("refs/heads/main", "refs/heads/main", "public", True),
            ("refs/heads/release-test", "refs/heads/release-test", "public", True),
            ("refs/heads/other", "refs/heads/main", "public", False),
            ("refs/heads/main", "refs/heads/main", "release", False),
        ):
            with self.subTest(branch=branch, project=project):
                result = self.run_beta_tag(allowed, branch, project)
                self.assertEqual(result.returncode == 0, succeeds, result.stdout + result.stderr)
                self.assertEqual("##vso[build.addbuildtag]sign-beta" in result.stdout, succeeds)

    def test_beta_tag_errors_cannot_emit_logging_commands(self):
        for payload in self.logging_payloads:
            for name, overrides in (
                ("allowed branch", {"allowed": payload, "branch": "refs/heads/other"}),
                ("source branch", {"branch": payload}),
                ("project", {"project": payload}),
            ):
                with self.subTest(input=name, payload=payload):
                    result = self.run_beta_tag(**overrides)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertIn("ERROR: sign_beta", result.stdout)
                    self.assert_logging_commands(result.stdout)

    def run_beta_preflight(self, **overrides):
        replacements = {
            "$(resources.pipeline.betaBuild.runID)": "123",
            "$(resources.pipeline.betaBuild.sourceBranch)": "refs/heads/main",
            "$(Build.SourceBranch)": "refs/heads/main",
            "$(SourceProject)": "public",
            "$(SourcePipelineName)": "azclips-beta-build",
            "$(resources.pipeline.betaBuild.runName)": "azclips-beta-build-main-20261103.1",
            "$(resources.pipeline.betaBuild.sourceCommit)": "a" * 40,
            "$(SourceArtifactName)": "Final_Build_Distribution",
            **overrides,
        }
        script, environment = self.load_step(
            "azclips-beta-sign.yml", "Validate Beta Build Source", replacements
        )
        with tempfile.TemporaryDirectory() as directory:
            return self.run_step(script, environment, Path(directory))

    def test_upstream_metadata_remains_inert(self):
        for expression in (
            "$(resources.pipeline.betaBuild.runName)",
            "$(resources.pipeline.betaBuild.sourceCommit)",
            "$(SourcePipelineName)",
            "$(SourceArtifactName)",
        ):
            for payload in self.payloads:
                with self.subTest(expression=expression, payload=payload):
                    result = self.run_beta_preflight(**{expression: payload})
                    self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                    self.assertIn(payload, result.stdout)

    def test_upstream_metadata_cannot_emit_logging_commands(self):
        for expression in (
            "$(resources.pipeline.betaBuild.runName)",
            "$(resources.pipeline.betaBuild.sourceCommit)",
            "$(SourceProject)",
            "$(SourcePipelineName)",
            "$(SourceArtifactName)",
        ):
            for payload in self.logging_payloads:
                with self.subTest(expression=expression, payload=payload):
                    result = self.run_beta_preflight(**{expression: payload})
                    self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                    self.assert_logging_commands(result.stdout)
                    self.assertIn("##vso_[", result.stdout)

        for payload in self.logging_payloads:
            for overrides in (
                {"$(resources.pipeline.betaBuild.runID)": payload},
                {"$(resources.pipeline.betaBuild.sourceBranch)": payload},
                {
                    "$(resources.pipeline.betaBuild.sourceBranch)": "refs/heads/other",
                    "$(Build.SourceBranch)": payload,
                },
            ):
                with self.subTest(overrides=overrides):
                    result = self.run_beta_preflight(**overrides)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertIn("ERROR:", result.stdout)
                    self.assert_logging_commands(result.stdout)

    def test_beta_preflight_source_guards(self):
        for build_id, branch, pipeline_branch, succeeds in (
            ("123", "refs/heads/main", "refs/heads/main", True),
            ("123", "refs/heads/test", "refs/heads/test", True),
            ("123", "refs/heads/other", "refs/heads/main", False),
            ("", "refs/heads/main", "refs/heads/main", False),
            ("not-a-number", "refs/heads/main", "refs/heads/main", False),
            (self.payloads[0], "refs/heads/main", "refs/heads/main", False),
        ):
            with self.subTest(build_id=build_id, branch=branch):
                result = self.run_beta_preflight(**{
                    "$(resources.pipeline.betaBuild.runID)": build_id,
                    "$(resources.pipeline.betaBuild.sourceBranch)": branch,
                    "$(Build.SourceBranch)": pipeline_branch,
                })
                self.assertEqual(result.returncode == 0, succeeds, result.stdout + result.stderr)

    def test_beta_sign_has_no_queue_parameters(self):
        pipeline = (self.pipeline_root / "azclips-beta-sign.yml").read_text(encoding="utf-8")
        self.assertIsNone(re.search(r"(?m)^parameters:", pipeline))
        self.assertIn("- name: SignedArtifactName\n  value: Final_Signed_Beta_Distribution\n", pipeline)
        self.assertEqual(pipeline.count("signed_artifact_name: ${{ variables.SignedArtifactName }}"), 3)

    def run_musl_validation(self, artifact_name, include_variable=True):
        replacements = {
            "${{ parameters.signed_artifact_name }}": artifact_name,
            "$(Rid)": "linux-musl-x64",
            "$(CliArchiveRelativePath)": "azcli-linux-musl-x64.tar.gz",
            "$(Build.SourcesDirectory)": "/sources",
            "${{ parameters.PackagingLinuxOutRoot }}": "/out-linux",
            "${{ parameters.PackagingCommonOutRoot }}": "/out-common",
            "$(ArtifactVersion)": "1.2.3",
            "$(BuildProfile)": "full",
        }
        script, environment = self.load_step(
            "templates/dist/jobs/validate-build-linux.yml",
            "Validate CLI Linux musl Tarball in Alpine",
            replacements,
        )
        if not include_variable:
            environment.pop("SIGNED_ARTIFACT_NAME")
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            bin_directory = root / "bin"
            bin_directory.mkdir()
            docker = bin_directory / "docker"
            docker.write_text('#!/bin/bash\nprintf "%s\\n" "$@" > docker-args\n', encoding="utf-8")
            docker.chmod(0o755)
            environment["PATH"] = f"{bin_directory}{os.pathsep}{os.defpath}"
            result = self.run_step(script, environment, root)
            arguments = (root / "docker-args").read_text(encoding="utf-8").splitlines()
            return result, arguments

    def test_signed_artifact_name_is_inert(self):
        archive_option = "CLI_TAR_PATH=/out/linux/azcli-linux-musl-x64.tar.gz"
        for payload in ("Final_Signed_Beta_Distribution", *self.payloads):
            with self.subTest(payload=payload):
                result, arguments = self.run_musl_validation(payload)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertIn(archive_option, arguments)
                self.assertNotIn(payload, "\n".join(arguments))

        for include_variable in (True, False):
            with self.subTest(empty=True, include_variable=include_variable):
                result, arguments = self.run_musl_validation("", include_variable)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertNotIn(archive_option, arguments)

    def run_existing_artifact_check(self, artifact_name, status):
        script, environment = self.load_step(
            "templates/dist/steps/publish-artifact.yml",
            "${{ parameters.display_name }} (Check Earlier Attempt)",
            {"${{ parameters.artifact_name }}": artifact_name, "$(System.AccessToken)": "token"},
        )
        environment.update(
            SYSTEM_COLLECTIONURI="https://dev.azure.com/org/",
            SYSTEM_TEAMPROJECTID="project",
            BUILD_BUILDID="42",
            STUB_STATUS=status,
        )
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            bin_directory = root / "bin"
            bin_directory.mkdir()
            curl = bin_directory / "curl"
            curl.write_text('#!/bin/bash\nprintf "%s\\n" "$@" > curl-args\nprintf "%s" "$STUB_STATUS"\n', encoding="utf-8")
            curl.chmod(0o755)
            environment["PATH"] = f"{bin_directory}{os.pathsep}{os.defpath}"
            result = self.run_step(script, environment, root)
            arguments_file = root / "curl-args"
            arguments = arguments_file.read_text(encoding="utf-8") if arguments_file.exists() else ""
            return result, arguments

    def test_retry_reuses_only_existing_artifacts(self):
        name = "Docker_Verification_CLI_AzureLinux3_Lean_Amd64"
        for status, exists in (("200", "true"), ("404", "false")):
            with self.subTest(status=status):
                result, arguments = self.run_existing_artifact_check(name, status)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertIn(
                    f"https://dev.azure.com/org/project/_apis/build/builds/42/artifacts?artifactName={name}&api-version=7.1",
                    arguments,
                )
                self.assertIn("Authorization: Bearer token", arguments)
                self.assert_logging_commands(
                    result.stdout, [f"##vso[task.setvariable variable=PublishArtifactAlreadyExists]{exists}"]
                )

        result, _ = self.run_existing_artifact_check(name, "500")
        self.assertNotEqual(result.returncode, 0)
        self.assertNotIn("setvariable", result.stdout)

    def test_retry_artifact_check_rejects_unsafe_names(self):
        for payload in (*self.payloads, *self.logging_payloads, "name with spaces", "a&b=c"):
            with self.subTest(payload=payload):
                result, arguments = self.run_existing_artifact_check(payload, "200")
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(arguments, "")
                self.assertNotIn("setvariable", result.stdout)


class PipelineArtifactRetryTests(unittest.TestCase):
    # Artifact names are immutable within a build, so a publish that also runs after
    # failures would make "Rerun failed jobs" fail with "Artifact ... already exists".
    publish_task = re.compile(
        r"(?m)^(?P<indent> *)- task: (?:PublishPipelineArtifact@\d+|PublishBuildArtifacts@\d+|\S*PublishSecurityAnalysisLogs@\d+)\n"
        r"(?P<body>(?:(?P=indent)  [^\n]*\n|\s*\n)*)"
    )
    failure_condition = re.compile(r"condition:[^\n]*\b(?:always|succeededOrFailed|failed)\(\)")

    def test_failure_path_publishes_use_retry_safe_template(self):
        pipeline_root = Path(__file__).resolve().parents[3] / ".azure-pipelines"
        template = pipeline_root / "templates/dist/steps/publish-artifact.yml"
        offenders = []
        for path in sorted(pipeline_root.rglob("*.yml")):
            if path == template:
                continue
            for match in self.publish_task.finditer(path.read_text(encoding="utf-8")):
                if self.failure_condition.search(match.group("body")):
                    offenders.append(f"{path.relative_to(pipeline_root)}: {match.group(0).splitlines()[0].strip()}")
        self.assertEqual(
            offenders,
            [],
            "Artifacts published after failures must use templates/dist/steps/publish-artifact.yml so job retries do not collide.",
        )


class ShellPipelineSafetyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.bash = shutil.which("bash")
        if not cls.bash:
            raise RuntimeError("Bash is required to test shell pipeline safety.")
        cls.repo_root = Path(__file__).resolve().parents[3]

    def run_bash(self, script, **environment):
        return subprocess.run(
            [self.bash, "--noprofile", "--norc", "-c", "set -euo pipefail\n" + script],
            env={"PATH": os.defpath, **environment},
            capture_output=True,
            text=True,
            timeout=30,
        )

    def test_extensions_help_preserves_output_and_producer_status(self):
        validator = (
            self.repo_root / "devops/scripts/packaging/common/validate_cli_download.sh"
        ).read_text(encoding="utf-8").splitlines()
        start = next(
            index for index, line in enumerate(validator)
            if "bash -s -- --help" in line and "azcli-extensions-download.sh" in line
        )
        fragment = "\n".join(validator[start:start + 3])
        with tempfile.TemporaryDirectory() as directory:
            install = Path(directory) / "install"
            install.mkdir()
            (install / "azcli-extensions-download.sh").write_text(
                'if [[ "$HELP_CASE" != missing ]]; then printf "Usage: fixture\\n"; fi\n'
                'printf "%1048576s\\n" ""\n'
                'if [[ "$HELP_CASE" == failure ]]; then exit 37; fi\n',
                encoding="utf-8",
            )
            for scenario, expected in (("valid", 0), ("missing", 1), ("failure", 37)):
                with self.subTest(scenario=scenario):
                    result = self.run_bash(fragment, repo_root=directory, HELP_CASE=scenario)
                    self.assertEqual(result.returncode, expected, result.stderr)

    def test_signing_checks_handle_large_output_and_missing_fields(self):
        for filename in ("sign-macos.yml", "validate-sign-macos.yml"):
            source = (
                self.repo_root / ".azure-pipelines/templates/dist/jobs" / filename
            ).read_text(encoding="utf-8")
            checks = re.findall(
                r"if ! ((?:printf|grep)[^\n]*(?:signature_details|entitlements)[^\n]*); then",
                source,
            )
            self.assertTrue(checks, filename)
            for check in checks:
                for present in (True, False):
                    with self.subTest(filename=filename, check=check, present=present):
                        header = (
                            "TeamIdentifier=UBF8T346G9\nflags=runtime\nTimestamp=now\n"
                            "com.apple.security.cs.allow-jit"
                        ) if present else "unrelated output"
                        result = self.run_bash(
                            'printf -v signature_details "%s\\n%1048576s\\n" "$HEADER" ""\n'
                            'entitlements="$signature_details"\n'
                            'entitlement=com.apple.security.cs.allow-jit\n' + check,
                            HEADER=header,
                        )
                        self.assertEqual(result.returncode, 0 if present else 1, result.stderr)

    def test_first_assembly_selection_drains_large_input(self):
        source = (
            self.repo_root / "devops/scripts/packaging/common/build_common.sh"
        ).read_text(encoding="utf-8").splitlines()
        assignment = next(line for line in source if "ext_assembly_name=$(" in line)
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory) / "extension.csproj"
            for content, expected in (
                ("<AssemblyName>First</AssemblyName>\n" * 32768, "First"),
                ("<Project />\n", ""),
            ):
                with self.subTest(expected=expected):
                    project.write_text(content, encoding="utf-8")
                    result = self.run_bash(
                        assignment + '\nprintf "%s" "$ext_assembly_name"', ext=str(project)
                    )
                    self.assertEqual(result.returncode, 0, result.stderr)
                    self.assertEqual(result.stdout, expected)


if __name__ == "__main__":
    unittest.main()
