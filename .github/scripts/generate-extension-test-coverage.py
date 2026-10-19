#!/usr/bin/env python3
"""Generate a command-surface test coverage report for azclips extensions."""

from __future__ import annotations

import argparse
import datetime as dt
import html
import json
import os
import re
import subprocess
from dataclasses import dataclass, field
from pathlib import Path


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


@dataclass
class CommandCoverage:
    extension: str
    surface: str
    invocation_name: str
    processor_type: str = ""
    unit_tests: list[TestHit] = field(default_factory=list)
    scenario_tests: list[TestHit] = field(default_factory=list)
    script_scenario_tests: list[TestHit] = field(default_factory=list)
    docs_present: bool = False
    docs_path: str = ""
    docs_tests: list[TestHit] = field(default_factory=list)
    benchmarks: list[TestHit] = field(default_factory=list)

    def score(self) -> int:
        return (
            (1 if self.unit_tests else 0)
            + (2 if self.scenario_tests or self.script_scenario_tests else 0)
            + (1 if self.benchmarks else 0)
        )

    def gaps(self) -> list[str]:
        gaps: list[str] = []
        if not self.unit_tests:
            gaps.append("missing-unit")
        if not self.scenario_tests and not self.script_scenario_tests:
            gaps.append("missing-scenario")
        if not self.benchmarks:
            gaps.append("missing-benchmark")
        return gaps

    def status(self) -> str:
        has_scenario = bool(self.scenario_tests or self.script_scenario_tests)
        if self.unit_tests and has_scenario:
            return "Good"
        if self.unit_tests or has_scenario or self.docs_tests:
            return "Partial"
        return "Gap"


def repo_relative(repo: Path, path: Path) -> str:
    return path.relative_to(repo).as_posix()


def normalize(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "", value.lower())


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


def parse_metadata_declarations(extension: str, text: str) -> dict[str, CommandCoverage]:
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

        full_name_match = re.search(r"FullName:\s*\"([^\"]+)\"", body)
        processor_match = re.search(r"ProcessorType:\s*typeof\(([^)]+)\)", body)
        processor_type = full_name_match.group(1) if full_name_match else ""
        if not processor_type and processor_match:
            processor_type = f"Microsoft.Azure.Azclips.Extensions.{extension}.{processor_match.group(1)}"

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
        if match.group("body"):
            for entry in re.finditer(r'\["(?P<key>[^"]+)"\]\s*=\s*(?P<name>[A-Za-z_][A-Za-z0-9_]*)', match.group("body")):
                name = entry.group("name")
                if name in declarations:
                    command = declarations[name]
                    commands.append(CommandCoverage(extension, surface, entry.group("key"), command.processor_type))

    # KeyVault builds its dispatch tables through local Add<T>("verb", "Ps-Name") calls.
    if "void Add<T>" in text:
        for add in re.finditer(r'Add<(?P<type>[^>]+)>\("(?P<cli>[^"]+)",\s*"(?P<ps>[^"]+)"', text):
            processor = f"Microsoft.Azure.Azclips.Extensions.{extension}.{add.group('type')}"
            if add.group("cli") != "recover":
                commands.append(CommandCoverage(extension, "CLI", f"keyvault secret {add.group('cli')}", processor))
            commands.append(CommandCoverage(extension, "PowerShell", add.group("ps"), processor))

    seen: set[tuple[str, str]] = set()
    registered: list[CommandCoverage] = []
    for command in commands:
        key = (command.surface, command.invocation_name)
        if key not in seen:
            seen.add(key)
            registered.append(command)

    return registered


def parse_commands(repo: Path) -> list[CommandCoverage]:
    commands: list[CommandCoverage] = []
    extension_indexes = sorted((repo / "src" / "Extensions").glob("*/ExtensionIndex.cs"))

    for index_path in extension_indexes:
        extension = index_path.parent.name
        text = index_path.read_text(encoding="utf-8-sig")
        declarations = parse_metadata_declarations(extension, text)
        commands.extend(parse_registered_commands(extension, text, declarations))

    return commands


