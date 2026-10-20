#!/usr/bin/env python3

import copy
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
        self.temp = tempfile.TemporaryDirectory(prefix='binskim health tests ')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.reports = self.root / 'reports'
        self.reports.mkdir()
        self.scan = self.root / 'publish' / 'cli' / 'win-arm64'
        self.scan.mkdir(parents=True)
        self.document = {
            'version': '2.1.0',
            'runs': [{
                'tool': {'driver': {'name': 'BinSkim'}},
                'invocations': [{'executionSuccessful': True}],
                'results': [],
            }],
        }

    def notification(self, filename='coreclr.dll', rule='ERR997.ExceptionLoadingPdb', code='E_PDB_NOT_FOUND'):
        return {
            'descriptor': {'id': rule},
            'level': 'error',
            'message': {'text': "'{0}' was not evaluated ({1}).", 'arguments': [filename, code]},
            'locations': [{'physicalLocation': {'artifactLocation': {'uri': (self.scan / filename).as_uri()}}}],
        }

    def invoke(self, write=True):
        if write:
            (self.reports / 'binskim.sarif').write_text(json.dumps(self.document), encoding='utf-8')
        args = [self.pwsh, '-NoLogo', '-NoProfile', '-File', str(SCRIPT), '-ReportRoot', str(self.reports)]
        return subprocess.run(args, capture_output=True, text=True)

    def assert_failed(self, result):
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)

    def completed_pdb_failure(self):
        invocation = self.document['runs'][0]['invocations'][0]
        invocation.update({
            'executionSuccessful': False,
            'startTimeUtc': '2026-10-19T22:16:48Z',
            'endTimeUtc': '2026-10-19T22:17:54Z',
            'toolConfigurationNotifications': [self.notification()],
        })
        return invocation

    def test_completed_pdb_only_failure_is_no_longer_exempt(self):
        self.completed_pdb_failure()
        self.assert_failed(self.invoke())

    def test_failed_invocation_with_zero_exit_code_still_fails(self):
        self.completed_pdb_failure()['exitCode'] = 0
        self.assert_failed(self.invoke())

    def test_crash_signal_fails(self):
        self.document['runs'][0]['invocations'][0]['exitSignalName'] = 'SIGSEGV'
        self.assert_failed(self.invoke())

    def test_numeric_signal_fails(self):
        self.document['runs'][0]['invocations'][0]['exitSignalNumber'] = 11
        self.assert_failed(self.invoke())

    def test_missing_status_fails(self):
        del self.document['runs'][0]['invocations'][0]['executionSuccessful']
        result = self.invoke()
        self.assert_failed(result)
        self.assertIn('executionSuccessful=<missing>', result.stdout)

    def test_successful_invocation_does_not_hide_second_failure(self):
        self.document['runs'][0]['invocations'].append({'executionSuccessful': False})
        self.assert_failed(self.invoke())

    def test_clean_report_with_zero_findings_passes(self):
        result = self.invoke()
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_missing_report_directory_fails(self):
        self.reports.rmdir()
        self.assert_failed(self.invoke(write=False))

    def test_no_report_fails(self):
        self.assert_failed(self.invoke(write=False))

    def test_malformed_report_fails(self):
        (self.reports / 'binskim.sarif').write_text('{invalid', encoding='utf-8')
        self.assert_failed(self.invoke(write=False))

    def test_missing_runs_fails(self):
        del self.document['runs']
        self.assert_failed(self.invoke())

    def test_empty_runs_fails(self):
        self.document['runs'] = []
        self.assert_failed(self.invoke())

    def test_missing_invocation_fails(self):
        del self.document['runs'][0]['invocations']
        self.assert_failed(self.invoke())

    def test_wrong_tool_fails(self):
        self.document['runs'][0]['tool']['driver']['name'] = 'OtherTool'
        self.assert_failed(self.invoke())

    def test_failed_execution_fails(self):
        self.document['runs'][0]['invocations'][0]['executionSuccessful'] = False
        self.assert_failed(self.invoke())

    def test_nonzero_exit_code_fails(self):
        self.document['runs'][0]['invocations'][0]['exitCode'] = 1
        self.assert_failed(self.invoke())

    def test_engine_exception_with_failed_invocation_fails(self):
        self.document['runs'][0]['invocations'][0]['executionSuccessful'] = False
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [self.notification(rule='ERR999.UnhandledEngineException')]
        self.assert_failed(self.invoke())

    def test_notifications_do_not_override_successful_invocation(self):
        for field in ['toolExecutionNotifications', 'toolConfigurationNotifications']:
            for level in ['note', 'warning', 'error']:
                with self.subTest(field=field, level=level):
                    notice = self.notification(rule='ERR999.UnhandledEngineException')
                    notice['level'] = level
                    self.document['runs'][0]['invocations'][0] = {'executionSuccessful': True, field: [notice]}
                    result = self.invoke()
                    self.assertEqual(result.returncode, 0, result.stderr)

    def test_findings_and_suppressions_are_left_to_postanalysis(self):
        self.document['runs'][0]['results'] = [
            {'ruleId': 'BA2008', 'level': 'error', 'message': {'text': 'Missing CFG.'}},
            {'ruleId': 'BA2018', 'level': 'error', 'message': {'id': 'PdbMissing', 'arguments': ['sample.dll', 'E_PDB_NOT_FOUND']}},
            {'ruleId': 'ERR999.UnhandledEngineException', 'level': 'warning', 'message': {'text': 'Diagnostic recorded as a result.'}},
            {'ruleId': 'BA2008', 'level': 'error', 'message': {'text': 'Approved suppression.'}, 'suppressions': [{'kind': 'external', 'status': 'accepted'}]},
        ]
        result = self.invoke()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('Finding enforcement is delegated to PostAnalysis.', result.stdout)
        self.assertEqual(json.loads((self.reports / 'binskim.sarif').read_text(encoding='utf-8')), self.document)

    def test_empty_invocations_fail(self):
        self.document['runs'][0]['invocations'] = []
        self.assert_failed(self.invoke())

    def test_non_boolean_success_status_fails(self):
        self.document['runs'][0]['invocations'][0]['executionSuccessful'] = 'true'
        self.assert_failed(self.invoke())

    def test_unsupported_report_version_fails(self):
        self.document['version'] = '1.0.0'
        self.assert_failed(self.invoke())

    def test_valid_report_does_not_hide_second_failed_report(self):
        failed = copy.deepcopy(self.document)
        failed['runs'][0]['invocations'][0]['executionSuccessful'] = False
        (self.reports / 'second.sarif').write_text(json.dumps(failed), encoding='utf-8')
        self.assert_failed(self.invoke())


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
        self.out = self.root / 'windows outputs'
        self.script = SCRIPT.with_name('get_binskim_cli_targets.ps1')

    def invoke(self, rid='win-x64'):
        return subprocess.run([self.pwsh, '-NoLogo', '-NoProfile', '-File', str(self.script), '-OutRoot', str(self.out), '-Rid', rid], capture_output=True, text=True)

    def targets(self, rid='win-x64'):
        result = self.invoke(rid)
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout.strip().split(';')

    def excluded(self, targets, relative):
        path = (self.out / relative).as_posix().lower()
        return any(fnmatch.fnmatchcase(path, target[len('-:file|'):].lower()) for target in targets if target.startswith('-:file|'))

    def test_exact_runtime_exclusions_for_both_architectures(self):
        for rid in ['win-x64', 'win-arm64']:
            with self.subTest(rid=rid):
                targets = self.targets(rid)
                self.assertEqual(targets[:2], [self.out.as_posix() + '/**/*.dll', self.out.as_posix() + '/**/*.exe'])
                for name in POLICY['files']:
                    self.assertIn('-:file|' + self.out.as_posix() + '/publish/cli/' + rid + '/' + name, targets)
                    self.assertTrue(self.excluded(targets, 'publish/cli/' + rid + '/' + name))

    def test_nested_and_other_architecture_copies_remain_in_scope(self):
        targets = self.targets()
        self.assertFalse(self.excluded(targets, 'publish/cli/win-x64/Extensions/Other/coreclr.dll'))
        self.assertFalse(self.excluded(targets, 'publish/cli/win-arm64/coreclr.dll'))
        self.assertFalse(self.excluded(targets, 'somewhere-else/coreclr.dll'))

    def test_first_party_and_other_dependencies_remain_in_scope(self):
        targets = self.targets()
        for name in ['az.exe', 'Microsoft.Azure.Azclips.Core.dll', 'Microsoft.Other.dll', 'System.Other.dll', 'mscordaccore_amd64_amd64_99.0.0.0.dll']:
            self.assertFalse(self.excluded(targets, 'publish/cli/win-x64/' + name))

    def test_existing_msal_and_copilot_exclusions_remain(self):
        targets = self.targets()
        self.assertTrue(self.excluded(targets, 'publish/cli/win-x64/msalruntime.dll'))
        self.assertTrue(self.excluded(targets, 'publish/cli/win-x64/Extensions/Microsoft.Azure.Azclips.Extensions.Agent/deps/runtimes/win-x64/native/copilot.exe'))
        self.assertFalse(self.excluded(targets, 'publish/cli/win-x64/Extensions/Other/deps/runtimes/win-x64/native/other.dll'))

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
