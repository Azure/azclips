#!/usr/bin/env python3

import contextlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import scan_recordings


class RecordingSecretScanTests(unittest.TestCase):
    def recording(self, value, encoding="literal"):
        content = json.dumps({"body": value}, indent=2)
        if encoding == "upper":
            content = content.replace("&", r"\u0026").replace("<", r"\u003C").replace(">", r"\u003E")
        elif encoding == "lower":
            content = content.replace("&", r"\u0026").replace("<", r"\u003c").replace(">", r"\u003e")
        elif encoding == "mixed":
            content = content.replace("<", r"\u003C")
        return content

    def assert_scan(self, content, expected_rule=None, expected_line=2):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "recording.json"
            path.write_text(content, encoding="utf-8")
            stdout, stderr = io.StringIO(), io.StringIO()
            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                result = scan_recordings.main([str(path)])
        if expected_rule:
            self.assertEqual(result, 1)
            location = f":{expected_line}:" if expected_line is not None else ":"
            self.assertIn(f"{location} possible {expected_rule} in recording", stderr.getvalue())
            self.assertNotIn("fixture-secret", stderr.getvalue())
        else:
            self.assertEqual(result, 0, stderr.getvalue())
            self.assertEqual(stderr.getvalue(), "")
            self.assertIn("no secrets found", stdout.getvalue())

    def test_sas_signatures_fail_and_redacted_signatures_pass(self):
        for separator in ("?", "&", "&amp;", "%26"):
            for assignment in ("=", "%3D"):
                for encoding in ("literal", "upper"):
                    with self.subTest(separator=separator, assignment=assignment, encoding=encoding):
                        prefix = f"https://example.invalid/blob{separator}sig{assignment}"
                        self.assert_scan(
                            self.recording(prefix + "fixture-secret", encoding),
                            "shared access signature",
                        )
                        self.assert_scan(self.recording(prefix + "[redacted]", encoding))

    def test_sas_signing_ids_fail_and_placeholder_ids_pass(self):
        for parameter in ("skoid", "sktid", "saoid", "suoid", "sduoid", "scid"):
            for encoding in ("literal", "upper"):
                for assignment in ("=", "%3D"):
                    with self.subTest(parameter=parameter, encoding=encoding, assignment=assignment):
                        prefix = f"https://example.invalid/blob?sv=fixture&{parameter}{assignment}"
                        self.assert_scan(
                            self.recording(prefix + "11111111-1111-1111-1111-111111111111", encoding),
                            "sas signed key ids",
                        )
                        for placeholder in (
                            scan_recordings.PLACEHOLDER_GUID,
                            scan_recordings.FAKE_OBJECT_ID,
                            "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX",
                        ):
                            self.assert_scan(self.recording(prefix + placeholder, encoding))

    def test_delegation_key_secrets_fail_in_literal_and_escaped_xml(self):
        for element in ("Value", "SignedOid", "SignedTid", "SignedDelegatedUserTid"):
            for encoding in ("literal", "upper", "lower", "mixed"):
                with self.subTest(element=element, encoding=encoding):
                    xml = f"<UserDelegationKey><{element}>fixture-secret</{element}></UserDelegationKey>"
                    self.assert_scan(self.recording(xml, encoding), "user delegation key")

    def test_fake_delegation_key_and_identity_ids_pass(self):
        xml = (
            "<UserDelegationKey>"
            "<Value>veryFakedStorageAccountKey==</Value>"
            f"<SignedOid>{scan_recordings.FAKE_OBJECT_ID}</SignedOid>"
            f"<SignedTid>{scan_recordings.PLACEHOLDER_GUID}</SignedTid>"
            f"<SignedDelegatedUserTid>{scan_recordings.PLACEHOLDER_GUID}</SignedDelegatedUserTid>"
            "</UserDelegationKey>"
        )
        for encoding in ("literal", "upper", "lower", "mixed"):
            with self.subTest(encoding=encoding):
                self.assert_scan(self.recording(xml, encoding))

    def test_blob_tag_values_outside_delegation_key_are_not_keys(self):
        xml = (
            "<UserDelegationKey><Value>veryFakedStorageAccountKey==</Value></UserDelegationKey>"
            "<Tags><TagSet><Tag><Key>label</Key><Value>fixture-secret</Value></Tag></TagSet></Tags>"
        )
        for encoding in ("literal", "upper", "lower", "mixed"):
            with self.subTest(encoding=encoding):
                self.assert_scan(self.recording(xml, encoding))

    def test_arm_key_values_fail_and_fake_key_values_pass(self):
        for value, expected_rule in (
            ("fixture-secret", "storage account key value"),
            ("veryFakedStorageAccountKey==", None),
        ):
            for key in (
                {"keyName": "key1", "value": value},
                {"value": value, "keyName": "key1"},
                {"keyName": "key1", "permissions": "FULL", "value": value},
            ):
                for encoding in ("literal", "upper"):
                    with self.subTest(value=value, key=key, encoding=encoding):
                        body = json.dumps({"keys": [key]})
                        self.assert_scan(self.recording(body, encoding), expected_rule)
                with self.subTest(value=value, key=key, raw=True):
                    self.assert_scan(json.dumps({"keys": [key]}, indent=2), expected_rule, expected_line=None)

    def test_key_name_and_unrelated_value_in_different_objects_do_not_match(self):
        body = json.dumps({"keys": [{"keyName": "key1"}, {"value": "fixture-secret"}]})
        self.assert_scan(self.recording(body))

    def test_key_value_with_escaped_characters_is_detected(self):
        body = json.dumps({"keys": [{"value": "fixture-secret\"\\\n", "keyName": "key1"}]})
        self.assert_scan(self.recording(body), "storage account key value")

    def test_invalid_recording_json_fails_explicitly(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "recording.json"
            path.write_text("{", encoding="utf-8")
            with contextlib.redirect_stderr(io.StringIO()) as stderr:
                self.assertEqual(scan_recordings.scan(path), 1)
            self.assertIn("invalid recording JSON", stderr.getvalue())

    def test_command_exit_codes_for_serialized_secret_and_redaction(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "recording.json"
            for value, expected_code in (("fixture-secret", 1), ("[redacted]", 0)):
                with self.subTest(value=value):
                    path.write_text(
                        self.recording(f"https://example.invalid/blob?sv=fixture&sig={value}", "upper"),
                        encoding="utf-8",
                    )
                    result = subprocess.run(
                        [sys.executable, "-B", scan_recordings.__file__, str(path)],
                        capture_output=True,
                        text=True,
                        encoding="utf-8",
                    )
                    self.assertEqual(result.returncode, expected_code, result.stderr)
                    self.assertNotIn("fixture-secret", result.stderr)


if __name__ == "__main__":
    unittest.main()
