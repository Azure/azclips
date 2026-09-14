#!/usr/bin/env python3

import argparse
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections.abc import Callable
from pathlib import Path

STABLE_RELEASE_BRANCH_PREFIX = "release/trigger-stable-release-"
STABLE_RELEASE_PR_AUTHOR = "x-engineering-agent[bot]"
STABLE_RELEASE_TITLE_PATTERN = re.compile(r"^\{DevOps\} Trigger stable release (?P<version>\d+\.\d+\.\d+)$")
STABLE_RELEASE_ALLOWED_PATH_PATTERN = re.compile(
    r"^(?:CHANGELOG\.md|src/CLI/Core\.Cli\.csproj|"
    r"src/Extensions/[^/]+/Extensions\.[^/]+\.csproj|"
    r"src/PS/[^/]+/(?:ChangeLog\.md|[^/]+\.psd1))$"
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


def get_project_versions(content: str) -> dict[str, tuple[str, ...]]:
    root = ET.fromstring(content)
    values: dict[str, list[str]] = {}
    for property_group in root.iter():
        if property_group.tag.rsplit("}", 1)[-1] != "PropertyGroup":
            continue
        for element in property_group:
            property_name = element.tag.rsplit("}", 1)[-1]
            if property_name in VERSION_PROPERTIES:
                values.setdefault(property_name, []).append((element.text or "").strip())
    return {name: tuple(sorted(property_values)) for name, property_values in values.items()}


def find_policy_violations(
    changed_files: list[str],
    read_base_file: Callable[[str], str | None],
    read_head_file: Callable[[str], str | None],
    stable_release: bool = False,
) -> list[str]:
    violations = []
    if stable_release:
        for file_path in changed_files:
            if not STABLE_RELEASE_ALLOWED_PATH_PATTERN.fullmatch(file_path):
                violations.append(f"{file_path}: generated stable-release PRs may only change release metadata.")
    elif "CHANGELOG.md" in changed_files:
        violations.append("CHANGELOG.md must not be edited manually; stable-release automation owns it.")

    for file_path in sorted(path for path in changed_files if path.endswith(".csproj")):
        base_content = read_base_file(file_path)
        head_content = read_head_file(file_path)
        if base_content is None or head_content is None:
            if stable_release:
                violations.append(f"{file_path}: generated stable-release PRs may not add or delete project files.")
            continue

        if stable_release:
            base_structure = get_project_structure_without_versions(base_content)
            head_structure = get_project_structure_without_versions(head_content)
            if base_structure != head_structure:
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


def get_project_structure_without_versions(content: str) -> tuple:
    root = ET.fromstring(content)

    def canonicalize(element: ET.Element, parent_name: str = "") -> tuple:
        name = element.tag.rsplit("}", 1)[-1]
        text = (element.text or "").strip()
        children = (
            child
            for child in element
            if not (name == "PropertyGroup" and child.tag.rsplit("}", 1)[-1] in VERSION_PROPERTIES)
        )
        return (
            name,
            tuple(sorted(element.attrib.items())),
            text,
            tuple(canonicalize(child, name) for child in children),
        )

    return canonicalize(root)


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

    changed_files = run_git(
        repo_root,
        ["diff", "--name-only", f"{args.base_sha}...{args.head_sha}", "--"],
    ).splitlines()
    violations = find_policy_violations(
        changed_files,
        lambda path: read_file_at_ref(repo_root, args.base_sha, path),
        lambda path: read_file_at_ref(repo_root, args.head_sha, path),
        stable_release=stable_release,
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
