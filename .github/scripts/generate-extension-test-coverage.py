#!/usr/bin/env python3
"""Generate a command-surface test coverage report for azclips extensions."""

from __future__ import annotations

import argparse
import datetime as dt
import html
import hashlib
import json
import os
import re
import subprocess
from collections import Counter
from dataclasses import asdict, dataclass, field, replace
from pathlib import Path

from extension_coverage_analysis import Analysis, ArmScenarioAnalysis, LocalScenarioAnalysis, parser_metadata, without_comments


PS_VERBS = (
    "Add",
    "Clear",
    "Connect",
    "Disconnect",
    "Get",
    "Invoke",
    "New",
    "Open",
    "Remove",
    "Restart",
    "Save",
    "Set",
    "Start",
    "Stop",
    "Test",
    "Undo",
    "Update",
)


@dataclass
class TestHit:
    name: str
    path: str
    confidence: str
    kind: str = "processor-behavior"
    method: str = ""
    reason: str = ""
    scenario_type: str = ""
    line: int = 0
    shared_from: str = ""
    call_path: list[str] = field(default_factory=list)
    assertion_status: str = "not-analyzed"


@dataclass
class UnmappedTest:
    tier: str
    name: str
    path: str
    evidence: str


@dataclass
class CommandCoverage:
    extension: str
    surface: str
    invocation_name: str
    processor_type: str = ""
    unit_tests: list[TestHit] = field(default_factory=list)
    scenario_tests: list[TestHit] = field(default_factory=list)
    script_scenario_tests: list[TestHit] = field(default_factory=list)
    benchmarks: list[TestHit] = field(default_factory=list)
    applicability: dict = field(default_factory=lambda: {
        "armScenario": "unassessed",
        "nonArmScenario": "unassessed",
        "reason": "Automatic source discovery does not establish ARM/non-ARM scenario requirements.",
        "evidence": [],
    })
    inferred_processor_tests: list[tuple[str, TestHit]] = field(default_factory=list)
    analysis: dict = field(default_factory=dict, repr=False)
    source_classes: dict[tuple[str, int], tuple[str, int]] = field(default_factory=dict, repr=False)

    def has_unit_behavior(self) -> bool:
        return any(hit.kind in (
            "processor-helper", "processor-behavior", "cli-execution",
            "ps-execution", "engine-execution",
        ) for hit in self.unit_tests)

    def dimension_hits(self) -> dict[str, list[tuple[str, TestHit]]]:
        hits = [
            (tier, hit) for tier, items in (
                ("unit", self.unit_tests), ("scenario", self.scenario_tests),
                ("script-scenario", self.script_scenario_tests),
            ) for hit in items
        ] + self.inferred_processor_tests
        kinds = {
            "registration": {"registration"},
            "processorHelper": {"processor-helper"},
            "processorBehavior": {"processor-behavior", "cli-execution", "ps-execution", "engine-execution"},
            "binding": {"binding", "cli-execution", "ps-execution"},
            "surfaceExecution": {"cli-execution" if self.surface == "CLI" else "ps-execution"},
            "engineExecution": {"engine-execution"},
            "processorSetup": {"processor-setup"},
            "surfaceReference": {"surface-reference", "script-candidate"},
        }
        return {name: [(tier, hit) for tier, hit in hits if hit.kind in accepted]
                for name, accepted in kinds.items()}

    def has_processor_behavior(self) -> bool:
        return bool(self.dimension_hits()["processorBehavior"])

    def display_name(self) -> str:
        if self.surface == "PowerShell" and "_" in self.invocation_name:
            return f"{self.invocation_name.split('_', 1)[0]} (internal route: {self.invocation_name})"
        return self.invocation_name

    def scenario_hits(self) -> list[TestHit]:
        return self.scenario_tests + self.script_scenario_tests

    def scenario_label(self, hits: list[TestHit]) -> str:
        return "yes" if hits else "N/A" if self.recording_not_applicable() else "Unknown"

    def recording_not_applicable(self) -> bool:
        return self.applicability["armScenario"] == "not-applicable"

    def execution_status(self) -> str:
        hits = self.unit_tests + self.scenario_hits()
        kind = "cli-execution" if self.surface == "CLI" else "ps-execution"
        if any(hit.kind == kind for hit in hits):
            return "mapped"
        return "no-mapped-signal"

    def score_maximum(self) -> None:
        return None

    def required_score(self) -> int:
        return int(self.has_processor_behavior())

    def notes(self) -> str:
        applicability = self.applicability
        notes = (
            f"ARM: {applicability['armScenario']}; non-ARM: {applicability['nonArmScenario']}. "
            f"{applicability['reason']} Execution: {self.execution_status()}."
        )
        if not self.has_processor_behavior():
            notes += " No mapped processor execution/behavior tests; registration, helpers and setup are separate."
        if self.inferred_processor_tests:
            notes += " Shared processor behavior is inferred from another command's execution tests; surface-specific branches remain unverified."
        notes += " Applicable score denominator: unassessed."
        return notes

    def score(self) -> int:
        return (
            (1 if self.has_unit_behavior() else 0)
            + (2 if self.scenario_tests or self.script_scenario_tests else 0)
            + (1 if self.benchmarks else 0)
        )

    def gaps(self) -> list[str]:
        gaps: list[str] = []
        if not self.processor_type or self.processor_type in self.analysis.get("ambiguousProcessorIdentities", []):
            gaps.append("unresolved-processor-identity")
        if not self.has_processor_behavior():
            gaps.append("no-mapped-processor-tests")
        if self.execution_status() != "mapped":
            gaps.append("no-mapped-cli-execution-tests" if self.surface == "CLI" else "no-mapped-ps-execution-tests")
        if not self.benchmarks:
            gaps.append("no-mapped-benchmark")
        return gaps

    def gap_assessments(self) -> dict[str, str]:
        return {
            gap: (
                "informational" if gap == "no-mapped-benchmark"
                else "unresolved-source-identity" if gap == "unresolved-processor-identity"
                else "unreviewed-no-mapped-signal"
            )
            for gap in self.gaps()
        }

    def findings(self) -> str:
        return "; ".join(f"{gap} ({assessment})" for gap, assessment in self.gap_assessments().items())

    def status(self) -> str:
        if not self.processor_type or self.processor_type in self.analysis.get("ambiguousProcessorIdentities", []):
            return "Unknown"
        if self.has_processor_behavior() and self.execution_status() == "mapped":
            return "Good"
        if self.has_processor_behavior() or self.unit_tests or self.scenario_hits():
            return "Partial"
        return "Gap"


def repo_relative(repo: Path, path: Path) -> str:
    return path.relative_to(repo).as_posix()


def get_git_sha(repo: Path) -> str:
    try:
        return subprocess.check_output(
            ["git", "-C", str(repo), "rev-parse", "HEAD"],
            text=True,
            stderr=subprocess.DEVNULL,
        ).strip()
    except Exception:
        return ""


def iter_source_files(root: Path, pattern: str) -> list[Path]:
    return [
        p
        for p in root.glob(pattern)
        if "bin" not in p.parts and "obj" not in p.parts
    ]


