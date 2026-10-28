#!/usr/bin/env python3

import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import textwrap
import unittest


class StableReleaseTagTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if os.name == "nt":
            cls.bash = str(Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Git/bin/bash.exe")
            if not Path(cls.bash).is_file():
                raise RuntimeError("Git Bash is required to test stable-release tagging on Windows.")
        else:
            cls.bash = shutil.which("bash")
            if not cls.bash:
                raise RuntimeError("Bash is required to test stable-release tagging.")

        pipeline_path = Path(__file__).resolve().parents[3] / ".azure-pipelines/azclips-trigger-stable-release.yml"
        pipeline = pipeline_path.read_text(encoding="utf-8")
        end = pipeline.index("      displayName: Validate And Create Stable Tag")
        marker = "    - bash: |\n"
        start = pipeline.rindex(marker, 0, end) + len(marker)
        cls.tag_script = textwrap.dedent(pipeline[start:end])

    def run_command(self, *args, input_text=None):
        result = subprocess.run(
            args, input=input_text, cwd=self.work, env=self.environment, capture_output=True, text=True, timeout=30
        )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        return result.stdout.strip()

    def test_tagging_release_commits_with_notice(self):
        scenarios = (
            ("version and root NOTICE", True, None, True),
            ("NOTICE without a version bump", False, None, False),
            ("version, NOTICE, and product code", True, "src/CLI/Program.cs", False),
            ("version, NOTICE, and nested NOTICE", True, "docs/NOTICE", False),
        )
        for name, bump_version, extra_path, expect_tag in scenarios:
            with self.subTest(name=name), tempfile.TemporaryDirectory() as directory:
                root = Path(directory).resolve()
                self.work = root / "work"
                self.work.mkdir()
                origin = root / "origin.git"
                self.environment = {
                    **os.environ,
                    "GIT_CONFIG_NOSYSTEM": "1",
                    "GIT_CONFIG_GLOBAL": os.devnull,
                    "GIT_TERMINAL_PROMPT": "0",
                    "GH_TOKEN": "fixture-token",
                }
                self.run_command("git", "init", "--bare", str(origin))
                self.run_command("git", "init", "-b", "main")
                self.run_command("git", "config", "user.name", "Release test")
                self.run_command("git", "config", "user.email", "release-test@example.invalid")
                self.run_command("git", "remote", "add", "origin", origin.as_uri())
                # Route the production script's GitHub push to the local fixture.
                self.run_command(
                    "git", "config", f"url.{origin.as_uri()}.insteadOf", "https://github.com/fixture/release.git"
                )
                project = self.work / "src/CLI/Core.Cli.csproj"
                project.parent.mkdir(parents=True)
                project.write_text(
                    "<Project>\n<PropertyGroup>\n<Version>0.1.0</Version>\n</PropertyGroup>\n</Project>\n",
                    encoding="utf-8",
                )
                notice = self.work / "NOTICE"
                notice.write_text("Previous attribution\n", encoding="utf-8")
                self.run_command("git", "add", ".")
                self.run_command("git", "commit", "-m", "Initial fixture")

                notice.write_text("Updated official attribution\n", encoding="utf-8")
                if bump_version:
                    project.write_text(
                        project.read_text(encoding="utf-8").replace("0.1.0", "0.1.1"), encoding="utf-8"
                    )
                if extra_path:
                    extra_file = self.work / extra_path
                    extra_file.parent.mkdir(parents=True, exist_ok=True)
                    extra_file.write_text("Unrelated change\n", encoding="utf-8")
                self.run_command("git", "add", ".")
                self.run_command("git", "commit", "-m", "Prepare release fixture")
                self.run_command("git", "push", "origin", "main")
                commit = self.run_command("git", "rev-parse", "HEAD")
                script = (
                    self.tag_script.replace("$(Build.SourceVersion)", commit)
                    .replace("$(Build.SourceBranch)", "refs/heads/main")
                    .replace("$(GitHubTargetRepo)", "fixture/release")
                )

                output = self.run_command(self.bash, "--noprofile", "--norc", "-s", input_text=script)

                tags = self.run_command("git", "--git-dir", str(origin), "tag", "--list").splitlines()
                if expect_tag:
                    self.assertEqual(len(tags), 1, output)
                    self.assertRegex(tags[0], r"^v0\.1\.1\.\d{8}T\d{6}Z$")
                    tagged_commit = self.run_command(
                        "git", "--git-dir", str(origin), "rev-parse", f"{tags[0]}^{{commit}}"
                    )
                    self.assertEqual(tagged_commit, commit)
                    self.assertIn("Created and pushed", output)
                else:
                    self.assertEqual(tags, [], output)
                    self.assertIn("Skipping stable tag creation", output)


if __name__ == "__main__":
    unittest.main()
