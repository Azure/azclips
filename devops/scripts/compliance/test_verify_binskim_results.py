#!/usr/bin/env python3

import copy
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).with_name('verify_binskim_results.ps1')
POLICY = json.loads(SCRIPT.with_name('binskim-runtime-pdb-exceptions.json').read_text(encoding='utf-8'))


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

    def invoke(self, allow=False, write=True):
        if write:
            (self.reports / 'binskim.sarif').write_text(json.dumps(self.document), encoding='utf-8')
        args = [self.pwsh, '-NoLogo', '-NoProfile', '-File', str(SCRIPT), '-ReportRoot', str(self.reports), '-ScanRoot', str(self.scan)]
        if allow:
            args.append('-AllowReviewedRuntimePdbGaps')
        return subprocess.run(args, capture_output=True, text=True)

    def assert_failed(self, result):
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)

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

    def test_engine_exception_fails_even_if_invocation_claims_success(self):
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [self.notification(rule='ERR999.UnhandledEngineException')]
        self.assert_failed(self.invoke(allow=True))

    def test_all_reviewed_runtime_pdb_gaps_warn(self):
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [self.notification(name) for name in POLICY['files']]
        result = self.invoke(allow=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.count('task.logissue type=warning'), len(POLICY['files']))

    def test_powershell_scan_does_not_opt_into_cli_runtime_exceptions(self):
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [self.notification()]
        self.assert_failed(self.invoke())

    def test_unreviewed_binary_pdb_error_fails(self):
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [self.notification('az.exe')]
        self.assert_failed(self.invoke(allow=True))

    def test_new_versioned_runtime_requires_review(self):
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [self.notification('mscordaccore_arm64_arm64_99.0.0.0.dll')]
        self.assert_failed(self.invoke(allow=True))

    def test_nested_runtime_name_is_not_exempt(self):
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [self.notification('extension/coreclr.dll')]
        self.assert_failed(self.invoke(allow=True))

    def test_path_outside_scan_root_is_not_exempt(self):
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [self.notification('../coreclr.dll')]
        self.assert_failed(self.invoke(allow=True))

    def test_different_pdb_error_is_not_exempt(self):
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [self.notification(code='E_PDB_INVALID_SIG')]
        self.assert_failed(self.invoke(allow=True))

    def test_location_is_required_for_exception(self):
        notice = self.notification()
        del notice['locations']
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [notice]
        self.assert_failed(self.invoke(allow=True))

    def test_artifact_index_location_is_supported(self):
        notice = self.notification()
        location = notice['locations'][0]['physicalLocation']['artifactLocation']
        self.document['runs'][0]['artifacts'] = [{'location': copy.deepcopy(location)}]
        notice['locations'][0]['physicalLocation']['artifactLocation'] = {'index': 0}
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [notice]
        result = self.invoke(allow=True)
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_uri_base_id_location_is_supported(self):
        notice = self.notification()
        notice['locations'][0]['physicalLocation']['artifactLocation'] = {'uri': 'coreclr.dll', 'uriBaseId': 'PUBLISH'}
        self.document['runs'][0]['originalUriBaseIds'] = {'PUBLISH': {'uri': self.scan.as_uri() + '/'}}
        self.document['runs'][0]['invocations'][0]['toolExecutionNotifications'] = [notice]
        result = self.invoke(allow=True)
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_security_findings_on_vendor_runtime_still_fail(self):
        self.document['runs'][0]['results'] = [{'ruleId': 'BA2008', 'level': 'error', 'message': {'text': 'coreclr.dll lacks CFG.'}}]
        self.assert_failed(self.invoke(allow=True))

    def test_other_tool_warning_fails(self):
        notice = self.notification(rule='UnsupportedTarget')
        notice['level'] = 'warning'
        self.document['runs'][0]['invocations'][0]['toolConfigurationNotifications'] = [notice]
        self.assert_failed(self.invoke(allow=True))

    def test_engine_error_in_results_fails(self):
        self.document['runs'][0]['results'] = [{'ruleId': 'ERR999.UnhandledEngineException', 'level': 'warning', 'message': {'text': 'Missing --output'}}]
        self.assert_failed(self.invoke(allow=True))

    def test_valid_report_does_not_hide_second_failed_report(self):
        failed = copy.deepcopy(self.document)
        failed['runs'][0]['invocations'][0]['executionSuccessful'] = False
        (self.reports / 'second.sarif').write_text(json.dumps(failed), encoding='utf-8')
        self.assert_failed(self.invoke())


if __name__ == '__main__':
    unittest.main()
