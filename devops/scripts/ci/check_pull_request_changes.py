#!/usr/bin/env python3

import argparse
import difflib
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections.abc import Callable
from pathlib import Path

STABLE_RELEASE_BRANCH_PREFIX = "release/trigger-stable-release-"
STABLE_RELEASE_PR_AUTHOR = "x-engineering-agent[bot]"
STABLE_RELEASE_TITLE_PATTERN = re.compile(r"^\{DevOps\} Trigger stable release (?P<version>\d+\.\d+\.\d+)$")
STABLE_RELEASE_EXTENSION_PATH_PATTERN = re.compile(
    r"^src/Extensions/(?P<module>[^/]+)/Extensions\.(?P<project>[^/]+)\.csproj$"
)
STABLE_RELEASE_PS_PATH_PATTERN = re.compile(
    r"^src/PS/(?P<module>[^/]+)/(?P<file>[^/]+)$"
)
VERSION_PROPERTIES = {
    "AssemblyVersion",
    "FileVersion",
    "InformationalVersion",
    "PackageVersion",
    "Version",
    "VersionPrefix",
    "VersionSuffix",
}
VERSION_PROPERTY_LINE_PATTERN = re.compile(
    rf"^\s*<(?P<name>{'|'.join(sorted(VERSION_PROPERTIES))})>" r"(?P<value>[^<]*)</(?P=name)>\s*(?:\r?\n)?$"
)


def is_stable_release_pull_request(
    head_repository: str,
    base_repository: str,
    head_ref: str,
    pull_request_title: str,
    pull_request_author: str,
) -> bool:
    title_match = STABLE_RELEASE_TITLE_PATTERN.fullmatch(pull_request_title)
    return (
        head_repository.casefold() == base_repository.casefold()
        and pull_request_author.casefold() == STABLE_RELEASE_PR_AUTHOR.casefold()
        and title_match is not None
        and head_ref == f"{STABLE_RELEASE_BRANCH_PREFIX}{title_match.group('version')}"
    )


def is_stable_release_allowed_path(file_path: str) -> bool:
    if file_path in {"CHANGELOG.md", "src/CLI/Core.Cli.csproj"}:
        return True
    extension_match = STABLE_RELEASE_EXTENSION_PATH_PATTERN.fullmatch(file_path)
    if extension_match:
        return extension_match.group("module") == extension_match.group("project")
    ps_match = STABLE_RELEASE_PS_PATH_PATTERN.fullmatch(file_path)
    if not ps_match:
        return False
    return ps_match.group("file") in {"ChangeLog.md", f"{ps_match.group('module')}.psd1"}


def get_project_versions(content: str) -> dict[str, tuple[tuple, ...]]:
    root = ET.fromstring(content.removeprefix("\ufeff"))
    values: dict[str, list[tuple]] = {}
    for property_group in root.iter():
        if property_group.tag.rsplit("}", 1)[-1] != "PropertyGroup":
            continue
        for element in property_group:
            property_name = element.tag.rsplit("}", 1)[-1]
            if property_name in VERSION_PROPERTIES:
                values.setdefault(property_name, []).append(
                    (
                        tuple(sorted(property_group.attrib.items())),
                        tuple(sorted(element.attrib.items())),
                        (element.text or "").strip(),
                    )
                )
    return {name: tuple(sorted(property_values)) for name, property_values in values.items()}


def get_project_structure_without_versions(content: str) -> tuple:
    root = ET.fromstring(content.removeprefix("\ufeff"))

    def get_structure(element: ET.Element) -> tuple:
        element_name = element.tag.rsplit("}", 1)[-1]
        children = []
        for child in element:
            child_name = child.tag.rsplit("}", 1)[-1]
            if element_name == "PropertyGroup" and child_name in VERSION_PROPERTIES:
                continue
            children.append(get_structure(child))
        return (
            element.tag,
            tuple(sorted(element.attrib.items())),
            (element.text or "").strip(),
            tuple(children),
        )

    return get_structure(root)


def find_policy_violations(
    changed_files: list[str],
    read_base_file: Callable[[str], str | None],
    read_head_file: Callable[[str], str | None],
    stable_release: bool = False,
    project_renames: list[tuple[str, str]] | None = None,
) -> list[str]:
    violations = []
    if stable_release:
        for file_path in changed_files:
            if not is_stable_release_allowed_path(file_path):
                violations.append(f"{file_path}: generated stable-release PRs may only change release metadata.")
    elif "CHANGELOG.md" in changed_files:
        violations.append("CHANGELOG.md must not be edited manually; stable-release automation owns it.")

    if not stable_release:
        for old_path, new_path in project_renames or []:
            base_content = read_base_file(old_path)
            head_content = read_head_file(new_path)
            if (
                not old_path.endswith(".csproj")
                or not new_path.endswith(".csproj")
                or base_content is None
                or head_content is None
                or get_project_versions(base_content) != get_project_versions(head_content)
            ):
                violations.append(
                    f"{old_path} -> {new_path}: renamed project files must preserve release-owned versions."
                )

    for file_path in sorted(path for path in changed_files if path.endswith(".csproj")):
        base_content = read_base_file(file_path)
        head_content = read_head_file(file_path)
        if base_content is None or head_content is None:
            if stable_release:
                violations.append(f"{file_path}: generated stable-release PRs may not add or delete project files.")
            continue

        if stable_release:
            if not has_only_project_version_line_changes(base_content, head_content):
                violations.append(
                    f"{file_path}: generated stable-release PRs may only change project version properties."
                )
            continue

        base_versions = get_project_versions(base_content)
        head_versions = get_project_versions(head_content)
        for property_name in sorted(base_versions.keys() | head_versions.keys()):
            if base_versions.get(property_name) != head_versions.get(property_name):
                violations.append(
                    f"{file_path}: <{property_name}> must not be changed manually; "
                    "stable-release automation owns project versions."
                )

    return violations