def read_index(repo: Path) -> tuple[list[tuple[Path, str, str]], list[tuple[Path, str, str]], list[tuple[Path, str, str]]]:
    unit_files: list[tuple[Path, str, str]] = []
    scenario_files: list[tuple[Path, str, str]] = []
    benchmark_files: list[tuple[Path, str, str]] = []

    for path in iter_source_files(repo / "tests", "**/*.cs"):
        text = path.read_text(encoding="utf-8-sig", errors="ignore")
        norm = normalize(text)
        rel = repo_relative(repo, path)
        if "tests/Scenario.Tests/" in rel:
            scenario_files.append((path, text, norm))
        elif "tests/Benchmark/" in rel:
            benchmark_files.append((path, text, norm))
        elif "[Fact]" in text or "[Theory]" in text or "Fact(" in text or "Theory(" in text:
            unit_files.append((path, text, norm))

    return unit_files, scenario_files, benchmark_files


def command_tokens(command: CommandCoverage) -> set[str]:
    tokens = {normalize(command.invocation_name)}
    if command.processor_type:
        tokens.add(normalize(command.processor_type.rsplit(".", 1)[-1]))
    return {token for token in tokens if token}


def command_key(surface: str, invocation_name: str) -> str:
    return f"{surface}:{invocation_name.lower()}"


def parse_cli_args(strings: list[str]) -> str | None:
    parts: list[str] = []
    for value in strings:
        if value.startswith("-"):
            break
        parts.append(value)
    return " ".join(parts) if parts else None


def invocations_from_csharp(text: str) -> set[str]:
    invocations: set[str] = set()
    for match in re.finditer(r"RunCli\((?P<args>.*?)\)", text, re.DOTALL):
        strings = re.findall(r'"([^"]+)"', match.group("args"))
        command = parse_cli_args(strings)
        if command:
            invocations.add(command_key("CLI", command))

    for match in re.finditer(r'RunPowerShell(?:Command)?\(\s*"(?P<command>[^"]+)"', text):
        invocations.add(command_key("PowerShell", match.group("command")))

    for match in re.finditer(r'AddCommand\(\s*"(?P<command>[^"]+)"', text):
        invocations.add(command_key("PowerShell", match.group("command")))

    return invocations


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


def script_paths_from_wrapper(repo: Path, text: str) -> list[Path]:
    paths: list[Path] = []
    scripts_root = repo / "tests" / "Scenario.Tests" / "Scripts"
    for match in re.finditer(r'ScriptScenarioRunner\.RunPlayback\("(?P<script>[^"]+)"', text):
        path = scripts_root / match.group("script")
        if path.exists():
            paths.append(path)
    return paths


def command_test_name(path: Path) -> str:
    return path.stem


def add_test_hit(target: list[TestHit], repo: Path, path: Path, confidence: str) -> None:
    rel = repo_relative(repo, path)
    if all(existing.path != rel for existing in target):
        target.append(TestHit(command_test_name(path), rel, confidence))


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


def map_tests(repo: Path, commands: list[CommandCoverage]) -> None:
    unit_files, scenario_files, benchmark_files = read_index(repo)
    by_invocation = {
        command_key(command.surface, command.invocation_name): command
        for command in commands
    }

    for command in commands:
        docs_path = docs_file_for_command(repo, command)
        if docs_path and docs_path.exists():
            command.docs_present = True
            command.docs_path = repo_relative(repo, docs_path)

    for path, text, _norm in unit_files:
        for invocation in invocations_from_csharp(text):
            if invocation in by_invocation:
                add_test_hit(by_invocation[invocation].unit_tests, repo, path, "exact-invocation")

    for path, text, _norm in scenario_files:
        direct_invocations = invocations_from_csharp(text)
        for invocation in direct_invocations:
            if invocation in by_invocation:
                add_test_hit(by_invocation[invocation].scenario_tests, repo, path, "exact-invocation")

        for script_path in script_paths_from_wrapper(repo, text):
            script_text = script_path.read_text(encoding="utf-8-sig", errors="ignore")
            for invocation in invocations_from_script(script_path, script_text):
                if invocation in by_invocation:
                    add_test_hit(by_invocation[invocation].script_scenario_tests, repo, script_path, "script-invocation")

    for command in commands:
        tokens = command_tokens(command)
        for path, text, norm in benchmark_files:
            has_extension_attr = f'BenchmarkExtension("{command.extension}")' in text
            if has_extension_attr and command_key(command.surface, command.invocation_name) in invocations_from_csharp(text):
                add_test_hit(command.benchmarks, repo, path, "exact-invocation")
            elif has_extension_attr and "_" not in command.invocation_name and any(token and token in norm for token in tokens):
                add_test_hit(command.benchmarks, repo, path, "processor-token")


