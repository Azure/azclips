#!/usr/bin/env python3

import os
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).with_name("run_benchmark.sh")


@unittest.skipIf(os.name == "nt", "run_benchmark.sh needs Bash and GNU timeout")
class RunBenchmarkTests(unittest.TestCase):
    def run_script(self, command, timeout="5s"):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        environment = {**os.environ, "BENCHMARK_ATTEMPT_TIMEOUT": timeout,
                       "BENCHMARK_MAX_ATTEMPTS": "2", "BENCHMARK_RETRY_WAIT": "0"}
        return subprocess.run(["bash", str(SCRIPT), "bash", "-c", command], cwd=temp.name,
                              env=environment, capture_output=True, text=True, timeout=60)

    def test_a_success_needs_one_attempt(self):
        result = self.run_script("exit 0")

        self.assertEqual(result.returncode, 0)
        self.assertEqual(result.stdout.count("Benchmark attempt"), 1)

    def test_a_failed_attempt_is_retried(self):
        result = self.run_script("if [[ -f tried ]]; then exit 0; fi; touch tried; exit 3")

        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertIn("Attempt 1 failed with exit code 3.", result.stdout)
        self.assertEqual(result.stdout.count("Benchmark attempt"), 2)

    def test_the_last_failure_is_returned(self):
        result = self.run_script("exit 3")

        self.assertEqual(result.returncode, 3)
        self.assertEqual(result.stdout.count("Benchmark attempt"), 2)

    def test_timeouts_are_retried(self):
        result = self.run_script("sleep 10", timeout="0.2s")

        self.assertEqual(result.returncode, 124)
        self.assertIn("Attempt 1 timed out after 0.2s.", result.stdout)
        self.assertIn("Attempt 2 timed out after 0.2s.", result.stdout)


if __name__ == "__main__":
    unittest.main()
