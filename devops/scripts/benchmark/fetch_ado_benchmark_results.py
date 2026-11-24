#!/usr/bin/env python3
"""Download a verified Azure DevOps run's Benchmark_Results for the GitHub workflows.

The run is checked through the Azure DevOps REST API (anonymous for the public
project) against the expected project, pipeline, trigger and commit; nothing inside the
artifact is trusted for that. The artifact can come from a fork's pull request build,
so the download is size-capped and only expected files with safe paths are extracted.

Writes <out>/build.json (buildId, buildUrl, commit, pullRequest, stageResult,
failedJobs, available) and <out>/results/, and sets the step outputs available, commit
and pr_number when GITHUB_OUTPUT is set.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import stat
import sys
import urllib.parse
import urllib.request
import zipfile
from dataclasses import dataclass
from pathlib import Path

from benchmark_results import latest_attempts, warn

ARTIFACT = "Benchmark_Results"
STAGE = "Benchmark"
MAX_DOWNLOAD_BYTES = 256 * 1024 * 1024
MAX_EXTRACTED_BYTES = 1024 * 1024 * 1024
MAX_FILE_BYTES = 64 * 1024 * 1024
MAX_FILES = 5000
CHUNK_BYTES = 1024 * 1024
COMMIT = re.compile(r"^[0-9a-f]{40}\Z")
FAILED_RESULTS = ("failed", "canceled", "abandoned")
ALLOWED_FILES = (
    re.compile(r"^metadata\.json\Z"),
    re.compile(r"^summary\.md\Z"),
    re.compile(r"^reports/[A-Z0-9]+/(?:CLI|PS|STARTUP)/results/[A-Za-z0-9._-]+-report-full-compressed\.json\Z"),
    re.compile(r"^sidecars/[A-Z0-9]+/(?:CLI|PS|STARTUP)/[A-Za-z0-9._-]+\.json\Z"),
)


class VerificationError(Exception):
    """The run is not one this workflow may act on."""


class UnsafeArchiveError(Exception):
    """The artifact holds a path, link or size that is not allowed."""


@dataclass(frozen=True)
class BuildFacts:
    build_id: int
    commit: str
    pull_request: int | None


def verify_build(build, *, project: str, definition: str, expect: str, head_sha: str | None) -> BuildFacts:
    """Check a build API response and return the facts the workflows rely on."""
    if not isinstance(build, dict):
        raise VerificationError("unexpected build response")
    if (build.get("project") or {}).get("name") != project:
        raise VerificationError(f"the run is not in the {project} project")
    if (build.get("definition") or {}).get("name") != definition:
        raise VerificationError(f"the run is not a run of {definition}")
    if build.get("status") != "completed":
        raise VerificationError("the run has not completed")
    build_id = build.get("id")
    if not isinstance(build_id, int) or isinstance(build_id, bool):
        raise VerificationError("the run has no ID")

    if expect == "pull-request":
        if build.get("reason") != "pullRequest":
            raise VerificationError("the run was not triggered by a pull request")
        trigger = build.get("triggerInfo") or {}
        commit, number = trigger.get("pr.sourceSha"), trigger.get("pr.number")
        if not isinstance(number, str) or not number.isdigit():
            raise VerificationError("the run has no pull request number")
        pull_request = int(number)
    else:
        if build.get("sourceBranch") != "refs/heads/main":
            raise VerificationError("the run did not build refs/heads/main")
        commit, pull_request = build.get("sourceVersion"), None

    if not isinstance(commit, str) or not COMMIT.match(commit):
        raise VerificationError("the run has no commit")
    if head_sha and commit != head_sha:
        raise VerificationError(f"the run built {commit}, not {head_sha}")
    return BuildFacts(build_id, commit, pull_request)


def pick_artifact(artifacts) -> dict | None:
    """The newest attempt of Benchmark_Results among a run's artifacts."""
    by_name = {item["name"]: item for item in artifacts
               if isinstance(item, dict) and isinstance(item.get("name"), str)}
    name = latest_attempts(by_name).get(ARTIFACT)
    return by_name[name] if name else None


def download_url(artifact: dict) -> str:
    resource = artifact.get("resource") or {}
    url = resource.get("downloadUrl")
    parts = urllib.parse.urlsplit(url) if isinstance(url, str) else None
    host = (parts.hostname or "") if parts else ""
    if (resource.get("type") != "PipelineArtifact" or not parts or parts.scheme != "https"
            or not (host == "dev.azure.com" or host.endswith(".visualstudio.com"))):
        raise VerificationError("the artifact has no Azure DevOps download URL")
    return url


def download(url: str, destination: Path, opener=urllib.request.urlopen) -> None:
    """Stream the artifact zip to destination, refusing anything over the size limit."""
    request = urllib.request.Request(url, headers={"Accept": "application/zip"})
    written = 0
    with opener(request, timeout=300) as response, destination.open("wb") as handle:
        while chunk := response.read(CHUNK_BYTES):
            written += len(chunk)
            if written > MAX_DOWNLOAD_BYTES:
                raise UnsafeArchiveError("the artifact is larger than the download limit")
            handle.write(chunk)


