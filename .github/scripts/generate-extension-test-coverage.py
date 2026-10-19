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
    docs_tests: list[TestHit] = field(default_factory=list)
    benchmarks: list[TestHit] = field(default_factory=list)

    def score(self) -> int:
        return (
            (1 if self.unit_tests else 0)
            + (2 if self.scenario_tests or self.script_scenario_tests else 0)
            + (1 if self.docs_tests else 0)
            + (1 if self.benchmarks else 0)
        )

    def gaps(self) -> list[str]:
        gaps: list[str] = []
        if not self.unit_tests:
            gaps.append("missing-unit")
        if not self.scenario_tests and not self.script_scenario_tests:
            gaps.append("missing-scenario")
        if not self.docs_tests:
            gaps.append("missing-docs")
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


def parse_commands(repo: Path) -> list[CommandCoverage]:
    commands: list[CommandCoverage] = []
    extension_indexes = sorted((repo / "src" / "Extensions").glob("*/ExtensionIndex.cs"))
    command_pattern = re.compile(
        r"CommandMetadata\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*new\((?P<body>.*?)\n\s*\);",
        re.DOTALL,
    )

    for index_path in extension_indexes:
        extension = index_path.parent.name
        text = index_path.read_text(encoding="utf-8-sig")
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

        seen: set[tuple[str, str, str]] = set()
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

            surface = surface_from_name(match.group("name"), invocation_name, current_region(match.start()))
            key = (extension, surface, invocation_name)
            if key in seen:
                continue
            seen.add(key)
            commands.append(CommandCoverage(extension, surface, invocation_name, processor_type))

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
    if "_" in command.invocation_name and command.surface == "PowerShell":
        tokens.add(normalize(command.invocation_name.split("_", 1)[0]))
    if command.processor_type:
        tokens.add(normalize(command.processor_type.rsplit(".", 1)[-1]))
    return {token for token in tokens if token}


def command_test_name(path: Path) -> str:
    return path.stem


def add_test_hit(target: list[TestHit], repo: Path, path: Path, confidence: str) -> None:
    rel = repo_relative(repo, path)
    if all(existing.path != rel for existing in target):
        target.append(TestHit(command_test_name(path), rel, confidence))


def map_tests(repo: Path, commands: list[CommandCoverage]) -> None:
    unit_files, scenario_files, benchmark_files = read_index(repo)

    for command in commands:
        tokens = command_tokens(command)
        for path, _text, norm in unit_files:
            token_hit = any(token and token in norm for token in tokens)
            if token_hit:
                add_test_hit(command.unit_tests, repo, path, "token")

        for path, _text, norm in scenario_files:
            if any(token and token in norm for token in tokens):
                if "ScriptScenario" in path.name:
                    add_test_hit(command.script_scenario_tests, repo, path, "token")
                else:
                    add_test_hit(command.scenario_tests, repo, path, "token")

        for path, text, norm in benchmark_files:
            has_extension_attr = f'BenchmarkExtension("{command.extension}")' in text
            if has_extension_attr and any(token and token in norm for token in tokens):
                add_test_hit(command.benchmarks, repo, path, "token")

        docs_path = docs_file_for_command(repo, command)
        if docs_path and docs_path.exists():
            command.docs_tests.append(TestHit(docs_path.stem, repo_relative(repo, docs_path), "doc-present"))


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


