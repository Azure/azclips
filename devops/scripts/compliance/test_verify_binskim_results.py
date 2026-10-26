#!/usr/bin/env python3

import fnmatch
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).with_name('verify_binskim_results.ps1')
POLICY = json.loads(SCRIPT.with_name('binskim-runtime-exclusions.json').read_text(encoding='utf-8'))


class BinSkimReportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pwsh = shutil.which('pwsh')
        if not cls.pwsh:
            raise RuntimeError('PowerShell 7 (pwsh) is required for these tests.')

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='binskim report presence tests ')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.reports = self.root / 'reports'
        self.reports.mkdir()

    def invoke(self):
        args = [self.pwsh, '-NoLogo', '-NoProfile', '-File', str(SCRIPT), '-ReportRoot', str(self.reports)]
        return subprocess.run(args, capture_output=True, text=True)

    def assert_failed(self, result):
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_existing_sarif_file_passes(self):
        (self.reports / 'binskim.sarif').write_text('{"version":"2.1.0","runs":[]}', encoding='utf-8')
        result = self.invoke()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('Found 1 BinSkim SARIF report file(s).', result.stdout)

    def test_missing_report_directory_fails(self):
        self.reports.rmdir()
        self.assert_failed(self.invoke())

    def test_no_report_fails(self):
        self.assert_failed(self.invoke())

    def test_report_root_must_be_a_directory(self):
        self.reports.rmdir()
        self.reports.write_text('not a directory', encoding='utf-8')
        self.assert_failed(self.invoke())

    def test_other_file_types_do_not_count(self):
        (self.reports / 'options.json').write_text('{}', encoding='utf-8')
        self.assert_failed(self.invoke())

    def test_directory_named_sarif_does_not_count(self):
        (self.reports / 'binskim.sarif').mkdir()
        self.assert_failed(self.invoke())

    def test_nested_sarif_file_is_found(self):
        nested = self.reports / '001'
        nested.mkdir()
        (nested / 'binskim.sarif').touch()
        result = self.invoke()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('Found 1 BinSkim SARIF report file(s).', result.stdout)

    def test_contents_are_not_parsed_or_modified(self):
        report = self.reports / 'binskim.sarif'
        for contents in [b'', b'{invalid JSON', b'{"runs":[{"invocations":[{"executionSuccessful":false,"exitCode":1}]}]}']:
            with self.subTest(contents=contents):
                report.write_bytes(contents)
                result = self.invoke()
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertEqual(report.read_bytes(), contents)

    def test_multiple_sarif_files_are_counted(self):
        (self.reports / 'first.sarif').touch()
        (self.reports / 'second.sarif').touch()
        result = self.invoke()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('Found 2 BinSkim SARIF report file(s).', result.stdout)


class BinSkimTargetTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pwsh = shutil.which('pwsh')
        if not cls.pwsh:
            raise RuntimeError('PowerShell 7 (pwsh) is required for these tests.')

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='binskim target tests ')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.out = self.root / 'extracted zip'
        self.script = SCRIPT.with_name('get_binskim_cli_targets.ps1')

    def invoke(self):
        return subprocess.run([self.pwsh, '-NoLogo', '-NoProfile', '-File', str(self.script), '-ScanRoot', str(self.out)], capture_output=True, text=True)

    def targets(self):
        result = self.invoke()
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout.strip().split(';')

    def excluded(self, targets, relative):
        path = (self.out / relative).as_posix().lower()
        return any(fnmatch.fnmatchcase(path, target[len('-:file|'):].lower()) for target in targets if target.startswith('-:file|'))

    def test_exact_runtime_exclusions_at_archive_root(self):
        targets = self.targets()
        self.assertEqual(targets[:2], [self.out.as_posix() + '/**/*.dll', self.out.as_posix() + '/**/*.exe'])
        for name in POLICY['files']:
            self.assertIn('-:file|' + self.out.as_posix() + '/' + name, targets)
            self.assertTrue(self.excluded(targets, name))

    def test_nested_copies_remain_in_scope(self):
        targets = self.targets()
        self.assertFalse(self.excluded(targets, 'Extensions/Other/coreclr.dll'))
        self.assertFalse(self.excluded(targets, 'somewhere-else/coreclr.dll'))

    def test_first_party_and_other_dependencies_remain_in_scope(self):
        targets = self.targets()
        for name in ['az.exe', 'Microsoft.Azure.Azclips.Core.dll', 'Microsoft.Other.dll', 'System.Other.dll', 'mscordaccore_amd64_amd64_99.0.0.0.dll']:
            self.assertFalse(self.excluded(targets, name))

    def test_existing_msal_and_copilot_exclusions_remain(self):
        targets = self.targets()
        self.assertTrue(self.excluded(targets, 'msalruntime.dll'))
        self.assertTrue(self.excluded(targets, 'Extensions/Other/msalruntime_arm64.dll'))
        self.assertTrue(self.excluded(targets, 'Extensions/Microsoft.Azure.Azclips.Extensions.Agent/deps/runtimes/win-x64/native/copilot.exe'))
        self.assertFalse(self.excluded(targets, 'Extensions/Other/deps/runtimes/win-x64/native/other.dll'))

    def test_all_exclusions_are_anchored_to_the_scan_root(self):
        prefix = '-:file|' + self.out.as_posix() + '/'
        exclusions = [target for target in self.targets() if target.startswith('-:file|')]
        self.assertTrue(exclusions)
        for target in exclusions:
            self.assertTrue(target.startswith(prefix), target)

    def test_exclusion_list_cannot_introduce_wildcards_or_paths(self):
        self.script = self.root / 'get_binskim_cli_targets.ps1'
        shutil.copyfile(SCRIPT.with_name(self.script.name), self.script)
        for name in ['*.dll', 'nested/coreclr.dll', '../coreclr.dll', 'System.*.dll', 'name.dll;*.exe']:
            with self.subTest(name=name):
                (self.root / 'binskim-runtime-exclusions.json').write_text(json.dumps({'files': [name]}), encoding='utf-8')
                self.assertNotEqual(self.invoke().returncode, 0)

    def test_missing_or_empty_exclusion_list_fails(self):
        self.script = self.root / 'get_binskim_cli_targets.ps1'
        shutil.copyfile(SCRIPT.with_name(self.script.name), self.script)
        self.assertNotEqual(self.invoke().returncode, 0)
        (self.root / 'binskim-runtime-exclusions.json').write_text('{"files": []}', encoding='utf-8')
        self.assertNotEqual(self.invoke().returncode, 0)


if __name__ == '__main__':
    unittest.main()