def has_only_project_version_line_changes(base_content: str, head_content: str) -> bool:
    base_versions = get_project_versions(base_content)
    head_versions = get_project_versions(head_content)

    if base_versions.keys() != head_versions.keys() or any(
        len(head_versions[name]) != len(records) for name, records in base_versions.items()
    ):
        return False
    for name, records in base_versions.items():
        base_contexts = sorted(record[:2] for record in records)
        head_contexts = [record[:2] for record in head_versions.get(name, ())]
        if any(base_contexts.count(context) > head_contexts.count(context) for context in set(base_contexts)):
            return False
    if get_project_structure_without_versions(base_content) != get_project_structure_without_versions(head_content):
        return False

    def is_direct_version_line(line: str, versions: dict[str, tuple[tuple, ...]]) -> bool:
        match = VERSION_PROPERTY_LINE_PATTERN.fullmatch(line)
        return bool(
            match
            and any(
                record[2] == match.group("value").strip()
                for record in versions.get(match.group("name"), ())
            )
        )

    base_lines = base_content.splitlines(keepends=True)
    head_lines = head_content.splitlines(keepends=True)
    for operation, base_start, base_end, head_start, head_end in difflib.SequenceMatcher(
        None, base_lines, head_lines
    ).get_opcodes():
        if operation == "equal":
            continue
        if operation == "delete":
            return False
        if not all(is_direct_version_line(line, base_versions) for line in base_lines[base_start:base_end]):
            return False
        if not all(is_direct_version_line(line, head_versions) for line in head_lines[head_start:head_end]):
            return False
    return True


def run_git(repo_root: Path, arguments: list[str]) -> str:
    result = subprocess.run(
        ["git", "-C", str(repo_root), *arguments],
        capture_output=True,
        check=False,
        text=True,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or "git command failed")
    return result.stdout


def read_file_at_ref(repo_root: Path, ref: str, file_path: str) -> str | None:
    result = subprocess.run(
        ["git", "-C", str(repo_root), "show", f"{ref}:{file_path}"],
        capture_output=True,
        check=False,
        text=True,
    )
    if result.returncode != 0:
        return None
    return result.stdout


def parse_changed_files(diff_output: str) -> tuple[list[str], list[tuple[str, str]]]:
    changed_files = []
    project_renames = []
    for line in diff_output.splitlines():
        fields = line.split("\t")
        status = fields[0]
        if status.startswith("R") and len(fields) == 3:
            old_path, new_path = fields[1:]
            changed_files.extend((old_path, new_path))
            if old_path.endswith(".csproj") or new_path.endswith(".csproj"):
                project_renames.append((old_path, new_path))
        elif len(fields) == 2:
            changed_files.append(fields[1])
    return list(dict.fromkeys(changed_files)), project_renames


def get_default_repo_root() -> Path:
    for candidate in Path(__file__).resolve().parents:
        if (candidate / ".git").exists():
            return candidate
    return Path.cwd()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", default=get_default_repo_root())
    parser.add_argument("--base-sha", required=True)
    parser.add_argument("--head-sha", required=True)
    parser.add_argument("--base-repository", required=True)
    parser.add_argument("--head-repository", required=True)
    parser.add_argument("--head-ref", required=True)
    parser.add_argument("--pull-request-title", required=True)
    parser.add_argument("--pull-request-author", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = Path(args.repo_root).resolve()

    stable_release = is_stable_release_pull_request(
        args.head_repository,
        args.base_repository,
        args.head_ref,
        args.pull_request_title,
        args.pull_request_author,
    )

    changed_files, project_renames = parse_changed_files(
        run_git(
            repo_root,
            ["diff", "--name-status", "--find-renames", f"{args.base_sha}...{args.head_sha}", "--"],
        )
    )
    violations = find_policy_violations(
        changed_files,
        lambda path: read_file_at_ref(repo_root, args.base_sha, path),
        lambda path: read_file_at_ref(repo_root, args.head_sha, path),
        stable_release=stable_release,
        project_renames=project_renames,
    )

    for violation in violations:
        print(f"##vso[task.logissue type=error]{violation}")

    if violations:
        print(f"Pull request file policy validation failed with {len(violations)} violation(s).")
        return 1

    print("Pull request file policy checks passed.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ET.ParseError, RuntimeError) as error:
        print(f"##vso[task.logissue type=error]{error}")
        raise SystemExit(1) from error
