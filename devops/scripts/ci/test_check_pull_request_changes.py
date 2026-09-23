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
                self.assertIn("unless Git identifies them as renames", violations[0])

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


if __name__ == "__main__":
    unittest.main()