def surface_from_name(variable_name: str, invocation_name: str, current_region: str) -> str:
    if current_region:
        return current_region
    lower = variable_name.lower()
    if lower.startswith("ps") or lower.endswith("_ps") or re.match(rf"^({'|'.join(PS_VERBS)})-Az", invocation_name):
        return "PowerShell"
    return "CLI"


def namespace_for_type(extension_root: Path, simple_type_name: str) -> str:
    declaration = re.compile(rf"\b(?:class|record|struct)\s+{re.escape(simple_type_name)}\b")
    for path in iter_source_files(extension_root, "**/*.cs"):
        text = path.read_text(encoding="utf-8-sig", errors="ignore")
        if not declaration.search(text):
            continue

        namespace = re.search(r"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;", text)
        if not namespace:
            namespace = re.search(r"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*\{", text)
        if namespace:
            return f"{namespace.group(1)}.{simple_type_name}"
    return ""


def resolve_processor_type(extension: str, extension_root: Path, type_name: str) -> str:
    cleaned = type_name.strip().removeprefix("global::")
    if cleaned.startswith("Microsoft.Azure."):
        return cleaned

    simple_type_name = cleaned.rsplit(".", 1)[-1]
    declared = namespace_for_type(extension_root, simple_type_name)
    if declared:
        return declared

    return f"Microsoft.Azure.Azclips.Extensions.{extension}.{cleaned}"


def parse_metadata_declarations(extension: str, extension_root: Path, text: str) -> dict[str, CommandCoverage]:
    declarations: dict[str, CommandCoverage] = {}
    command_pattern = re.compile(
        r"CommandMetadata\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*new\((?P<body>.*?)\n\s*\);",
        re.DOTALL,
    )
    constants = {
        match.group("name"): match.group("value")
        for match in re.finditer(
            r"private\s+const\s+string\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*\"(?P<value>[^\"]*)\"",
            text,
        )
    }
    region_markers: list[tuple[int, str]] = []
    for match in re.finditer(r"#region\s+(CLI|PS|PowerShell)\b", text, re.IGNORECASE):
        marker = match.group(1).lower()
        region_markers.append((match.start(), "CLI" if marker == "cli" else "PowerShell"))

    def current_region(position: int) -> str:
        region = ""
        for marker_position, marker_surface in region_markers:
            if marker_position <= position:
                region = marker_surface
            else:
                break
        return region

    for match in command_pattern.finditer(text):
        body = match.group("body")
        invocation = re.search(r"InvocationName:\s*(?:\$?\"(?P<string>[^\"]+)\"|(?P<name>nameof\([^)]+\)))", body)
        if not invocation:
            continue
        invocation_name = invocation.group("string")
        if not invocation_name:
            continue
        for name, value in constants.items():
            invocation_name = invocation_name.replace("{" + name + "}", value)

        full_name_match = re.search(
            r"FullName:\s*(?:\"(?P<string>[^\"]+)\"|typeof\((?P<typeof>[^)]+)\)\.FullName!?)",
            body,
        )
        processor_match = re.search(r"ProcessorType:\s*typeof\(([^)]+)\)", body)
        processor_type = ""
        if full_name_match:
            processor_type = full_name_match.group("string") or resolve_processor_type(
                extension,
                extension_root,
                full_name_match.group("typeof"),
            )
        if not processor_type and processor_match:
            processor_type = resolve_processor_type(extension, extension_root, processor_match.group(1))

        declarations[match.group("name")] = CommandCoverage(
            extension,
            surface_from_name(match.group("name"), invocation_name, current_region(match.start())),
            invocation_name,
            processor_type,
        )

    return declarations


def parse_table_surface(table_name: str) -> str:
    return "PowerShell" if "ps" in table_name.lower() else "CLI"


def parse_registered_commands(extension: str, text: str, declarations: dict[str, CommandCoverage]) -> list[CommandCoverage]:
    commands: list[CommandCoverage] = []

    for match in re.finditer(
        r"(?P<table>[A-Za-z_][A-Za-z0-9_]*Table)\s*=\s*(?:BuildCommandTable\((?P<args>.*?)\)|new\(\)\s*\{(?P<body>.*?)\n\s*\});",
        text,
        re.IGNORECASE | re.DOTALL,
    ):
        surface = parse_table_surface(match.group("table"))
        if match.group("args"):
            for name in re.findall(r"\b[A-Za-z_][A-Za-z0-9_]*\b", match.group("args")):
                if name in declarations:
                    command = declarations[name]
                    commands.append(CommandCoverage(extension, surface, command.invocation_name, command.processor_type))
                else:
                    raise ValueError(f"Unresolved registration in {extension}: {name}")
        if match.group("body"):
            entries = list(re.finditer(r'\["(?P<key>[^"]+)"\]\s*=\s*(?P<name>[A-Za-z_][A-Za-z0-9_]*)', match.group("body")))
            if len(entries) != len(re.findall(r"\[[^\]\r\n]+\]\s*=", match.group("body"))):
                raise ValueError(f"Unsupported registration key in {extension}: {match.group('table')}")
            for entry in entries:
                name = entry.group("name")
                if name in declarations:
                    command = declarations[name]
                    commands.append(CommandCoverage(extension, surface, entry.group("key"), command.processor_type))
                else:
                    raise ValueError(f"Unresolved registration in {extension}: {name}")

    seen: set[tuple[str, str]] = set()
    registered: list[CommandCoverage] = []
    for command in commands:
        key = (command.surface, command.invocation_name)
        if key in seen:
            raise ValueError(f"Duplicate registration in {extension}: {key}")
        seen.add(key)
        registered.append(command)

    return registered


def parse_commands(repo: Path, source_index: Analysis | None = None) -> list[CommandCoverage]:
    commands: list[CommandCoverage] = []
    factories = source_index
    extension_indexes = sorted((repo / "src" / "Extensions").glob("*/ExtensionIndex.cs"))

    for index_path in extension_indexes:
        extension = index_path.parent.name
        text = without_comments(index_path.read_text(encoding="utf-8-sig"))
        declarations = parse_metadata_declarations(extension, index_path.parent, text)
        metadata_fields = set(re.findall(r"\bCommandMetadata\s+([A-Za-z_][A-Za-z0-9_]*)\s*=", text))
        if metadata_fields - declarations.keys():
            factories = factories or Analysis(repo, [])
            for name, (invocation, processor) in factories.factory_metadata(repo_relative(repo, index_path)).items():
                declarations.setdefault(name, CommandCoverage(
                    extension, surface_from_name(name, invocation, ""), invocation, processor,
                ))
        registered = parse_registered_commands(extension, text, declarations)
        if re.search(r"\bvoid\s+\w+<\w+>\s*\(", text):
            factories = factories or Analysis(repo, [])
            factories.registrations = []
            diagnostic_start = len(factories.diagnostics)
            discovered = factories.factory_commands(repo_relative(repo, index_path))
            if any(d["reason"] == "unresolved-registration-invocation" for d in factories.diagnostics[diagnostic_start:]):
                raise ValueError(f"Unresolved command invocation in table factory: {index_path}")
            if not discovered:
                raise ValueError(f"No registrations resolved from table factory: {index_path}")
            registered.extend(CommandCoverage(extension, surface, name, processor)
                              for surface, name, processor in discovered)
        if not registered:
            raise ValueError(f"No registered commands resolved in {index_path}")
        commands.extend(registered)

    keys = [(c.extension, c.surface, c.invocation_name.lower()) for c in commands]
    if len(keys) != len(set(keys)):
        raise ValueError("Duplicate registered command identities in ExtensionIndex inventory.")
    return commands


