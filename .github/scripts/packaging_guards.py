#!/usr/bin/env python3

import argparse
import base64
import json
import os
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path


COMMENT_MARKER = "<!-- packaging-guards -->"


def get_packageable_modules(paths: list[str]) -> list[str]:
    modules = set()
    pattern = re.compile(r"^src/PS/([^/]+)/([^/]+)\.psd1$")

    for file_path in paths:
        match = pattern.match(file_path)
        if match and match.group(1) == match.group(2) and match.group(1) != "Az":
            modules.add(match.group(1))

    return sorted(modules)


def get_rollup_modules(manifest_content: str) -> list[str]:
    modules = set()
    pattern = re.compile(
        r"ModuleName\s*=\s*['\"]\.\./([^/'\"]+)/([^/'\"]+)\.psd1['\"]"
    )

    for directory_name, manifest_name in pattern.findall(manifest_content):
        if directory_name == manifest_name:
            modules.add(directory_name)

    return sorted(modules)


def find_missing_rollup_modules(paths: list[str], manifest_content: str) -> list[str]:
    rollup_modules = set(get_rollup_modules(manifest_content))
    return [
        module_name
        for module_name in get_packageable_modules(paths)
        if module_name not in rollup_modules
    ]


def find_extension_changes_without_powershell(
    changed_files: list[str], packageable_modules: list[str]
) -> list[str]:
    packageable = set(packageable_modules)
    changed_extensions = set()
    changed_powershell_modules = set()

    for file_path in changed_files:
        extension_match = re.match(r"^src/Extensions/([^/]+)/.+\.cs$", file_path)
        if extension_match and extension_match.group(1) in packageable:
            changed_extensions.add(extension_match.group(1))

        powershell_match = re.match(r"^src/PS/([^/]+)/", file_path)
        if powershell_match:
            changed_powershell_modules.add(powershell_match.group(1))

    return sorted(changed_extensions - changed_powershell_modules)


def build_comment(
    missing_rollup_modules: list[str], extension_warnings: list[str]
) -> str | None:
    if not missing_rollup_modules and not extension_warnings:
        return None

    lines = ["## Packaging review", ""]

    if missing_rollup_modules:
        lines.extend(
            [
                "### PowerShell rollup warning",
                "",
                "These packageable modules are missing from `src/PS/Az/Az.psd1`:",
                "",
                *[f"- `Az.{module_name}`" for module_name in missing_rollup_modules],
                "",
            ]
        )

    if extension_warnings:
        lines.extend(
            [
                "### PowerShell review recommended",
                "",
                "Extension logic changed without a corresponding change under "
                "`src/PS/<Module>`:",
                "",
                *[f"- `{module_name}`" for module_name in extension_warnings],
                "",
                "Confirm that the PowerShell cmdlets, manifest, and help remain "
                "compatible. If no PowerShell change is needed, no action is required.",
                "",
            ]
        )

    lines.append(COMMENT_MARKER)
    return "\n".join(lines)


def list_repository_files(repo_root: Path) -> list[str]:
    return sorted(
        file_path.relative_to(repo_root).as_posix()
        for file_path in repo_root.rglob("*")
        if file_path.is_file() and ".git" not in file_path.parts
    )


def validate_repository_rollup(repo_root: Path) -> list[str]:
    manifest_path = repo_root / "src" / "PS" / "Az" / "Az.psd1"
    return find_missing_rollup_modules(
        list_repository_files(repo_root), manifest_path.read_text(encoding="utf-8")
    )


class GitHubClient:
    def __init__(self, token: str, api_url: str = "https://api.github.com") -> None:
        self._token = token
        self._api_url = api_url.rstrip("/")

    def request(self, method: str, path: str, payload: dict | None = None):
        body = json.dumps(payload).encode("utf-8") if payload is not None else None
        request = urllib.request.Request(
            f"{self._api_url}{path}",
            data=body,
            method=method,
            headers={
                "Accept": "application/vnd.github+json",
                "Authorization": f"Bearer {self._token}",
                "X-GitHub-Api-Version": "2022-11-28",
            },
        )
        with urllib.request.urlopen(request) as response:
            if response.status == 204:
                return None
            return json.load(response)

    def get_all(self, path: str) -> list[dict]:
        results = []
        page = 1
        separator = "&" if "?" in path else "?"
        while True:
            page_results = self.request(
                "GET", f"{path}{separator}per_page=100&page={page}"
            )
            results.extend(page_results)
            if len(page_results) < 100:
                return results
            page += 1


