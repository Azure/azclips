#!/usr/bin/env python3

import unittest

from check_pull_request_changes import (
    find_policy_violations,
    is_stable_release_pull_request,
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