def command_key(surface: str, invocation_name: str) -> str:
    return f"{surface}:{invocation_name.lower()}"


def resolve_invocation(invocation: str, commands: dict[str, CommandCoverage]) -> CommandCoverage | None:
    candidate = invocation
    while candidate not in commands:
        if not candidate.startswith("CLI:") or " " not in candidate:
            return None
        candidate = candidate.rsplit(" ", 1)[0]
    return commands[candidate]


def parse_cli_args(strings: list[str]) -> str | None:
    parts: list[str] = []
    for value in strings:
        if value.startswith("-"):
            break
        parts.append(value)
    return " ".join(parts) if parts else None


def csharp_string_literals(value: str) -> list[str]:
    return re.findall(r'"([^"]+)"', value)


def csharp_cli_helpers(text: str) -> dict[str, str]:
    helpers: dict[str, str] = {}

    for match in re.finditer(
        r"\b(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^)]*\)\s*=>\s*(?P<body>.*?);",
        text,
        re.DOTALL,
    ):
        command = parse_cli_args(csharp_string_literals(match.group("body")))
        if command:
            helpers[match.group("name")] = command

    for match in re.finditer(
        r"\b(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:new\[\]\s*)?(?:\[|\{)(?P<body>.*?)(?:\]|\});",
        text,
        re.DOTALL,
    ):
        command = parse_cli_args(csharp_string_literals(match.group("body")))
        if command:
            helpers[match.group("name")] = command

    return helpers


def invocations_from_csharp(text: str) -> tuple[set[str], list[str]]:
    """Compatibility lexical extractor; never used to award production evidence."""
    invocations: set[str] = set()
    unresolved: list[str] = []
    helpers = csharp_cli_helpers(text)
    for match in re.finditer(
        r"(?P<prefix>\b[A-Za-z_][A-Za-z0-9_]*\.)?\b(?P<name>(?:Run|Execute)Cli(?:Async)?)\s*\((?P<args>.*?)\)",
        text,
        re.DOTALL,
    ):
        args = match.group("args")
        name = match.group("name")
        if re.match(r"\s*(?:params\b|(?:string(?:\s*\[\s*\])?|ScenarioHost|ClientType|IReadOnlyList<[^>]+>)\s+\w+)", args):
            continue
        strings = csharp_string_literals(args)
        command = parse_cli_args(strings)
        if command:
            invocations.add(command_key("CLI", command))
            continue

        helper_match = re.search(r"\b(?P<helper>[A-Za-z_][A-Za-z0-9_]*)\s*(?:\(|\b)", args)
        helper = helper_match.group("helper") if helper_match else ""
        if helper in helpers:
            invocations.add(command_key("CLI", helpers[helper]))
        elif not match.group("prefix") and name in helpers:
            invocations.add(command_key("CLI", helpers[name]))
        else:
            unresolved.append(f"{name}({args.strip()})")

    for match in re.finditer(r'RunPowerShell(?:Command)?\(\s*"(?P<command>[^"]+)"', text):
        invocations.add(command_key("PowerShell", match.group("command")))

    for match in re.finditer(r'AddCommand\(\s*"(?P<command>[^"]+)"', text):
        invocations.add(command_key("PowerShell", match.group("command")))

    return invocations, unresolved


def invocations_from_script(path: Path, text: str) -> set[str]:
    invocations: set[str] = set()
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue

        if path.suffix == ".sh":
            match = re.search(r'(?:\baz(?:\.exe)?|"\$az")\s+(?P<args>.+)', stripped)
            if match:
                tokens = [t for t in re.split(r"\s+", match.group("args")) if t]
                command = parse_cli_args(tokens)
                if command:
                    invocations.add(command_key("CLI", command))

        if path.suffix == ".ps1":
            cli_match = re.search(r"&\s+\$az\s+(?P<args>.+)", stripped)
            if cli_match:
                tokens = [t for t in re.split(r"\s+", cli_match.group("args")) if t]
                command = parse_cli_args(tokens)
                if command:
                    invocations.add(command_key("CLI", command))

            for verb in PS_VERBS:
                for match in re.finditer(rf"\b{verb}-Az[A-Za-z0-9]+(?:\b|_)", stripped):
                    invocations.add(command_key("PowerShell", match.group(0)))

    return invocations


def command_test_name(path: Path) -> str:
    return path.stem


def add_test_hit(
    target: list[TestHit], repo: Path, path: Path, confidence: str,
    surface: str = "", scenario_type: str = "", kind: str = "",
) -> None:
    rel = repo_relative(repo, path)
    kind = kind or (
        ("cli-execution" if surface == "CLI" else "ps-execution")
        if confidence in ("exact-invocation", "script-invocation")
        else "benchmark" if confidence == "extension-benchmark"
        else "processor-setup"
    )
    if all((existing.path, existing.kind) != (rel, kind) for existing in target):
        target.append(TestHit(
            command_test_name(path), rel, confidence, kind, scenario_type=scenario_type
        ))


def benchmark_inventory(repo: Path) -> dict[str, dict[str, list[TestHit]]]:
    inventory: dict[str, dict[str, list[TestHit]]] = {}
    for path in iter_source_files(repo / "tests" / "Benchmark", "**/*.cs"):
        text = path.read_text(encoding="utf-8-sig", errors="ignore")
        attr = re.search(r'BenchmarkExtension\("(?P<extension>[^"]+)"\)', text)
        if not attr:
            continue

        if "CLI" in path.parts:
            surface = "CLI"
        elif "PS" in path.parts:
            surface = "PowerShell"
        else:
            surface = "Unknown"

        extension = attr.group("extension")
        inventory.setdefault(extension, {}).setdefault(surface, [])
        add_test_hit(inventory[extension][surface], repo, path, "extension-benchmark")
    return inventory


