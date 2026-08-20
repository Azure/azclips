#!/usr/bin/env python3

import argparse
import hashlib
import json
import os
import stat
import sys
from pathlib import Path


def sha256_bytes(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def create_entry(root: Path, path: Path) -> dict:
    metadata = path.lstat()
    relative_path = path.relative_to(root).as_posix()
    entry = {
        "path": relative_path,
        "mode": f"{stat.S_IMODE(metadata.st_mode):04o}",
    }

    if path.is_symlink():
        target = os.readlink(path)
        target_bytes = os.fsencode(target)
        entry.update({
            "type": "symlink",
            "size": len(target_bytes),
            "sha256": sha256_bytes(target_bytes),
            "target": target,
        })
    elif path.is_file():
        entry.update({
            "type": "file",
            "size": metadata.st_size,
            "sha256": sha256_file(path),
        })
    elif path.is_dir():
        entry.update({
            "type": "directory",
            "size": 0,
            "sha256": None,
        })
    else:
        raise ValueError(f"Unsupported payload entry type: {relative_path}")

    return entry


def collect_entries(root: Path) -> list[dict]:
    paths = []
    for current_root, directories, files in os.walk(root, followlinks=False):
        current_path = Path(current_root)
        paths.extend(current_path / name for name in directories)
        paths.extend(current_path / name for name in files)
    return [create_entry(root, path) for path in sorted(paths, key=lambda item: item.relative_to(root).as_posix())]


def build_manifest(root: Path, rid: str, profile: str) -> dict:
    return {
        "schemaVersion": 1,
        "rid": rid,
        "profile": profile,
        "entries": collect_entries(root),
    }


def write_manifest(manifest: dict, output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Create or verify a canonical Linux payload manifest.")
    parser.add_argument("action", choices=("generate", "verify"))
    parser.add_argument("--payload-dir", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--rid")
    parser.add_argument("--profile")
    args = parser.parse_args()

    payload_dir = args.payload_dir.resolve()
    manifest_path = args.manifest.resolve()
    if not payload_dir.is_dir():
        parser.error(f"payload directory does not exist: {payload_dir}")
    if manifest_path == payload_dir or payload_dir in manifest_path.parents:
        parser.error("manifest must be stored outside the payload directory")

    if args.action == "generate":
        if not args.rid or not args.profile:
            parser.error("generate requires --rid and --profile")
        write_manifest(build_manifest(payload_dir, args.rid, args.profile), manifest_path)
        print(f"Created payload manifest at {manifest_path}")
        return 0

    if not manifest_path.is_file():
        parser.error(f"manifest does not exist: {manifest_path}")
    expected = json.loads(manifest_path.read_text(encoding="utf-8"))
    actual = build_manifest(payload_dir, expected["rid"], expected["profile"])
    if actual != expected:
        print(f"ERROR: Payload does not match manifest: {manifest_path}", file=sys.stderr)
        return 1

    print(f"Verified payload manifest: {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())