def docs_file_for_command(repo: Path, command: CommandCoverage) -> Path | None:
    if command.surface == "CLI":
        parts = command.invocation_name.split()
        if not parts:
            return None
        if len(parts) == 1:
            return repo / "src" / "CLI" / "Help" / "Docs" / parts[0] / "index.md"
        return repo / "src" / "CLI" / "Help" / "Docs" / Path(*parts[:-1]) / f"{parts[-1]}.md"

    base_name = command.invocation_name.split("_", 1)[0]
    return repo / "src" / "PS" / command.extension / "help" / f"{base_name}.md"


def as_json(commands: list[CommandCoverage], commit: str, extension_benchmarks: dict[str, dict[str, list[TestHit]]]) -> dict:
    extensions: dict[str, list[CommandCoverage]] = {}
    for command in commands:
        extensions.setdefault(command.extension, []).append(command)

    def hit_json(hit: TestHit) -> dict:
        return {"name": hit.name, "path": hit.path, "confidence": hit.confidence}

    return {
        "schemaVersion": 1,
        "generatedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
        "commit": commit,
        "extensions": [
            {
                "name": extension,
                "benchmarks": {
                    surface: [hit_json(hit) for hit in hits]
                    for surface, hits in sorted(extension_benchmarks.get(extension, {}).items())
                },
                "commands": [
                    {
                        "surface": command.surface,
                        "invocationName": command.invocation_name,
                        "processorType": command.processor_type,
                        "unitTests": [hit_json(hit) for hit in command.unit_tests],
                        "scenarioTests": [hit_json(hit) for hit in command.scenario_tests],
                        "scriptScenarioTests": [hit_json(hit) for hit in command.script_scenario_tests],
                        "docsPresent": command.docs_present,
                        "docsPath": command.docs_path,
                        "docsTests": [hit_json(hit) for hit in command.docs_tests],
                        "benchmarks": [hit_json(hit) for hit in command.benchmarks],
                        "coverageScore": command.score(),
                        "status": command.status(),
                        "gaps": command.gaps(),
                    }
                    for command in sorted(extension_commands, key=lambda c: (c.surface, c.invocation_name))
                ],
            }
            for extension, extension_commands in sorted(extensions.items())
        ],
        "unmappedTests": [],
    }


def yes_no(value: bool) -> str:
    return "yes" if value else "no"


