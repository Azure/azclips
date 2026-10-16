#!/usr/bin/env bash
set -euo pipefail

VERSION="${VERSION:?ERROR: VERSION is required}"
METADATA_ROOT="${METADATA_ROOT:?ERROR: METADATA_ROOT is required}"
OUTPUT_ROOT="${OUTPUT_ROOT:?ERROR: OUTPUT_ROOT is required}"
CLI_REGISTRY_SERVER="${CLI_REGISTRY_SERVER:?ERROR: CLI_REGISTRY_SERVER is required}"
CLI_REPOSITORY="${CLI_REPOSITORY:?ERROR: CLI_REPOSITORY is required}"
POWERSHELL_REGISTRY_SERVER="${POWERSHELL_REGISTRY_SERVER:?ERROR: POWERSHELL_REGISTRY_SERVER is required}"
POWERSHELL_REPOSITORY="${POWERSHELL_REPOSITORY:?ERROR: POWERSHELL_REPOSITORY is required}"

digest_file="$METADATA_ROOT/image-digests.txt"
if [[ ! -f "$digest_file" ]]; then
  echo "ERROR: Published image digest metadata is missing '$digest_file'."
  exit 1
fi

pull_image() {
  local artifact_name="$1"
  local registry_server="$2"
  local repository="$3"
  local source_tag_suffix="$4"
  local manifest_tag_suffix="$5"
  local platform="$6"
  local source_tag="$registry_server/$repository:$VERSION-$source_tag_suffix"
  local source_prefix="$source_tag@"
  local source_ref source_digest manifest_tag manifest_digest image_tag

  source_ref="$(grep -F "$source_prefix" "$digest_file" || true)"
  if [[ "$(grep -c . <<<"$source_ref")" -ne 1 ]]; then
    echo "ERROR: Expected one published digest for '$source_prefix'."
    printf '%s\n' "$source_ref"
    exit 1
  fi

  source_digest="${source_ref##*@}"
  manifest_tag="$registry_server/$repository:$VERSION-$manifest_tag_suffix"
  manifest_digest="$(
    docker buildx imagetools inspect --raw "$manifest_tag" |
      python3 -c '
import json
import sys

platform = sys.argv[1]
os_name, architecture = platform.split("/", 1)
manifest = json.load(sys.stdin)
matches = [
    entry["digest"]
    for entry in manifest.get("manifests", [])
    if entry.get("platform", {}).get("os") == os_name
    and entry.get("platform", {}).get("architecture") == architecture
]
if len(matches) != 1:
    raise SystemExit(
        f"Expected one {platform} manifest entry, found {len(matches)}."
    )
print(matches[0])
' "$platform"
  )"
  if [[ "$manifest_digest" != "$source_digest" ]]; then
    echo "ERROR: Manifest '$manifest_tag' maps '$platform' to '$manifest_digest', expected '$source_digest'."
    exit 1
  fi

  docker pull --platform "$platform" "$source_ref"
  image_tag="$manifest_tag"
  docker tag "$source_ref" "$image_tag"
  mkdir -p "$OUTPUT_ROOT/$artifact_name"
  docker save --output "$OUTPUT_ROOT/$artifact_name/image.tar" "$image_tag"
}

pull_image Docker_Verification_CLI_AzureLinux3_Lean_Amd64 \
  "$CLI_REGISTRY_SERVER" "$CLI_REPOSITORY" azurelinux3.0-lean-amd64 azurelinux3.0-lean linux/amd64
pull_image Docker_Verification_CLI_AzureLinux3_Lean_Arm64 \
  "$CLI_REGISTRY_SERVER" "$CLI_REPOSITORY" azurelinux3.0-lean-arm64 azurelinux3.0-lean linux/arm64
pull_image Docker_Verification_CLI_AzureLinux3_Full_Amd64 \
  "$CLI_REGISTRY_SERVER" "$CLI_REPOSITORY" azurelinux3.0-amd64 azurelinux3.0 linux/amd64
pull_image Docker_Verification_CLI_AzureLinux3_Full_Arm64 \
  "$CLI_REGISTRY_SERVER" "$CLI_REPOSITORY" azurelinux3.0-arm64 azurelinux3.0 linux/arm64
pull_image Docker_Verification_PowerShell_AzureLinux3_Amd64 \
  "$POWERSHELL_REGISTRY_SERVER" "$POWERSHELL_REPOSITORY" azurelinux-3.0-amd64 azurelinux-3.0 linux/amd64
pull_image Docker_Verification_PowerShell_AzureLinux3_Arm64 \
  "$POWERSHELL_REGISTRY_SERVER" "$POWERSHELL_REPOSITORY" azurelinux-3.0-arm64 azurelinux-3.0 linux/arm64
