#!/usr/bin/env python3

import unittest

from packaging_guards import (
    COMMENT_MARKER,
    build_comment,
    find_extension_changes_without_powershell,
    find_missing_rollup_modules,
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

        self.assertEqual(
            find_missing_rollup_modules(REPOSITORY_PATHS, manifest), ["MsGraph"]
        )

    def test_succeeds_when_every_packageable_module_is_in_rollup(self):
        manifest = """
        RequiredModules = @(
            @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.0' },
            @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.0' },
            @{ ModuleName = '../MsGraph/MsGraph.psd1'; ModuleVersion = '0.1.0' }
        )
        """

        self.assertEqual(find_missing_rollup_modules(REPOSITORY_PATHS, manifest), [])


class ExtensionWarningTests(unittest.TestCase):
    def test_warns_when_matching_powershell_module_is_untouched(self):
        changed_files = [
            "src/Extensions/Accounts/Processors/LoginProcessor.cs",
            "src/Extensions/Compute/Processors/StartVmProcessor.cs",
            "src/PS/Compute/Cmdlets/StartAzVmCmdlet.cs",
            "src/Extensions/Extension/ExtensionIndex.cs",
        ]

        self.assertEqual(
            find_extension_changes_without_powershell(
                changed_files, get_packageable_modules(REPOSITORY_PATHS)
            ),
            ["Accounts"],
        )

    def test_comment_explains_both_packaging_findings(self):
        comment = build_comment(["MsGraph"], ["Accounts"])

        self.assertIn("Az.MsGraph", comment)
        self.assertIn("`Accounts`", comment)
        self.assertIn(COMMENT_MARKER, comment)
        self.assertIsNone(build_comment([], []))


if __name__ == "__main__":
    unittest.main()