def as_markdown(commands: list[CommandCoverage], commit: str, extension_benchmarks: dict[str, dict[str, list[TestHit]]]) -> str:
    extensions: dict[str, list[CommandCoverage]] = {}
    for command in commands:
        extensions.setdefault(command.extension, []).append(command)

    lines = [
        "# Extension test coverage",
        "",
        f"Generated from `{commit[:12] or 'unknown'}` on {dt.datetime.now(dt.timezone.utc).strftime('%Y-%m-%d %H:%M:%S UTC')}.",
        "",
        "## Summary",
        "",
        "| Extension | Commands | Good | Partial | Gaps | Scenario gaps | CLI bench | PS bench |",
        "| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |",
    ]

    for extension, extension_commands in sorted(extensions.items()):
        good = sum(1 for c in extension_commands if c.status() == "Good")
        partial = sum(1 for c in extension_commands if c.status() == "Partial")
        gaps = sum(1 for c in extension_commands if c.status() == "Gap")
        scenario_gaps = sum(1 for c in extension_commands if "missing-scenario" in c.gaps())
        cli_bench = len(extension_benchmarks.get(extension, {}).get("CLI", []))
        ps_bench = len(extension_benchmarks.get(extension, {}).get("PowerShell", []))
        lines.append(
            f"| {extension} | {len(extension_commands)} | {good} | {partial} | {gaps} | {scenario_gaps} | "
            f"{cli_bench or 'no'} | {ps_bench or 'no'} |"
        )

    gap_commands = [c for c in commands if c.status() == "Gap" or "missing-scenario" in c.gaps()]
    lines.extend(["", "## Highest priority gaps", ""])
    if gap_commands:
        lines.extend(["| Extension | Surface | Command | Gaps |", "| --- | --- | --- | --- |"])
        for command in sorted(gap_commands, key=lambda c: (c.status() != "Gap", c.extension, c.surface, c.invocation_name))[:50]:
            lines.append(f"| {command.extension} | {command.surface} | `{command.invocation_name}` | {', '.join(command.gaps())} |")
    else:
        lines.append("No command-surface gaps found.")

    for extension, extension_commands in sorted(extensions.items()):
        cli_benchmarks = extension_benchmarks.get(extension, {}).get("CLI", [])
        ps_benchmarks = extension_benchmarks.get(extension, {}).get("PowerShell", [])
        lines.extend([
            "",
            f"## {extension}",
            "",
            f"Extension-level benchmarks: CLI={len(cli_benchmarks) or 'no'}, PowerShell={len(ps_benchmarks) or 'no'}.",
            "",
            "| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |",
            "| --- | --- | --- | --- | --- | --- | --- | --- | --- |",
        ])
        for command in sorted(extension_commands, key=lambda c: (c.surface, c.invocation_name)):
            lines.append(
                f"| {command.surface} | `{command.invocation_name}` | "
                f"{yes_no(bool(command.unit_tests))} | "
                f"{yes_no(bool(command.scenario_tests))} | "
                f"{yes_no(bool(command.script_scenario_tests))} | "
                f"{yes_no(command.docs_present)} | "
                f"{yes_no(bool(command.docs_tests))} | "
                f"{yes_no(bool(command.benchmarks))} | "
                f"{command.status()} |"
            )

    lines.append("")
    return "\n".join(lines)