def map_tests(repo: Path, commands: list[CommandCoverage], source_index: Analysis | None = None) -> list[UnmappedTest]:
    analysis = Analysis(repo, commands, source_index).run()
    by_invocation = {command_key(c.surface, c.invocation_name): c for c in commands}
    unmapped: dict[tuple[str, str, str], UnmappedTest] = {}

    details = {
        "engine": "Python bounded source call paths",
        "csharpSourceSha256": analysis.source_digest.hexdigest(),
        "scriptSourceSha256": hashlib.sha256(b"".join(
            path.encode() + b"\0" + (repo / path).read_bytes()
            for path in sorted({caller["path"] for caller in analysis.script_calls})
        )).hexdigest(),
        "toolingSha256": hashlib.sha256(
            Path(__file__).read_bytes() + Path(__file__).with_name("extension_coverage_analysis.py").read_bytes()
        ).hexdigest(),
        "parserVersions": parser_metadata(),
        "unsupportedSyntax": [
            {"path": unit["path"], "constructs": len(unit["tree"].root_node.parse_issues),
             "examples": unit["tree"].root_node.parse_issues[:3]}
            for unit in analysis.units if unit["tree"].root_node.parse_issues
        ],
        "sourceFiles": len(analysis.units),
        "testSourceFiles": sum(u["path"].startswith("tests/") for u in analysis.units),
        "testRoots": len(analysis.roots),
        "mappedRoots": sum(r["mappedSignals"] > 0 for r in analysis.roots),
        "ambiguousProcessorIdentities": sorted(
            owner for owner in analysis.by_processor
            if len({d["unit"]["project"] for d in analysis.type_declarations.get(owner, [])}) > 1
        ),
        "diagnosticCounts": dict(sorted(Counter(d["reason"] for d in analysis.diagnostics).items())),
        "diagnostics": analysis.diagnostics,
        "roots": analysis.roots,
        "limitations": [
            "The Python source reader is bounded, not a complete C# parser or compiler; unsupported syntax stays opaque with diagnostic counts/examples.",
            "Source call paths are inferred; execution, passing outcomes and assertion quality are not established.",
            "Dynamic/member data, reflection, overloads and unresolved branches may remain unsupported; see diagnostics.",
            "Registration declarations and source-defined table factories are bounded, not arbitrary C# evaluation.",
            "Script contents supply literal candidate invocations only when a discovered test calls the playback runner.",
        ],
    }
    source_classes = {}
    for methods in analysis.methods.values():
        for method in methods:
            declaration = next((
                d for d in analysis.type_declarations.get(method.owner, [])
                if d["unit"] is method.unit
                and d["node"].start_byte <= method.node.start_byte < d["node"].end_byte
            ), None)
            if declaration:
                source_classes[(method.unit["path"], method.node.start_point.row + 1)] = (
                    method.owner.rsplit(".", 1)[-1].split("`")[0],
                    declaration["node"].start_point.row + 1,
                )
    for command in commands:
        command.analysis = details
        command.source_classes = source_classes
    applicability = LocalScenarioAnalysis(analysis)
    by_processor = {owner: applicability.infer(owner) for owner in analysis.by_processor}
    arm_applicability = ArmScenarioAnalysis(analysis)
    for owner, result in by_processor.items():
        arm = arm_applicability.infer(owner, result)
        result["nonArmAssessment"] = {
            key: result[key] for key in ("reason", "evidence", "diagnostics")
        }
        result["nonArmAssessment"]["status"] = result["nonArmScenario"]
        result["armScenario"] = arm["status"]
        result["armAssessment"] = arm
        result["scope"] = "arm-record-playback"
        for key in ("reason", "evidence", "diagnostics"):
            result[key] = arm[key]
    for command in commands:
        command.applicability = by_processor[command.processor_type]

    for event in analysis.hits:
        target = getattr(event["command"], {
            "unit": "unit_tests", "scenario": "scenario_tests", "benchmark": "benchmarks",
        }[event["tier"]])
        hit = TestHit(
            Path(event["path"]).stem, event["path"], "source-call-path", event["kind"],
            event["method"], event["reason"], line=event["line"],
            call_path=event["callPath"], assertion_status=event["assertionStatus"],
        )
        if not any((h.path, h.method, h.kind) == (hit.path, hit.method, hit.kind) for h in target):
            target.append(hit)

    for caller in analysis.script_calls:
        script_path = repo / caller["path"]
        for invocation in invocations_from_script(script_path, script_path.read_text(encoding="utf-8-sig")):
            command = resolve_invocation(invocation, by_invocation)
            if command:
                hit = TestHit(
                    script_path.stem, caller["path"], "source-script-candidate",
                    "script-candidate",
                    caller["method"],
                    "Playback test reaches this script; literal script invocation is inferred, not shell control-flow proof.",
                    line=next((i for i, line in enumerate(script_path.read_text(encoding="utf-8-sig").splitlines(), 1)
                               if invocation in invocations_from_script(script_path, line)), 0),
                    call_path=[f"{caller['rootPath']}::{caller['method']}:{caller['line']}"],
                )
                if not any((h.path, h.method, h.kind) == (hit.path, hit.method, hit.kind)
                           for h in command.script_scenario_tests):
                    command.script_scenario_tests.append(hit)
            else:
                key = ("script-scenario", caller["path"], invocation)
                unmapped[key] = UnmappedTest("script-scenario", script_path.stem, caller["path"], invocation)

    for diagnostic in analysis.diagnostics:
        path = diagnostic["path"]
        if not path.startswith("tests/"):
            continue
        tier = ("scenario" if path.startswith("tests/Scenario.Tests/") else
                "benchmark" if path.startswith("tests/Benchmark/") else "unit")
        evidence = diagnostic.get("detail", diagnostic["reason"])
        key = (tier, path, evidence)
        unmapped[key] = UnmappedTest(tier, Path(path).stem, path, evidence)

    infer_shared_processor_behavior(commands)
    return sorted(unmapped.values(), key=lambda item: (item.tier, item.path, item.evidence))


def infer_shared_processor_behavior(commands: list[CommandCoverage]) -> None:
    by_processor: dict[str, list[CommandCoverage]] = {}
    for command in commands:
        command.inferred_processor_tests.clear()
        if command.processor_type and command.processor_type not in command.analysis.get("ambiguousProcessorIdentities", []):
            by_processor.setdefault(command.processor_type, []).append(command)

    for related in by_processor.values():
        for source in related:
            for tier, items in (
                ("unit", source.unit_tests), ("scenario", source.scenario_tests),
                ("script-scenario", source.script_scenario_tests),
            ):
                for hit in sorted(items, key=lambda item: not bool(item.method)):
                    if hit.kind not in ("cli-execution", "ps-execution", "engine-execution"):
                        continue
                    for target in related:
                        if target is source:
                            continue
                        if any(
                            existing.path == hit.path and existing.method == hit.method and existing_tier == tier
                            for existing_tier, existing in target.dimension_hits()["processorBehavior"]
                        ):
                            continue
                        target.inferred_processor_tests.append((tier, replace(
                            hit, kind="processor-behavior", confidence="inferred-shared-processor",
                            scenario_type="", shared_from=f"{source.surface}:{source.invocation_name}",
                            reason=(
                                f"Inferred from {source.extension} {source.surface} "
                                f"'{source.invocation_name}' execution evidence using the same full "
                                f"processor identity ({source.processor_type}). Not an independent "
                                "test of this command's binding, execution or surface-specific branches."
                            ),
                        )))


