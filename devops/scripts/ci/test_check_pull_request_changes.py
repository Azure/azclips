#!/usr/bin/env python3

import unittest

from check_pull_request_changes import (
    find_policy_violations,
    is_stable_release_pull_request,
    parse_changed_files,
)


class PullRequestFilePolicyTests(unittest.TestCase):
    def test_rejects_root_changelog_edit(self):
        violations = find_policy_violations(["CHANGELOG.md"], lambda _: "old", lambda _: "new")

        self.assertEqual(len(violations), 1)
        self.assertIn("CHANGELOG.md", violations[0])

    def test_rejects_changes_to_project_version_properties(self):
        base_project = """
<Project><PropertyGroup><Version>0.1.0</Version><FileVersion>0.1.0.0</FileVersion></PropertyGroup></Project>
"""
        head_project = """
<Project><PropertyGroup><Version>0.1.1</Version><FileVersion>0.1.1.0</FileVersion></PropertyGroup></Project>
"""

        violations = find_policy_violations(
            ["src/Example/Example.csproj"],
            lambda _: base_project,
            lambda _: head_project,
        )

        self.assertEqual(len(violations), 2)
        self.assertIn("<FileVersion>", violations[0])
        self.assertIn("<Version>", violations[1])

        conditional_project = head_project.replace(
            "<PropertyGroup>", "<PropertyGroup Condition=\"'$(Configuration)' == 'Release'\">"
        )
        violations = find_policy_violations(
            ["src/Example/Example.csproj"],
            lambda _: head_project,
            lambda _: conditional_project,
        )
        self.assertEqual(len(violations), 2)

    def test_allows_non_version_project_changes_and_new_projects(self):
        base_project = "<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>"
        head_project = (
            "<Project><PropertyGroup><Version>0.1.0</Version><Nullable>enable</Nullable></PropertyGroup></Project>"
        )

        self.assertEqual(
            find_policy_violations(
                ["src/Existing/Existing.csproj"],
                lambda _: base_project,
                lambda _: head_project,
            ),
            [],
        )
        self.assertEqual(
            find_policy_violations(["src/New/New.csproj"], lambda _: None, lambda _: head_project),
            [],
        )

    def test_allows_project_renames_that_preserve_release_versions(self):
        base_project = "<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>"
        head_project = (
            "<Project><PropertyGroup><Version>0.1.0</Version><Nullable>enable</Nullable></PropertyGroup></Project>"
        )
        changed_files, project_renames = parse_changed_files("R098\tsrc/Old/Old.csproj\tsrc/New/New.csproj\n")
        self.assertEqual(
            find_policy_violations(
                changed_files,
                lambda path: base_project if path == "src/Old/Old.csproj" else None,
                lambda path: head_project if path == "src/New/New.csproj" else None,
                project_renames=project_renames,
            ),
            [],
        )

        versionless_base_project = "<Project><PropertyGroup><AssemblyName>Old</AssemblyName></PropertyGroup></Project>"
        versionless_head_project = "<Project><PropertyGroup><AssemblyName>New</AssemblyName></PropertyGroup></Project>"
        self.assertEqual(
            find_policy_violations(
                changed_files,
                lambda path: versionless_base_project if path == "src/Old/Old.csproj" else None,
                lambda path: versionless_head_project if path == "src/New/New.csproj" else None,
                project_renames=project_renames,
            ),
            [],
        )

    def test_rejects_project_renames_that_change_release_versions(self):
        base_project = "<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>"
        changed_version_project = "<Project><PropertyGroup><Version>0.1.1</Version></PropertyGroup></Project>"
        changed_files, project_renames = parse_changed_files("R098\tsrc/Old/Old.csproj\tsrc/New/New.csproj\n")
        violations = find_policy_violations(
            changed_files,
            lambda path: base_project if path == "src/Old/Old.csproj" else None,
            lambda path: changed_version_project if path == "src/New/New.csproj" else None,
            project_renames=project_renames,
        )
        self.assertEqual(len(violations), 1)
        self.assertIn("must preserve release-owned versions", violations[0])

    def test_rejects_unmatched_project_delete_add_pairs(self):
        base_project = "<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>"
        changed_files, project_renames = parse_changed_files("D\tsrc/Old/Old.csproj\nA\tsrc/New/New.csproj\n")

        for head_version in ("0.1.0", "0.1.1"):
            with self.subTest(head_version=head_version):
                head_project = f"<Project><PropertyGroup><Version>{head_version}</Version></PropertyGroup></Project>"
                violations = find_policy_violations(
                    changed_files,
                    lambda path: base_project if path == "src/Old/Old.csproj" else None,
                    lambda path: head_project if path == "src/New/New.csproj" else None,
                    project_renames=project_renames,
                )

                self.assertEqual(len(violations), 1)
                self.assertIn("matched one-to-one as Git renames", violations[0])

    def test_rejects_surplus_project_additions_or_deletions_alongside_rename(self):
        project = "<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>"
        cases = {
            "surplus deletion": (
                "R098\tsrc/Old/Old.csproj\tsrc/New/New.csproj\nD\tsrc/Extra/Extra.csproj\n",
                {"src/Old/Old.csproj", "src/Extra/Extra.csproj"},
                {"src/New/New.csproj"},
            ),
            "surplus addition": (
                "R098\tsrc/Old/Old.csproj\tsrc/New/New.csproj\nA\tsrc/Extra/Extra.csproj\n",
                {"src/Old/Old.csproj"},
                {"src/New/New.csproj", "src/Extra/Extra.csproj"},
            ),
        }

        for name, (diff_output, base_paths, head_paths) in cases.items():
            with self.subTest(name=name):
                changed_files, project_renames = parse_changed_files(diff_output)
                violations = find_policy_violations(
                    changed_files,
                    lambda path: project if path in base_paths else None,
                    lambda path: project if path in head_paths else None,
                    project_renames=project_renames,
                )

                self.assertEqual(len(violations), 1)
                self.assertIn("matched one-to-one as Git renames", violations[0])

    def test_rejects_project_renames_with_a_missing_side(self):
        project = "<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>"
        changed_files, project_renames = parse_changed_files("R098\tsrc/Old/Old.csproj\tsrc/New/New.csproj\n")

        for base_content, head_content in ((None, project), (project, None)):
            with self.subTest(base_missing=base_content is None):
                violations = find_policy_violations(
                    changed_files,
                    lambda path: base_content if path == "src/Old/Old.csproj" else None,
                    lambda path: head_content if path == "src/New/New.csproj" else None,
                    project_renames=project_renames,
                )

                self.assertEqual(len(violations), 1)
                self.assertIn("must preserve release-owned versions", violations[0])

    def test_allows_package_reference_child_version_changes(self):
        base_project = """
<Project><ItemGroup><PackageReference Include="Example"><Version>1.0.0</Version></PackageReference></ItemGroup></Project>
"""
        head_project = """
<Project><ItemGroup><PackageReference Include="Example"><Version>2.0.0</Version></PackageReference></ItemGroup></Project>
"""

        self.assertEqual(
            find_policy_violations(
                ["src/Existing/Existing.csproj"],
                lambda _: base_project,
                lambda _: head_project,
            ),
            [],
        )

    def test_requires_module_changelog_for_extension_source_changes(self):
        changed_files = ["src/Extensions/Compute/Commands/Create.cs"]
        changelog_path = "src/PS/Compute/ChangeLog.md"
        base_changelog = "## Upcoming Release\n\n## Version 0.1.0\n* First preview\n"
        head_changelog = "## Upcoming Release\n\n* Added VM creation support\n\n## Version 0.1.0\n* First preview\n"

        violations = find_policy_violations(
            changed_files,
            lambda _: "existing",
            lambda path: base_changelog if path == changelog_path else "changed",
        )

        self.assertEqual(len(violations), 1)
        self.assertIn(changelog_path, violations[0])

        self.assertEqual(
            find_policy_violations(
                [*changed_files, changelog_path],
                lambda path: base_changelog if path == changelog_path else "existing",
                lambda path: head_changelog if path == changelog_path else "changed",
            ),
            [],
        )

    def test_rejects_changelog_touch_without_upcoming_release_note(self):
        changed_file = "src/Extensions/Compute/Commands/Create.cs"
        changelog_path = "src/PS/Compute/ChangeLog.md"
        base_changelog = "## Upcoming Release\n\n## Version 0.1.0\n* First preview\n"
        head_changelog = base_changelog.replace("First preview", "Initial preview")

        violations = find_policy_violations(
            [changed_file, changelog_path],
            lambda path: base_changelog if path == changelog_path else "existing",
            lambda path: head_changelog if path == changelog_path else "changed",
        )

        self.assertEqual(len(violations), 1)
        self.assertIn("nonempty entry under ## Upcoming Release", violations[0])

    def test_requires_module_changelog_for_powershell_module_changes(self):
        changed_files = ["src/PS/Resources/Generated/NewAzRoleAssignment.cs"]
        changelog_path = "src/PS/Resources/ChangeLog.md"

        violations = find_policy_violations(
            changed_files,
            lambda _: "existing",
            lambda path: "existing" if path == changelog_path else "changed",
        )

        self.assertEqual(len(violations), 1)
        self.assertIn(changelog_path, violations[0])

    def test_does_not_require_changelog_for_non_packageable_or_release_changes(self):
        changed_file = "src/Extensions/Login/Commands/Login.cs"
        base_project = """<Project>
    <PropertyGroup>
        <Version>0.1.0</Version>
    </PropertyGroup>
</Project>
"""
        head_project = base_project.replace("0.1.0", "0.1.1")

        self.assertEqual(
            find_policy_violations(
                [changed_file],
                lambda _: "existing",
                lambda path: None if path == "src/PS/Login/ChangeLog.md" else "changed",
            ),
            [],
        )
        self.assertEqual(
            find_policy_violations(
                ["src/Extensions/Compute/Extensions.Compute.csproj"],
                lambda _: base_project,
                lambda _: head_project,
                stable_release=True,
            ),
            [],
        )

    def test_identifies_stable_release_identity(self):
        release_branch = "release/trigger-stable-release-0.1.2"
        release_title = "{DevOps} Trigger stable release 0.1.2"

        self.assertTrue(
            is_stable_release_pull_request(
                "Azure/azclips",
                "azure/AzCliPs",
                release_branch,
                release_title,
                "x-engineering-agent[bot]",
            )
        )
        self.assertFalse(
            is_stable_release_pull_request(
                "contributor/azclips",
                "Azure/azclips",
                release_branch,
                release_title,
                "x-engineering-agent[bot]",
            )
        )
        self.assertFalse(
            is_stable_release_pull_request(
                "Azure/azclips",
                "Azure/azclips",
                release_branch,
                "{DevOps} Trigger stable release 0.1.3",
                "x-engineering-agent[bot]",
            )
        )
        self.assertFalse(
            is_stable_release_pull_request(
                "Azure/azclips",
                "Azure/azclips",
                release_branch,
                release_title,
                "contributor",
            )
        )

    def test_restricts_generated_stable_release_changes(self):
        base_project = """<Project>
    <PropertyGroup>
        <Version>0.1.0</Version>
        <FileVersion>0.1.0.0</FileVersion>
    </PropertyGroup>
</Project>
"""
        version_only_project = """<Project>
    <PropertyGroup>
        <Version>0.1.1</Version>
        <FileVersion>0.1.1.0</FileVersion>
    </PropertyGroup>
</Project>
"""
        modified_project = version_only_project.replace(
            "    </PropertyGroup>", '        <Target Name="RunCode" />\n    </PropertyGroup>'
        )
        dependency_project = version_only_project.replace(
            "    </PropertyGroup>",
            '    </PropertyGroup>\n    <ItemGroup>\n        <PackageReference Include="Example">\n'
            "            <Version>0.1.0</Version>\n        </PackageReference>\n    </ItemGroup>",
        )
        modified_dependency_project = dependency_project.replace(
            "            <Version>0.1.0</Version>", "            <Version>0.1.1</Version>"
        )

        self.assertEqual(
            find_policy_violations(
                ["CHANGELOG.md", "src/CLI/Core.Cli.csproj"],
                lambda _: base_project,
                lambda _: version_only_project,
                stable_release=True,
            ),
            [],
        )

        violations = find_policy_violations(
            ["src/CLI/Core.Cli.csproj", "src/Unexpected.cs"],
            lambda _: base_project,
            lambda _: modified_project,
            stable_release=True,
        )
        self.assertEqual(len(violations), 2)
        self.assertIn("release metadata", violations[0])
        self.assertIn("project version properties", violations[1])

        added_project_violations = find_policy_violations(
            ["src/Extensions/New/Extensions.New.csproj"],
            lambda _: None,
            lambda _: version_only_project,
            stable_release=True,
        )
        self.assertEqual(len(added_project_violations), 1)
        self.assertIn("may not add or delete project files", added_project_violations[0])

        added_property_project = version_only_project.replace(
            "        <FileVersion>0.1.1.0</FileVersion>",
            "        <FileVersion>0.1.1.0</FileVersion>\n        <PackageVersion>0.1.1</PackageVersion>",
        )
        violations = find_policy_violations(
            ["src/CLI/Core.Cli.csproj"],
            lambda _: base_project,
            lambda _: added_property_project,
            stable_release=True,
        )
        self.assertEqual(len(violations), 1)
        self.assertIn("project version properties", violations[0])

        violations = find_policy_violations(
            ["src/CLI/Core.Cli.csproj"],
            lambda _: dependency_project,
            lambda _: modified_dependency_project,
            stable_release=True,
        )
        self.assertEqual(len(violations), 1)
        self.assertIn("project version properties", violations[0])

        deleted_version_project = base_project.replace("        <Version>0.1.0</Version>\n", "")
        violations = find_policy_violations(
            ["src/CLI/Core.Cli.csproj"],
            lambda _: base_project,
            lambda _: deleted_version_project,
            stable_release=True,
        )
        self.assertEqual(len(violations), 1)

        self.assertEqual(
            find_policy_violations(
                ["src/Extensions/Accounts/Extensions.Accounts.csproj"],
                lambda _: "\ufeff" + base_project,
                lambda _: "\ufeff" + version_only_project,
                stable_release=True,
            ),
            [],
        )

        for mismatched_path in (
            "src/Extensions/Accounts/Extensions.Other.csproj",
            "src/PS/Accounts/Other.psd1",
        ):
            violations = find_policy_violations(
                [mismatched_path],
                lambda _: base_project,
                lambda _: version_only_project,
                stable_release=True,
            )
            self.assertIn("release metadata", violations[0])

        for non_version_project in (
            version_only_project.replace("    <PropertyGroup>", "      <PropertyGroup>"),
            version_only_project.replace("    </PropertyGroup>", "        <!-- release -->\n    </PropertyGroup>"),
        ):
            violations = find_policy_violations(
                ["src/CLI/Core.Cli.csproj"],
                lambda _: base_project,
                lambda _: non_version_project,
                stable_release=True,
            )
            self.assertEqual(len(violations), 1)
            self.assertIn("project version properties", violations[0])

    def test_validates_generated_release_version_mapping_and_notes(self):
        project = """<Project>
    <PropertyGroup>
        <Version>0.1.3</Version>
    </PropertyGroup>
</Project>
"""
        module_manifest = """@{
ModuleVersion = '0.1.3'
PrivateData = @{
    PSData = @{
        ReleaseNotes = @'
* Added compute support
'@
    }
}
}
"""
        rollup_manifest = """@{
ModuleVersion = '0.1.5'
RequiredModules = @(
    @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.3' }
)
PrivateData = @{
    PSData = @{
        ReleaseNotes = @'
0.1.5 - 2026-10-13 00:00 UTC
Az.Compute 0.1.3
* Added compute support
'@
    }
}
}
"""
        files = {
            "CHANGELOG.md": "## v0.1.5 - 2026-10-13 00:00 UTC\n",
            "src/CLI/Core.Cli.csproj": project.replace("0.1.3", "0.1.5"),
            "src/Extensions/Compute/Extensions.Compute.csproj": project,
            "src/PS/Compute/Compute.psd1": module_manifest,
            "src/PS/Compute/ChangeLog.md": (
                "## Upcoming Release\n\n## Version 0.1.3\n* Added compute support\n"
            ),
            "src/PS/Az/Az.psd1": rollup_manifest,
        }
        changed_files = list(files)

        self.assertEqual(
            find_policy_violations(
                changed_files,
                lambda path: files.get(path),
                lambda path: files.get(path),
                stable_release=True,
                stable_release_version="0.1.5",
            ),
            [],
        )

        stale_files = dict(files)
        stale_files["src/PS/Compute/Compute.psd1"] = module_manifest.replace(
            "* Added compute support", "* First preview"
        )
        stale_files["src/PS/Az/Az.psd1"] = rollup_manifest.replace(
            "0.1.5 - 2026-10-13 00:00 UTC", "0.1.4 - 2026-09-30 00:00 UTC"
        )
        violations = find_policy_violations(
            changed_files,
            lambda path: files.get(path),
            lambda path: stale_files.get(path),
            stable_release=True,
            stable_release_version="0.1.5",
        )

        self.assertEqual(len(violations), 2)
        self.assertTrue(any("Compute.psd1: ReleaseNotes" in violation for violation in violations))
        self.assertTrue(any("Az.psd1: ReleaseNotes" in violation for violation in violations))

    def test_validates_cli_only_release_and_rejects_orphan_module_metadata(self):
        core_project = """<Project>
    <PropertyGroup>
        <Version>0.1.5</Version>
    </PropertyGroup>
</Project>
"""
        rollup_manifest = """@{
ModuleVersion = '0.1.5'
PrivateData = @{
    PSData = @{
        ReleaseNotes = @'
0.1.5 - 2026-10-13 00:00 UTC
No PowerShell module changes.
'@
    }
}
}
"""
        files = {
            "CHANGELOG.md": "## v0.1.5 - 2026-10-13 00:00 UTC\n",
            "src/CLI/Core.Cli.csproj": core_project,
            "src/PS/Az/Az.psd1": rollup_manifest,
        }

        self.assertEqual(
            find_policy_violations(
                list(files),
                lambda path: files.get(path),
                lambda path: files.get(path),
                stable_release=True,
                stable_release_version="0.1.5",
            ),
            [],
        )

        orphan_files = dict(files)
        orphan_files["src/PS/Compute/Compute.psd1"] = "@{ ModuleVersion = '0.1.3' }"
        violations = find_policy_violations(
            list(orphan_files),
            lambda path: files.get(path),
            lambda path: orphan_files.get(path),
            stable_release=True,
            stable_release_version="0.1.5",
        )

        self.assertTrue(any("matching extension version bump" in violation for violation in violations))


if __name__ == "__main__":
    unittest.main()
