#!/usr/bin/env python3

import contextlib
import io
import sys
import unittest
from unittest import mock

import packaging_guards
from packaging_guards import (
    COMMENT_MARKER,
    build_comment,
    find_extension_changes_without_powershell,
    find_invalid_version_bumps,
    find_missing_rollup_modules,
    find_version_mismatches,
    get_packageable_modules,
)

REPOSITORY_PATHS = [
    "src/PS/Accounts/Accounts.psd1",
    "src/PS/Az/Az.psd1",
    "src/PS/Compute/Compute.psd1",
    "src/PS/MsGraph/MsGraph.psd1",
]


class RollupValidationTests(unittest.TestCase):
    def test_reports_every_packageable_module_missing_from_rollup(self):
        manifest = """
        RequiredModules = @(
            @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.0' },
            @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.0' }
        )
        """

        self.assertEqual(find_missing_rollup_modules(REPOSITORY_PATHS, manifest), ["MsGraph"])

    def test_succeeds_when_every_packageable_module_is_in_rollup(self):
        manifest = """
        RequiredModules = @(
            @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.0' },
            @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.0' },
            @{ ModuleName = '../MsGraph/MsGraph.psd1'; ModuleVersion = '0.1.0' }
        )
        """

        self.assertEqual(find_missing_rollup_modules(REPOSITORY_PATHS, manifest), [])

    def test_missing_module_emits_warning_without_failing(self):
        output = io.StringIO()
        with (
            mock.patch.object(sys, "argv", ["packaging_guards.py", "validate"]),
            mock.patch.object(
                packaging_guards,
                "validate_repository",
                return_value=(["MsGraph"], []),
            ),
            contextlib.redirect_stdout(output),
        ):
            exit_code = packaging_guards.main()

        self.assertEqual(exit_code, 0)
        self.assertIn("::warning::", output.getvalue())
        self.assertIn("MsGraph", output.getvalue())


class ExtensionWarningTests(unittest.TestCase):
    def test_warns_when_matching_powershell_module_is_untouched(self):
        changed_files = [
            "src/Extensions/Accounts/Processors/LoginProcessor.cs",
            "src/Extensions/Compute/Processors/StartVmProcessor.cs",
            "src/PS/Compute/Cmdlets/StartAzVmCmdlet.cs",
            "src/Extensions/Extension/ExtensionIndex.cs",
        ]

        self.assertEqual(
            find_extension_changes_without_powershell(changed_files, get_packageable_modules(REPOSITORY_PATHS)),
            ["Accounts"],
        )

    def test_comment_explains_both_packaging_findings(self):
        comment = build_comment(
            ["MsGraph"],
            ["Accounts"],
            ["Az.Compute: extension=0.1.1, rollup=0.1.0"],
            ["Accounts: 0.1.0 -> 0.2.0"],
        )

        self.assertIn("Az.MsGraph", comment)
        self.assertIn("`Accounts`", comment)
        self.assertIn("rollup warning", comment)
        self.assertIn("Package version warning", comment)
        self.assertIn("extension=0.1.1", comment)
        self.assertIn("0.1.0 -> 0.2.0", comment)
        self.assertNotIn("validation failed", comment)
        self.assertIn(COMMENT_MARKER, comment)
        self.assertIsNone(build_comment([], [], [], []))


class VersionValidationTests(unittest.TestCase):
    def test_reports_extension_and_rollup_version_mismatch(self):
        rollup = """
        @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.0' }
        """
        extension_projects = {
            "Compute": "<Project><PropertyGroup><Version>0.1.1</Version>" "</PropertyGroup></Project>"
        }

        self.assertEqual(
            find_version_mismatches(rollup, extension_projects),
            ["Az.Compute: extension=0.1.1, rollup=0.1.0"],
        )

    def test_accepts_matching_extension_and_rollup_versions(self):
        rollup = """
        @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.2.0' }
        """
        extension_projects = {
            "Compute": "<Project><PropertyGroup><Version>0.2.0</Version>" "</PropertyGroup></Project>"
        }

        self.assertEqual(
            find_version_mismatches(rollup, extension_projects),
            [],
        )

    def test_accepts_patch_version_bump(self):
        base_projects = {
            "Accounts": "<Project><PropertyGroup><Version>0.1.0</Version>"
            "</PropertyGroup></Project>"
        }
        head_projects = {
            "Accounts": "<Project><PropertyGroup><Version>0.1.1</Version>"
            "</PropertyGroup></Project>"
        }

        self.assertEqual(
            find_invalid_version_bumps(base_projects, head_projects), []
        )

    def test_rejects_major_or_minor_version_bumps(self):
        base_projects = {
            "Accounts": "<Project><PropertyGroup><Version>0.1.0</Version>"
            "</PropertyGroup></Project>",
            "Compute": "<Project><PropertyGroup><Version>0.1.0</Version>"
            "</PropertyGroup></Project>",
        }
        head_projects = {
            "Accounts": "<Project><PropertyGroup><Version>1.1.0</Version>"
            "</PropertyGroup></Project>",
            "Compute": "<Project><PropertyGroup><Version>0.2.0</Version>"
            "</PropertyGroup></Project>",
        }

        self.assertEqual(
            find_invalid_version_bumps(base_projects, head_projects),
            ["Accounts: 0.1.0 -> 1.1.0", "Compute: 0.1.0 -> 0.2.0"],
        )


if __name__ == "__main__":
    unittest.main()