def as_json(
    commands: list[CommandCoverage],
    commit: str,
    extension_benchmarks: dict[str, dict[str, list[TestHit]]],
    unmapped_tests: list[UnmappedTest],
) -> dict:
    extensions: dict[str, list[CommandCoverage]] = {}
    for command in commands:
        extensions.setdefault(command.extension, []).append(command)

    def hit_json(hit: TestHit) -> dict:
        result = asdict(hit)
        result["scenarioType"] = result.pop("scenario_type")
        result["sharedFrom"] = result.pop("shared_from")
        result["callPath"] = result.pop("call_path")
        result["assertionStatus"] = result.pop("assertion_status")
        result["sourceUrl"] = evidence_url(hit, commit)
        return result

    def unmapped_json(test: UnmappedTest) -> dict:
        return {"tier": test.tier, "name": test.name, "path": test.path, "evidence": test.evidence}

    return {
        "schemaVersion": 2,
        "coverageModel": "mapped-test-signals",
        "analysisMode": "automatic-discovery-only",
        "advisory": True,
        "testsExecuted": False,
        "analysis": commands[0].analysis if commands else {},
        "guidance": REPORT_GUIDANCE,
        "generatedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
        "commit": commit,
        "extensions": [
            {
                "name": extension,
                "summary": command_summary(extension_commands),
                "benchmarks": {
                    surface: [hit_json(hit) for hit in hits]
                    for surface, hits in sorted(extension_benchmarks.get(extension, {}).items())
                },
                "commands": [
                    {
                        "surface": command.surface,
                        "invocationName": command.invocation_name,
                        "displayName": command.display_name(),
                        "processorType": command.processor_type,
                        "unitTests": [hit_json(hit) for hit in command.unit_tests],
                        "scenarioTests": [hit_json(hit) for hit in command.scenario_tests],
                        "scriptScenarioTests": [hit_json(hit) for hit in command.script_scenario_tests],
                        "benchmarks": [hit_json(hit) for hit in command.benchmarks],
                        "sharedProcessorTests": [
                            {"tier": tier, **hit_json(hit)}
                            for tier, hit in command.inferred_processor_tests
                        ],
                        "coverageScore": command.score(),
                        "requiredScore": command.required_score(),
                        "applicableScoreMaximum": command.score_maximum(),
                        "tierApplicability": command.applicability,
                        "scenarioLabel": command.scenario_label(command.scenario_tests),
                        "scriptScenarioLabel": command.scenario_label(command.script_scenario_tests),
                        "unitBehaviorMapped": command.has_unit_behavior(),
                        "unitEvidence": unit_test_evidence(command),
                        "evidenceDimensions": {
                            name: {
                                "status": dimension_label(command, name),
                                "evidence": [{"tier": tier, **hit_json(hit)} for tier, hit in hits],
                            }
                            for name, hits in command.dimension_hits().items()
                        },
                        "executionCoverage": {
                            "status": command.execution_status(),
                            "reason": "Automatically mapped source signals; absence is unreviewed, not a confirmed gap.",
                            "reviewReason": "",
                            "reviewEvidence": [],
                            "testingTarget": "",
                        },
                        "notes": command.notes(),
                        "status": command.status(),
                        "gaps": command.gaps(),
                        "gapAssessments": command.gap_assessments(),
                    }
                    for command in sorted(extension_commands, key=lambda c: (c.surface, c.invocation_name))
                ],
            }
            for extension, extension_commands in sorted(extensions.items())
        ],
        "unmappedTests": [unmapped_json(test) for test in unmapped_tests],
    }


def yes_no(value: bool) -> str:
    return "yes" if value else "no"


def dimension_label(command: CommandCoverage, name: str) -> str:
    if name == "surfaceExecution":
        return command.execution_status()
    hits = command.dimension_hits()[name]
    if hits and all(hit.confidence == "inferred-shared-processor" for _, hit in hits):
        return "inferred-shared"
    return "mapped" if hits else "no-mapped-signal"


REPORT_GUIDANCE = (
    "Discovery is not proof tests passed: this report does not execute tests. "
    "For test tiers, no or Gap means no mapped test signal was found, "
    "not proof that no tests exist. This automatic-only report is advisory. "
    "ARM record/playback applicability uses bounded source-effect inference, independently of non-ARM traffic; "
    "unresolved ARM-capable dependencies remain unassessed. Mapped scenario tests do not prove ARM recordings. "
    "Good means processor and own-surface execution signals are mapped, not complete coverage or passing tests. "
    "Partial means some non-benchmark evidence exists but those dimensions are incomplete; "
    "Unknown means processor identity could not be resolved. "
    "Registration, processor behavior, helpers, binding and surface execution are distinct; "
    "tier/project does not establish evidence depth. Direct PowerShell engine calls bypass "
    "the cmdlet/binder; processor setup and AddCommand references alone prove neither behavior nor execution. "
    "All no-mapped-signal findings are unreviewed, not confirmed missing tests. "
    "Inferred shared processor behavior uses execution evidence for the same full processor identity; "
    "it does not grant binding, surface execution or scenario credit to another command. "
    "Benchmarks are informational and do not gate Good. Raw coverageScore preserves "
    "real signal points (including benchmarks); the applicable score denominator is unknown. "
)

HUMAN_REPORT_GUIDANCE = (
    "Source discovery, not test results or comprehensive quality: unmapped signals are unreviewed, "
    "not confirmed absent tests. Not found means no mapped evidence. Full evidence and discovery limitations are in JSON/data.js."
)

REPORT_LEGEND = (
    ("Registration", "Checks the command's registered processor and parameter definitions."),
    ("Processor behavior", "Tests the command's underlying logic, which CLI and PowerShell may share."),
    ("Helper behavior", "Tests supporting functions, such as building a URL, not the full command."),
    ("Binding", "Checks that argument values populate the expected processor properties."),
    ("Surface execution", "Invokes the command through CLI dispatch or an actual PowerShell cmdlet, not just the shared engine."),
    ("ARM scenarios", "ARM request record/playback applicability only; Unknown means not determined. Non-ARM traffic is separate."),
    ("Scenario / script scenario", "Mapped scenario tests are retained independently of ARM applicability; scripts are candidates, not verified recordings."),
    ("Bench", "Measures a command workload's performance; informational, not required for Good."),
    ("Unit test evidence", "Unit tests includes registration/helper tests. Nested areas show Found / Not found for mapped unit-tier signals, not code coverage, passing results or overall Good status."),
    ("Status", "Good: processor behavior and own-surface execution mapped. Partial: other mapped evidence, but those dimensions are incomplete. Gap: resolved processor without mapped unit/scenario or processor-behavior signals. Unknown: unresolved processor identity."),
)


def unit_evidence_checklist(command: CommandCoverage) -> dict[str, bool]:
    execution_kind = "cli-execution" if command.surface == "CLI" else "ps-execution"
    return {
        "processor": any(tier == "unit" for tier, _ in command.dimension_hits()["processorBehavior"]),
        "binding": any(hit.kind in ("binding", execution_kind) for hit in command.unit_tests),
        "execution": any(hit.kind == execution_kind for hit in command.unit_tests),
    }


def unit_test_evidence(command: CommandCoverage) -> dict:
    areas = unit_evidence_checklist(command)
    found = sum(areas.values())
    return {
        "found": bool(command.unit_tests or any(tier == "unit" for tier, _ in command.inferred_processor_tests)),
        "areas": areas, "mappedAreas": found, "totalAreas": 3,
        "status": "OK" if found >= 2 else "Partial" if found else "Not mapped",
    }