def as_html(commands: list[CommandCoverage], commit: str, extension_benchmarks: dict[str, dict[str, list[TestHit]]]) -> str:
    extensions: dict[str, list[CommandCoverage]] = {}
    for command in commands:
        extensions.setdefault(command.extension, []).append(command)

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
        good = sum(1 for c in extension_commands if c.status() == "Good")
        partial = sum(1 for c in extension_commands if c.status() == "Partial")
        gaps = sum(1 for c in extension_commands if c.status() == "Gap")
        scenario_gaps = sum(1 for c in extension_commands if "missing-scenario" in c.gaps())
        cli_bench = len(extension_benchmarks.get(extension, {}).get("CLI", []))
        ps_bench = len(extension_benchmarks.get(extension, {}).get("PowerShell", []))
        rows.append(
            f"<tr><td><a href=\"#{html.escape(extension.lower())}\">{html.escape(extension)}</a></td>"
            f"<td>{len(extension_commands)}</td><td>{good}</td><td>{partial}</td><td>{gaps}</td><td>{scenario_gaps}</td>"
            f"<td>{cli_bench or '<span class=\"no\">no</span>'}</td><td>{ps_bench or '<span class=\"no\">no</span>'}</td></tr>"
        )

    extension_sections = []
    for extension, extension_commands in sorted(extensions.items()):
        cli_benchmarks = extension_benchmarks.get(extension, {}).get("CLI", [])
        ps_benchmarks = extension_benchmarks.get(extension, {}).get("PowerShell", [])
        command_rows = []
        for command in sorted(extension_commands, key=lambda c: (c.surface, c.invocation_name)):
            command_rows.append(
                "<tr>"
                f"<td>{html.escape(command.surface)}</td>"
                f"<td><code>{html.escape(command.invocation_name)}</code></td>"
                f"<td>{yn(bool(command.unit_tests))}</td>"
                f"<td>{yn(bool(command.scenario_tests))}</td>"
                f"<td>{yn(bool(command.script_scenario_tests))}</td>"
                f"<td>{yn(command.docs_present)}</td>"
                f"<td>{yn(bool(command.docs_tests))}</td>"
                f"<td>{yn(bool(command.benchmarks))}</td>"
                f"<td>{badge(command.status())}</td>"
                "</tr>"
            )
        extension_sections.append(
            f"""
            <section>
              <h2 id="{html.escape(extension.lower())}">{html.escape(extension)}</h2>
              <p class="meta">Extension-level benchmarks: CLI={len(cli_benchmarks) or 'no'}, PowerShell={len(ps_benchmarks) or 'no'}.</p>
              <table>
                <thead>
                  <tr><th>Surface</th><th>Command</th><th>Unit</th><th>Scenario</th><th>Script scenario</th><th>Docs present</th><th>Docs test</th><th>Bench</th><th>Status</th></tr>
                </thead>
                <tbody>{''.join(command_rows)}</tbody>
              </table>
            </section>
            """
        )

    gap_commands = [c for c in commands if c.status() == "Gap" or "missing-scenario" in c.gaps()]
    gap_rows = []
    for command in sorted(gap_commands, key=lambda c: (c.status() != "Gap", c.extension, c.surface, c.invocation_name))[:50]:
        gap_rows.append(
            "<tr>"
            f"<td>{html.escape(command.extension)}</td>"
            f"<td>{html.escape(command.surface)}</td>"
            f"<td><code>{html.escape(command.invocation_name)}</code></td>"
            f"<td>{html.escape(', '.join(command.gaps()))}</td>"
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
    main {{ max-width: 1180px; margin: 0 auto; padding: 32px 20px 64px; }}
    h1 {{ margin-bottom: 4px; }}
    h2 {{ margin-top: 36px; }}
    a {{ color: var(--link); }}
    .meta {{ color: var(--muted); margin-top: 0; }}
    table {{ width: 100%; border-collapse: collapse; margin: 16px 0 24px; background: var(--panel); }}
    th, td {{ border: 1px solid var(--border); padding: 8px 10px; text-align: left; vertical-align: top; }}
    th {{ position: sticky; top: 0; background: #21262d; }}
    code {{ font-family: ui-monospace, SFMono-Regular, Consolas, monospace; }}
    .badge {{ border-radius: 999px; padding: 2px 8px; font-size: 12px; font-weight: 600; color: white; }}
    .good {{ background: var(--good); }}
    .partial {{ background: var(--partial); }}
    .gap {{ background: var(--gap); }}
    .yes {{ color: #3fb950; font-weight: 600; }}
    .no {{ color: var(--muted); }}
  </style>
</head>
<body>
  <main>
    <h1>Azclips extension test coverage</h1>
    <p class="meta">Generated from <code>{html.escape(commit[:12] or 'unknown')}</code> on {html.escape(generated)}.</p>
    <p><a href="extension-test-coverage.md">Markdown</a> · <a href="extension-test-coverage.json">JSON</a></p>
    <h2>Summary</h2>
    <table>
      <thead><tr><th>Extension</th><th>Commands</th><th>Good</th><th>Partial</th><th>Gaps</th><th>Scenario gaps</th><th>CLI bench</th><th>PS bench</th></tr></thead>
      <tbody>{''.join(rows)}</tbody>
    </table>
    <h2>Highest priority gaps</h2>
    <table>
      <thead><tr><th>Extension</th><th>Surface</th><th>Command</th><th>Gaps</th></tr></thead>
      <tbody>{''.join(gap_rows)}</tbody>
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

    commands = parse_commands(repo)
    map_tests(repo, commands)
    extension_benchmarks = benchmark_inventory(repo)
    commit = args.commit or get_git_sha(repo)

    report_json = as_json(commands, commit, extension_benchmarks)
    (out_dir / "extension-test-coverage.json").write_text(json.dumps(report_json, indent=2), encoding="utf-8")
    (out_dir / "extension-test-coverage.md").write_text(as_markdown(commands, commit, extension_benchmarks), encoding="utf-8")
    (out_dir / "index.html").write_text(as_html(commands, commit, extension_benchmarks), encoding="utf-8")
    (out_dir / "data.js").write_text(
        "window.EXTENSION_TEST_COVERAGE = " + json.dumps(report_json, indent=2) + ";\n",
        encoding="utf-8",
    )

    total = len(commands)
    gaps = sum(1 for command in commands if command.status() == "Gap")
    scenario_gaps = sum(1 for command in commands if "missing-scenario" in command.gaps())
    print(f"Wrote {total} command rows to {out_dir}")
    print(f"Commands with no mapped tests: {gaps}")
    print(f"Commands missing scenario coverage: {scenario_gaps}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
