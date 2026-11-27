#!/usr/bin/env python3
"""Checks the names the Benchmark stage's YAML shares with the detection and collection scripts.

A rename on one side only would skip every benchmark leg, or leave its results uncollected,
while the stage stays green. The YAML is read as text, so only the standard library is needed.
"""

import json
import re
import unittest
from pathlib import Path

import collect_benchmark_results as collect
import detect_benchmark_changes as detect

REPO_ROOT = Path(__file__).resolve().parents[3]
SUITE = REPO_ROOT / ".azure-pipelines" / "templates" / "dist" / "jobs" / "benchmark-suite.yml"
LEG_TEMPLATE = "- template: ../steps/run-benchmark-leg.yml"
JOB = re.compile(r"\s*- job: (\S+)\s*\Z")
OUTPUT_REFERENCE = re.compile(r"dependencies\.(\w+)\.outputs\['(\w+)\.(\w+)'\]")
SET_OUTPUT = re.compile(r"##vso\[task\.setvariable variable=(\w+);isOutput=true\](.*)\Z")


def jobs(text: str) -> dict[str, list[str]]:
    """Each job's lines, from its '- job:' line up to the next job's."""
    found, current = {}, None
    for line in text.splitlines():
        match = JOB.match(line)
        if match:
            current = found.setdefault(match.group(1), [])
        elif current is not None:
            current.append(line)
    return found


def steps(job: list[str]) -> list[list[str]]:
    """The steps of a job, each as its stripped lines."""
    stripped = [line.strip() for line in job]
    if "steps:" not in stripped:
        return []
    found, indent = [], None
    for line in job[stripped.index("steps:") + 1:]:
        text = line.strip()
        if not text or text.startswith("#"):
            continue
        depth = len(line) - len(line.lstrip())
        indent = depth if indent is None else indent
        if depth < indent or (depth == indent and not text.startswith("- ")):
            break
        if depth == indent:
            found.append([])
        found[-1].append(text)
    return found


class StageContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.yaml = SUITE.read_text(encoding="utf-8")
        cls.extensions = sorted(set(detect.discover_extensions(REPO_ROOT).values()))

    def outputs(self, selection) -> dict[str, str]:
        """The output variables detect_benchmark_changes.py sets for a selection."""
        values = {}
        for line in detect.output_lines(selection, "a base"):
            match = SET_OUTPUT.match(line)
            self.assertIsNotNone(match, line)
            values[match.group(1)] = match.group(2)
        return values

    def test_the_yaml_reads_only_outputs_that_detection_sets(self):
        references = OUTPUT_REFERENCE.findall(self.yaml)
        names = self.outputs(detect.NOTHING)

        self.assertTrue(references)
        for job, step, name in references:
            with self.subTest(reference=f"{job}.outputs['{step}.{name}']"):
                self.assertEqual((job, step), ("DetectBenchmarks", "detect"))
                self.assertIn(name, names)

    def test_detection_runs_in_the_job_and_step_the_yaml_reads(self):
        suite = jobs(self.yaml)

        self.assertIn("DetectBenchmarks", suite)
        detection = [step for step in steps(suite["DetectBenchmarks"])
                     if any("detect_benchmark_changes.py" in line for line in step)]
        self.assertEqual(len(detection), 1)
        self.assertIn("name: detect", detection[0])

    def test_matrix_legs_set_the_variable_the_legs_read(self):
        self.assertIn("$(Extension)", self.yaml)
        for label, selection in (("repository", detect.select_all(detect.discover_extensions(REPO_ROOT))),
                                 ("placeholder", detect.NOTHING)):
            with self.subTest(label):
                matrix = json.loads(self.outputs(selection)["extensionMatrix"])
                self.assertTrue(matrix)
                for leg in matrix.values():
                    self.assertEqual(list(leg), ["Extension"])

    def test_leg_artifacts_are_the_ones_collection_reads(self):
        legs = [step for job in jobs(self.yaml).values() for step in steps(job) if step[0] == LEG_TEMPLATE]

        self.assertTrue(legs)
        self.assertTrue(self.extensions)
        for leg in legs:
            names = [line.removeprefix("artifact_name:").strip() for line in leg if line.startswith("artifact_name:")]
            self.assertEqual(len(names), 1, leg)
            for artifact in sorted({names[0].replace("$(Extension)", extension) for extension in self.extensions}):
                with self.subTest(artifact=artifact):
                    self.assertRegex(artifact, collect.LEG_ARTIFACT)


if __name__ == "__main__":
    unittest.main()