def test_evidence_summary(command: CommandCoverage, format: str = "text") -> str:
    unit = unit_test_evidence(command)
    areas = unit["areas"]
    scenarios = {hit.path for hit in command.scenario_hits()}
    parts = [
        "Unit tests: Found" if unit["found"] else "Unit tests: Not found",
        "Unit test evidence:",
        "Benchmarks: Found" if command.benchmarks else "Benchmarks: Not found",
        "ARM scenarios: N/A" if command.recording_not_applicable() else
        "ARM scenarios: Applicable" if command.applicability["armScenario"] == "applicable" else "ARM scenarios: Unknown",
    ]
    if scenarios:
        parts.append(f"Scenario tests: Found ({len(scenarios)} files)")
    area_labels = [
        f"{label}: {'Found' if areas[key] else 'Not found'}"
        for key, label in (("processor", "Processor"), ("binding", "Param Binding"), ("execution", "Command execution"))
    ]
    if format == "html":
        nested = "<ul>" + "".join(f"<li>{html.escape(area)}</li>" for area in area_labels) + "</ul>"
        return '<ul class="test-evidence">' + "".join(
            f"<li>{html.escape(part)}{nested if index == 1 else ''}</li>"
            for index, part in enumerate(parts)
        ) + "</ul>"
    if format == "markdown":
        lines = [f"\u2022 {markdown_cell(part)}" for part in parts]
        lines[2:2] = [f"&nbsp;&nbsp;&nbsp;&nbsp;\u2022 {markdown_cell(area)}" for area in area_labels]
        return "<br>".join(lines)
    lines = [f"- {part}" for part in parts]
    lines[2:2] = [f"    - {area}" for area in area_labels]
    return "\n".join(lines)


def command_summary(commands: list[CommandCoverage]) -> dict:
    return {
        **{status: sum(c.status() == status for c in commands)
           for status in ("Good", "Partial", "Gap", "Unknown")},
        "scenarioGaps": 0,
        "scenarioUnassessed": sum(c.applicability["armScenario"] == "unassessed" for c in commands),
        "reviewedExecutionGaps": 0,
        "noMappedProcessorSignals": sum(not c.has_processor_behavior() for c in commands),
        "noMappedExecutionSignals": sum(c.execution_status() != "mapped" for c in commands),
    }


def evidence_url(hit: TestHit, commit: str) -> str:
    return (
        f"https://github.com/Azure/azclips/blob/{commit}/{hit.path}"
        + (f"#L{hit.line}" if hit.line else "")
    )


def evidence_description(command: CommandCoverage, commit: str = "", format: str = "text") -> str:
    evidence = [
        *command.unit_tests, *command.scenario_tests, *command.script_scenario_tests,
        *command.benchmarks, *(hit for _, hit in command.inferred_processor_tests),
    ]
    classes = {}
    for hit in evidence:
        label, line = command.source_classes.get((hit.path, hit.line), (Path(hit.path).stem, 0))
        classes[(hit.path, line)] = (label, evidence_url(replace(hit, line=line), commit))
    descriptions = []
    for _, (label, url) in sorted(classes.items()):
        if format == "html":
            descriptions.append(f'<li><a href="{html.escape(url, quote=True)}">{html.escape(label)}</a></li>')
        elif format == "markdown":
            descriptions.append(f"\u2022 [{markdown_cell(label)}]({url})")
        else:
            descriptions.append(label)
    if not descriptions:
        return "-"
    if format == "html":
        return "<ul>" + "\n".join(descriptions) + "</ul>"
    return ("<br>" if format == "markdown" else "\n").join(descriptions)


def priority_gaps(commands: list[CommandCoverage]) -> list[CommandCoverage]:
    return sorted(
        (c for c in commands if c.status() != "Good"),
        key=lambda c: (c.has_processor_behavior(), c.extension, c.surface, c.invocation_name),
    )


def highest_priority_gaps(commands: list[CommandCoverage]) -> list[CommandCoverage]:
    return [
        command for command in priority_gaps(commands)
        if not unit_test_evidence(command)["found"] and command.applicability["armScenario"] == "unassessed"
    ]


def benchmark_gap_summary(commands: list[CommandCoverage]) -> list[tuple[str, int, int, int]]:
    totals = Counter(command.extension for command in commands)
    missing = Counter(command.extension for command in commands if not command.benchmarks)
    return [
        (extension, totals[extension], totals[extension] - count, count)
        for extension, count in sorted(missing.items())
    ]


PRIORITY_FILTER_DESCRIPTION = "Only commands with no mapped unit tests and unknown ARM scenario applicability."
PRIORITY_EMPTY_STATE = "No commands match the unit-test and ARM-applicability filter."
BENCHMARK_GAP_DESCRIPTION = (
    "Commands without mapped benchmark tests, including Good commands. Informational; not confirmed absent tests."
)


def gap_view_scope(commands: list[CommandCoverage], unmapped_tests: list[UnmappedTest]) -> str:
    return (
        f"Details: showing {len(priority_gaps(commands))} of {len(commands)} commands with potential gaps. "
        "The summary includes all commands; Good command details are hidden."
    )