def review_pull_request(
    client: GitHubClient,
    repository: str,
    pull_request_number: int,
    head_repository: str,
    head_sha: str,
) -> int:
    changed_files = [
        item["filename"]
        for item in client.get_all(
            f"/repos/{repository}/pulls/{pull_request_number}/files"
        )
    ]
    tree_response = client.request(
        "GET", f"/repos/{head_repository}/git/trees/{head_sha}?recursive=1"
    )
    if tree_response.get("truncated"):
        raise RuntimeError("GitHub returned a truncated repository tree")
    tree = tree_response["tree"]
    repository_paths = [entry["path"] for entry in tree if entry["type"] == "blob"]

    manifest_entry = next(
        entry for entry in tree if entry["path"] == "src/PS/Az/Az.psd1"
    )
    manifest_blob = client.request(
        "GET", f"/repos/{head_repository}/git/blobs/{manifest_entry['sha']}"
    )
    manifest_content = base64.b64decode(manifest_blob["content"]).decode("utf-8")

    packageable_modules = get_packageable_modules(repository_paths)
    missing_modules = find_missing_rollup_modules(repository_paths, manifest_content)
    extension_warnings = find_extension_changes_without_powershell(
        changed_files, packageable_modules
    )
    comment_body = build_comment(missing_modules, extension_warnings)

    comments_path = f"/repos/{repository}/issues/{pull_request_number}/comments"
    comments = client.get_all(comments_path)
    existing_comment = next(
        (
            comment
            for comment in comments
            if comment["user"]["type"] == "Bot"
            and COMMENT_MARKER in comment.get("body", "")
        ),
        None,
    )

    if comment_body and existing_comment:
        client.request(
            "PATCH",
            f"/repos/{repository}/issues/comments/{existing_comment['id']}",
            {"body": comment_body},
        )
    elif comment_body:
        client.request("POST", comments_path, {"body": comment_body})
    elif existing_comment:
        client.request(
            "DELETE",
            f"/repos/{repository}/issues/comments/{existing_comment['id']}",
        )

    for module_name in extension_warnings:
        print(
            f"::warning::Extension logic changed for {module_name} without a matching "
            f"src/PS/{module_name} change."
        )

    if missing_modules:
        print(
            "::warning::PowerShell modules missing from the Az rollup: "
            + ", ".join(missing_modules)
        )

    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    validate_parser = subparsers.add_parser("validate-rollup")
    validate_parser.add_argument("repo_root", nargs="?", default=".")

    review_parser = subparsers.add_parser("review-pr")
    review_parser.add_argument("--repository", required=True)
    review_parser.add_argument("--pull-request", required=True, type=int)
    review_parser.add_argument("--head-repository", required=True)
    review_parser.add_argument("--head-sha", required=True)

    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.command == "validate-rollup":
        missing_modules = validate_repository_rollup(Path(args.repo_root).resolve())
        if missing_modules:
            print(
                "::warning::PowerShell modules missing from the Az rollup: "
                + ", ".join(missing_modules)
            )
            return 0
        print("All packageable PowerShell modules are included in the Az rollup.")
        return 0

    token = os.environ.get("GITHUB_TOKEN")
    if not token:
        raise RuntimeError("GITHUB_TOKEN is required for review-pr")
    return review_pull_request(
        GitHubClient(token, os.environ.get("GITHUB_API_URL", "https://api.github.com")),
        args.repository,
        args.pull_request,
        args.head_repository,
        args.head_sha,
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (urllib.error.HTTPError, urllib.error.URLError) as error:
        print(f"GitHub API request failed: {error}", file=sys.stderr)
        raise