def safe_extract(archive_path: Path, destination: Path, root: str) -> list[str]:
    """Extract the expected files under <root>/ in the zip. Returns their relative paths."""
    extracted, total = [], 0
    with zipfile.ZipFile(archive_path) as archive:
        members = archive.infolist()
        if len(members) > MAX_FILES:
            raise UnsafeArchiveError("the artifact has too many files")
        for member in members:
            if member.is_dir():
                continue
            name = member.filename
            parts = name.split("/")
            if ("\\" in name or ":" in name or any(part in ("", ".", "..") for part in parts)
                    or stat.S_ISLNK(member.external_attr >> 16)):
                raise UnsafeArchiveError(f"the artifact has an unsafe entry: {name!r}")
            if len(parts) < 2 or parts[0] != root:
                raise UnsafeArchiveError(f"the artifact has an entry outside {root}/: {name!r}")
            relative = "/".join(parts[1:])
            if not any(pattern.match(relative) for pattern in ALLOWED_FILES):
                warn(f"Ignoring an unexpected file in {root}: {relative!r}")
                continue
            target = destination.joinpath(*parts[1:])
            target.parent.mkdir(parents=True, exist_ok=True)
            size = 0
            with archive.open(member) as source, target.open("wb") as handle:
                while chunk := source.read(CHUNK_BYTES):
                    size += len(chunk)
                    total += len(chunk)
                    if size > MAX_FILE_BYTES or total > MAX_EXTRACTED_BYTES:
                        raise UnsafeArchiveError("the artifact expands beyond the size limit")
                    handle.write(chunk)
            extracted.append(relative)
    return extracted


def benchmark_stage_status(timeline) -> tuple[str | None, list[str]]:
    """The Benchmark stage result and the names of its failed jobs, from a build timeline."""
    records = [record for record in (timeline or {}).get("records") or [] if isinstance(record, dict)]
    parents = {record.get("id"): record.get("parentId") for record in records}
    stage = next((record for record in records
                  if record.get("type") == "Stage" and record.get("identifier") == STAGE), None)
    if stage is None:
        return None, []

    def in_stage(record) -> bool:
        seen, parent = set(), record.get("parentId")
        while parent and parent not in seen:
            if parent == stage.get("id"):
                return True
            seen.add(parent)
            parent = parents.get(parent)
        return False

    failed = sorted({record["name"] for record in records
                     if record.get("type") == "Job" and record.get("result") in FAILED_RESULTS
                     and isinstance(record.get("name"), str) and in_stage(record)})
    result = stage.get("result")
    return (result if isinstance(result, str) else None), failed


def fetch_json(url: str):
    request = urllib.request.Request(url, headers={"Accept": "application/json"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.load(response)


def set_outputs(values: dict) -> None:
    path = os.environ.get("GITHUB_OUTPUT")
    if path:
        with open(path, "a", encoding="utf-8") as handle:
            for name, value in values.items():
                handle.write(f"{name}={value}\n")


def main(argv=None, fetch=fetch_json, opener=urllib.request.urlopen) -> int:
    parser = argparse.ArgumentParser(description="Download a verified run's Benchmark_Results.")
    parser.add_argument("--organization-url", required=True, help="e.g. https://dev.azure.com/azclitools")
    parser.add_argument("--project", required=True)
    parser.add_argument("--definition", required=True, help="pipeline the run must belong to")
    parser.add_argument("--expect", required=True, choices=("pull-request", "main"))
    parser.add_argument("--build-id", required=True, type=int)
    parser.add_argument("--head-sha", help="commit the run must have built")
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args(argv)

    organization = args.organization_url.rstrip("/")
    project = urllib.parse.quote(args.project)
    api = f"{organization}/{project}/_apis/build/builds/{args.build_id}"
    try:
        facts = verify_build(fetch(f"{api}?api-version=7.1"), project=args.project, definition=args.definition,
                             expect=args.expect, head_sha=args.head_sha)
    except VerificationError as exc:
        print(f"::error::Azure DevOps run {args.build_id} was rejected: {exc}")
        return 1

    artifact = pick_artifact(fetch(f"{api}/artifacts?api-version=7.1").get("value") or [])
    stage_result, failed_jobs = benchmark_stage_status(fetch(f"{api}/timeline?api-version=7.1"))
    args.out.mkdir(parents=True, exist_ok=True)
    if artifact is None:
        print(f"Run {facts.build_id} has no {ARTIFACT} artifact, so nothing was benchmarked.")
    else:
        archive = args.out / "artifact.zip"
        download(download_url(artifact), archive, opener)
        files = safe_extract(archive, args.out / "results", artifact["name"])
        archive.unlink()
        print(f"Extracted {len(files)} file(s) from {artifact['name']} of run {facts.build_id}.")

    info = {
        "buildId": facts.build_id,
        "buildUrl": f"{organization}/{project}/_build/results?buildId={facts.build_id}",
        "commit": facts.commit,
        "pullRequest": facts.pull_request,
        "stageResult": stage_result,
        "failedJobs": failed_jobs,
        "available": artifact is not None,
    }
    (args.out / "build.json").write_text(json.dumps(info, indent=2) + "\n", encoding="utf-8")
    set_outputs({"available": "true" if artifact is not None else "false", "commit": facts.commit,
                 "pr_number": "" if facts.pull_request is None else str(facts.pull_request)})
    return 0


if __name__ == "__main__":
    sys.exit(main())