def markdown_cell(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ")


INDICATOR_LEGEND = (
    "\U0001f534 any Gap; \U0001f7e1 Partial/Unknown with no Gap; \U0001f7e2 all commands Good. "
    "Based on all commands in each extension, not test quality."
)


def extension_indicator(commands: list[CommandCoverage], extension: str) -> str:
    statuses = {c.status() for c in commands if c.extension == extension}
    return "\U0001f534" if "Gap" in statuses else "\U0001f7e1" if statuses != {"Good"} else "\U0001f7e2"


def extension_anchors(commands: list[CommandCoverage]) -> dict[str, str]:
    anchors, used = {}, set()
    for extension in sorted({c.extension for c in commands}):
        base = "extension-" + (re.sub(r"[^a-z0-9]+", "-", extension.casefold()).strip("-") or "unnamed")
        anchor, suffix = base, 2
        while anchor in used:
            anchor, suffix = f"{base}-{suffix}", suffix + 1
        anchors[extension] = anchor
        used.add(anchor)
    return anchors


def analysis_disclosure(commands: list[CommandCoverage]) -> str:
    analysis = commands[0].analysis if commands else {}
    if not analysis:
        return "Analysis completeness: source inference only; test execution and quality are not assessed."
    return (
        f"Analysis scope: {analysis['sourceFiles']} C# files, {analysis['testRoots']} attributed test/benchmark roots, "
        f"{analysis['mappedRoots']} roots with mapped signals. "
        f"{sum(analysis['diagnosticCounts'].values())} discovery diagnostics remain "
        "(including dynamic/data/conditional paths; details and root ledger in JSON/data.js). "
        "This is a bounded source-pattern audit, not exhaustive behavioral verification or evidence that tests passed."
    )


def as_markdown(
    commands: list[CommandCoverage],
    commit: str,
    extension_benchmarks: dict[str, dict[str, list[TestHit]]],
    unmapped_tests: list[UnmappedTest],
) -> str:
    extensions: dict[str, list[CommandCoverage]] = {}
    for command in commands:
        extensions.setdefault(command.extension, []).append(command)
    anchors = extension_anchors(commands)

    lines = [
        "# Extension test coverage",
        "",
        f"Generated from `{commit[:12] or 'unknown'}` on {dt.datetime.now(dt.timezone.utc).strftime('%Y-%m-%d %H:%M:%S UTC')}.",
        "",
        gap_view_scope(commands, unmapped_tests),
        "",
        "## About this report",
        "",
        HUMAN_REPORT_GUIDANCE,
        "",
        "| Area | Meaning |",
        "| --- | --- |",
        *(f"| {area} | {meaning} |" for area, meaning in REPORT_LEGEND),
        "",
        "[Full inventory and evidence (JSON)](extension-test-coverage.json)",
        "",
        "## Gap summary",
        "",
        INDICATOR_LEGEND,
        "",
        "| Extension | Commands | Good | Partial | Gap | Unknown |",
        "| --- | ---: | ---: | ---: | ---: | ---: |",
    ]

    for extension, extension_commands in sorted(extensions.items()):
        summary = command_summary(extension_commands)
        lines.append(
            f"| {extension_indicator(commands, extension)} [{markdown_cell(extension)}](#{anchors[extension]}) | {len(extension_commands)} | {summary['Good']} | {summary['Partial']} | {summary['Gap']} | {summary['Unknown']} |"
        )

    lines.extend(["", "## Benchmark gap summary", "", BENCHMARK_GAP_DESCRIPTION, ""])
    benchmark_gaps = benchmark_gap_summary(commands)
    if benchmark_gaps:
        lines.extend([
            "| Extension | Commands | Benchmarks found | Benchmarks not found |",
            "| --- | ---: | ---: | ---: |",
        ])
        for extension, total, found, missing in benchmark_gaps:
            lines.append(
                f"| [{markdown_cell(extension)}](#{anchors[extension]}) | {total} | {found} | {missing} |"
            )
    else:
        lines.append("No unmapped benchmark gaps identified.")

    gap_commands = highest_priority_gaps(commands)
    lines.extend(["", "## Highest priority mapped-signal gaps", "", PRIORITY_FILTER_DESCRIPTION, ""])
    if gap_commands:
        lines.extend(["| Extension | Surface | Command | Test evidence |", "| --- | --- | --- | --- |"])
        for command in gap_commands[:50]:
            lines.append(f"| {command.extension} | {command.surface} | `{command.display_name()}` | {test_evidence_summary(command, 'markdown')} |")
    else:
        lines.append(PRIORITY_EMPTY_STATE)

    for extension, extension_commands in sorted(extensions.items()):
        lines.extend(["", f'<a id="{anchors[extension]}"></a>', f"## {extension}", ""])
        extension_commands = [c for c in extension_commands if c.status() != "Good"]
        if not extension_commands:
            lines.append("No potential gaps identified in mapped dimensions.")
            continue
        cli_benchmarks = extension_benchmarks.get(extension, {}).get("CLI", [])
        ps_benchmarks = extension_benchmarks.get(extension, {}).get("PowerShell", [])
        lines.extend([
            f"Extension-level benchmarks: CLI={len(cli_benchmarks) or 'no'}, PowerShell={len(ps_benchmarks) or 'no'}.",
            "",
            "| Surface | Command | Registration | Processor behavior | Helper behavior | Binding | Surface execution | Scenario | Script scenario | Bench | Status | Evidence | Test evidence |",
            "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
        ])
        for command in sorted(extension_commands, key=lambda c: (c.surface, c.invocation_name)):
            lines.append(
                f"| {command.surface} | `{command.display_name()}` | "
                + "".join(f"{dimension_label(command, name)} | " for name in (
                    "registration", "processorBehavior", "processorHelper", "binding", "surfaceExecution",
                )) +
                f"{command.scenario_label(command.scenario_tests)} | "
                f"{command.scenario_label(command.script_scenario_tests)} | "
                f"{yes_no(bool(command.benchmarks))} | "
                f"{command.status()} | "
                f"{evidence_description(command, commit, 'markdown')} | {test_evidence_summary(command, 'markdown')} |"
            )

    lines.append("")
    return "\n".join(lines)


def as_html(
    commands: list[CommandCoverage],
    commit: str,
    extension_benchmarks: dict[str, dict[str, list[TestHit]]],
    unmapped_tests: list[UnmappedTest],
) -> str:
    extensions: dict[str, list[CommandCoverage]] = {}
    for command in commands:
        extensions.setdefault(command.extension, []).append(command)
    anchors = extension_anchors(commands)

    generated = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")

    def badge(status: str) -> str:
        css = {
            "Good": "good",
            "Partial": "partial",
            "Gap": "gap",
        }.get(status, "unknown")
        return f'<span class="badge {css}">{html.escape(status)}</span>'

    def yn(value: bool) -> str:
        return '<span class="yes">yes</span>' if value else '<span class="no">no</span>'

    rows = []
    for extension, extension_commands in sorted(extensions.items()):
        summary = command_summary(extension_commands)
        label = f'<a href="#{anchors[extension]}">{html.escape(extension)}</a>'
        rows.append(
            f"<tr><td>{extension_indicator(commands, extension)} {label}</td>"
            f"<td>{len(extension_commands)}</td><td>{summary['Good']}</td><td>{summary['Partial']}</td>"
            f"<td>{summary['Gap']}</td><td>{summary['Unknown']}</td></tr>"
        )

    extension_sections = []
    for extension, extension_commands in sorted(extensions.items()):
        extension_commands = [c for c in extension_commands if c.status() != "Good"]
        if not extension_commands:
            extension_sections.append(
                f'<section><h2 id="{anchors[extension]}">{html.escape(extension)}</h2>'
                "<p>No potential gaps identified in mapped dimensions.</p></section>"
            )
            continue
        cli_benchmarks = extension_benchmarks.get(extension, {}).get("CLI", [])
        ps_benchmarks = extension_benchmarks.get(extension, {}).get("PowerShell", [])
        command_rows = []
        for command in sorted(extension_commands, key=lambda c: (c.surface, c.invocation_name)):
            command_rows.append(
                "<tr>"
                f"<td>{html.escape(command.surface)}</td>"
                f"<td><code>{html.escape(command.display_name())}</code></td>"
                + "".join(f"<td>{dimension_label(command, name)}</td>" for name in (
                    "registration", "processorBehavior", "processorHelper", "binding", "surfaceExecution",
                )) +
                f"<td>{html.escape(command.scenario_label(command.scenario_tests))}</td>"
                f"<td>{html.escape(command.scenario_label(command.script_scenario_tests))}</td>"
                f"<td>{yn(bool(command.benchmarks))}</td>"
                f"<td>{badge(command.status())}</td>"
                f"<td>{evidence_description(command, commit, 'html')}</td>"
                f"<td>{test_evidence_summary(command, 'html')}</td>"
                "</tr>"
            )
        extension_sections.append(
            f"""
            <section>
              <h2 id="{anchors[extension]}">{html.escape(extension)}</h2>
              <p class="meta">Extension-level benchmarks: CLI={len(cli_benchmarks) or 'no'}, PowerShell={len(ps_benchmarks) or 'no'}.</p>
              <table>
                <thead>
                  <tr><th>Surface</th><th>Command</th><th>Registration</th><th>Processor behavior</th><th>Helper behavior</th><th>Binding</th><th>Surface execution</th><th>Scenario</th><th>Script scenario</th><th>Bench</th><th>Status</th><th>Evidence</th><th>Test evidence</th></tr>
                </thead>
                <tbody>{''.join(command_rows)}</tbody>
              </table>
            </section>
            """
        )

    benchmark_rows = [
        f'<tr><td><a href="#{anchors[extension]}">{html.escape(extension)}</a></td>'
        f"<td>{total}</td><td>{found}</td><td>{missing}</td></tr>"
        for extension, total, found, missing in benchmark_gap_summary(commands)
    ]
    gap_commands = highest_priority_gaps(commands)
    gap_rows = []
    for command in gap_commands[:50]:
        gap_rows.append(
            "<tr>"
            f"<td>{html.escape(command.extension)}</td>"
            f"<td>{html.escape(command.surface)}</td>"
            f"<td><code>{html.escape(command.display_name())}</code></td>"
            f"<td>{test_evidence_summary(command, 'html')}</td>"
            "</tr>"
        )

    return f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Azclips extension test coverage</title>
  <style>
    :root {{
      color-scheme: light dark;
      --bg: #0d1117;
      --panel: #161b22;
      --text: #e6edf3;
      --muted: #8b949e;
      --border: #30363d;
      --good: #238636;
      --partial: #9e6a03;
      --gap: #da3633;
      --link: #58a6ff;
    }}
    body {{ margin: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; background: var(--bg); color: var(--text); }}
    main {{ max-width: 1800px; margin: 0 auto; padding: 32px 20px 64px; }}
    section {{ overflow-x: auto; }}
    td {{ overflow-wrap: anywhere; }}
    h1 {{ margin-bottom: 4px; }}
    h2 {{ margin-top: 36px; }}
    a {{ color: var(--link); }}
    .meta {{ color: var(--muted); margin-top: 0; }}
    table {{ width: 100%; border-collapse: collapse; margin: 16px 0 24px; background: var(--panel); }}
    th, td {{ border: 1px solid var(--border); padding: 8px 10px; text-align: left; vertical-align: top; }}
    th {{ position: sticky; top: 0; background: #21262d; }}
    code {{ font-family: ui-monospace, SFMono-Regular, Consolas, monospace; }}
    .test-evidence, .test-evidence ul {{ margin: 0; padding-left: 1.2em; }}
    .badge {{ border-radius: 999px; padding: 2px 8px; font-size: 12px; font-weight: 600; color: white; }}
    .good {{ background: var(--good); }}
    .partial {{ background: var(--partial); }}
    .gap {{ background: var(--gap); }}
    .yes {{ color: #3fb950; font-weight: 600; }}
    .no {{ color: var(--muted); }}
    .unknown {{ color: var(--muted); font-style: italic; }}
  </style>
</head>
<body>
  <main>
    <h1>Azclips extension test coverage</h1>
    <p class="meta">Generated from <code>{html.escape(commit[:12] or 'unknown')}</code> on {html.escape(generated)}.</p>
    <p>{html.escape(gap_view_scope(commands, unmapped_tests))}</p>
    <h2>About this report</h2>
    <p class="meta">{html.escape(HUMAN_REPORT_GUIDANCE)}</p>
    <table>
      <thead><tr><th>Area</th><th>Meaning</th></tr></thead>
      <tbody>{''.join(f'<tr><td>{html.escape(area)}</td><td>{html.escape(meaning)}</td></tr>' for area, meaning in REPORT_LEGEND)}</tbody>
    </table>
    <p><a href="extension-test-coverage.md">Markdown</a> · <a href="extension-test-coverage.json">JSON</a></p>
    <h2>Gap summary</h2>
    <p>{html.escape(INDICATOR_LEGEND)}</p>
    <table>
      <thead><tr><th>Extension</th><th>Commands</th><th>Good</th><th>Partial</th><th>Gap</th><th>Unknown</th></tr></thead>
      <tbody>{''.join(rows)}</tbody>
    </table>
    <h2>Benchmark gap summary</h2>
    <p>{html.escape(BENCHMARK_GAP_DESCRIPTION)}</p>
    <table>
      <thead><tr><th>Extension</th><th>Commands</th><th>Benchmarks found</th><th>Benchmarks not found</th></tr></thead>
      <tbody>{''.join(benchmark_rows) if benchmark_rows else '<tr><td colspan="4">No unmapped benchmark gaps identified.</td></tr>'}</tbody>
    </table>
    <h2>Highest priority mapped-signal gaps</h2>
    <p>{html.escape(PRIORITY_FILTER_DESCRIPTION)}</p>
    <table>
      <thead><tr><th>Extension</th><th>Surface</th><th>Command</th><th>Test evidence</th></tr></thead>
      <tbody>{''.join(gap_rows) if gap_rows else f'<tr><td colspan="4">{html.escape(PRIORITY_EMPTY_STATE)}</td></tr>'}</tbody>
    </table>
    {''.join(extension_sections)}
  </main>
</body>
</html>
"""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".", help="Path to the azclips repository root.")
    parser.add_argument("--out", default="artifacts/TestCoverage", help="Output directory.")
    parser.add_argument("--commit", default="", help="Commit SHA to include in the report.")
    args = parser.parse_args()

    repo = Path(args.repo_root).resolve()
    out_dir = (repo / args.out).resolve() if not os.path.isabs(args.out) else Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    source_index = Analysis(repo, [])
    commands = parse_commands(repo, source_index)
    if not commands:
        parser.error("No registered commands discovered. Check --repo-root and ExtensionIndex metadata.")
    unmapped_tests = map_tests(repo, commands, source_index)
    extension_benchmarks = benchmark_inventory(repo)
    commit = args.commit or get_git_sha(repo)

    report_json = as_json(commands, commit, extension_benchmarks, unmapped_tests)
    (out_dir / "extension-test-coverage.json").write_text(json.dumps(report_json, indent=2), encoding="utf-8")
    (out_dir / "extension-test-coverage.md").write_text(
        as_markdown(commands, commit, extension_benchmarks, unmapped_tests),
        encoding="utf-8",
    )
    (out_dir / "index.html").write_text(as_html(commands, commit, extension_benchmarks, unmapped_tests), encoding="utf-8")
    (out_dir / "data.js").write_text(
        "window.EXTENSION_TEST_COVERAGE = " + json.dumps(report_json, indent=2) + ";\n",
        encoding="utf-8",
    )

    total = len(commands)
    gaps = sum(1 for command in commands if command.status() == "Gap")
    scenario_gaps = sum(1 for command in commands if "missing-scenario" in command.gaps())
    print(f"Wrote {total} full-inventory command rows to {out_dir}")
    print(f"Human report potential-gap command rows (unreviewed): {len(priority_gaps(commands))}")
    print(f"Commands with no mapped non-benchmark evidence: {gaps}")
    print(f"Confirmed scenario gaps: {scenario_gaps} (scenario requirements are not imposed)")
    print(f"Commands with unknown ARM scenario applicability: {command_summary(commands)['scenarioUnassessed']}")
    print(f"Unmapped test evidence rows: {len(unmapped_tests)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