def as_json(commands: list[CommandCoverage], commit: str) -> dict:
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
                "commands": [
                    {
                        "surface": command.surface,
                        "invocationName": command.invocation_name,
                        "processorType": command.processor_type,
                        "unitTests": [hit_json(hit) for hit in command.unit_tests],
                        "scenarioTests": [hit_json(hit) for hit in command.scenario_tests],
                        "scriptScenarioTests": [hit_json(hit) for hit in command.script_scenario_tests],
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


def as_markdown(commands: list[CommandCoverage], commit: str) -> str:
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
        "| Extension | Commands | Good | Partial | Gaps | Scenario gaps |",
        "| --- | ---: | ---: | ---: | ---: | ---: |",
    ]

    for extension, extension_commands in sorted(extensions.items()):
        good = sum(1 for c in extension_commands if c.status() == "Good")
        partial = sum(1 for c in extension_commands if c.status() == "Partial")
        gaps = sum(1 for c in extension_commands if c.status() == "Gap")
        scenario_gaps = sum(1 for c in extension_commands if "missing-scenario" in c.gaps())
        lines.append(f"| {extension} | {len(extension_commands)} | {good} | {partial} | {gaps} | {scenario_gaps} |")

    gap_commands = [c for c in commands if c.status() == "Gap" or "missing-scenario" in c.gaps()]
    lines.extend(["", "## Highest priority gaps", ""])
    if gap_commands:
        lines.extend(["| Extension | Surface | Command | Gaps |", "| --- | --- | --- | --- |"])
        for command in sorted(gap_commands, key=lambda c: (c.status() != "Gap", c.extension, c.surface, c.invocation_name))[:50]:
            lines.append(f"| {command.extension} | {command.surface} | `{command.invocation_name}` | {', '.join(command.gaps())} |")
    else:
        lines.append("No command-surface gaps found.")

    for extension, extension_commands in sorted(extensions.items()):
        lines.extend([
            "",
            f"## {extension}",
            "",
            "| Surface | Command | Unit | Scenario | Script scenario | Docs/help | Bench | Status |",
            "| --- | --- | --- | --- | --- | --- | --- | --- |",
        ])
        for command in sorted(extension_commands, key=lambda c: (c.surface, c.invocation_name)):
            lines.append(
                f"| {command.surface} | `{command.invocation_name}` | "
                f"{yes_no(bool(command.unit_tests))} | "
                f"{yes_no(bool(command.scenario_tests))} | "
                f"{yes_no(bool(command.script_scenario_tests))} | "
                f"{yes_no(bool(command.docs_tests))} | "
                f"{yes_no(bool(command.benchmarks))} | "
                f"{command.status()} |"
            )

    lines.append("")
    return "\n".join(lines)


def as_html(commands: list[CommandCoverage], commit: str) -> str:
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
        rows.append(
            f"<tr><td><a href=\"#{html.escape(extension.lower())}\">{html.escape(extension)}</a></td>"
            f"<td>{len(extension_commands)}</td><td>{good}</td><td>{partial}</td><td>{gaps}</td><td>{scenario_gaps}</td></tr>"
        )

    extension_sections = []
    for extension, extension_commands in sorted(extensions.items()):
        command_rows = []
        for command in sorted(extension_commands, key=lambda c: (c.surface, c.invocation_name)):
            command_rows.append(
                "<tr>"
                f"<td>{html.escape(command.surface)}</td>"
                f"<td><code>{html.escape(command.invocation_name)}</code></td>"
                f"<td>{yn(bool(command.unit_tests))}</td>"
                f"<td>{yn(bool(command.scenario_tests))}</td>"
                f"<td>{yn(bool(command.script_scenario_tests))}</td>"
                f"<td>{yn(bool(command.docs_tests))}</td>"
                f"<td>{yn(bool(command.benchmarks))}</td>"
                f"<td>{badge(command.status())}</td>"
                "</tr>"
            )
        extension_sections.append(
            f"""
            <section>
              <h2 id="{html.escape(extension.lower())}">{html.escape(extension)}</h2>
              <table>
                <thead>
                  <tr><th>Surface</th><th>Command</th><th>Unit</th><th>Scenario</th><th>Script scenario</th><th>Docs/help</th><th>Bench</th><th>Status</th></tr>
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
      <thead><tr><th>Extension</th><th>Commands</th><th>Good</th><th>Partial</th><th>Gaps</th><th>Scenario gaps</th></tr></thead>
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
    commit = args.commit or get_git_sha(repo)

    report_json = as_json(commands, commit)
    (out_dir / "extension-test-coverage.json").write_text(json.dumps(report_json, indent=2), encoding="utf-8")
    (out_dir / "extension-test-coverage.md").write_text(as_markdown(commands, commit), encoding="utf-8")
    (out_dir / "index.html").write_text(as_html(commands, commit), encoding="utf-8")
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
