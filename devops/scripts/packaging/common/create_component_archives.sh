#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"

read_artifact_config() {
  local key="$1"
  local default_value="$2"
  local config_path="${ARTIFACT_NAMING_CONFIG:-$PACKAGING_ROOT/config/artifact_naming.config}"
  local configured_value=""

  if [[ -f "$config_path" ]] && configured_value="$(awk -F= -v key="$key" '
      BEGIN { found = 0 }
      /^[[:space:]]*(#|$)/ { next }
      {
        k=$1
        sub(/^[[:space:]]+/, "", k)
        sub(/[[:space:]]+$/, "", k)
        if (k == key) {
          $1=""
          sub(/^=/, "")
          v=$0
          sub(/^[[:space:]]+/, "", v)
          sub(/[[:space:]]+$/, "", v)
          print v
          found = 1
          exit
        }
      }
      END { if (!found) exit 1 }
    ' "$config_path")"; then
    printf '%s\n' "$configured_value"
  else
    printf '%s\n' "$default_value"
  fi
}

usage() {
  cat <<'EOF'
Usage: create_component_archives.sh --cli-extensions-root DIR --ps-modules-root DIR --output-root DIR [--artifact-version VERSION] [--version-source-root DIR]

Creates:
  azcli-extensions-<version>.tar.gz
  azps-cmdlets-<version>.tar.gz
EOF
}

CLI_EXTENSIONS_ROOT=""
PSGALLERY_ROOT=""
OUTPUT_ROOT=""
ARTIFACT_VERSION="${ARTIFACT_VERSION:-}"
VERSION_SOURCE_ROOT=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --cli-extensions-root)
      CLI_EXTENSIONS_ROOT="$2"
      shift 2
      ;;
    --ps-modules-root|--psgallery-root)
      PSGALLERY_ROOT="$2"
      shift 2
      ;;
    --output-root)
      OUTPUT_ROOT="$2"
      shift 2
      ;;
    --artifact-version)
      ARTIFACT_VERSION="$2"
      shift 2
      ;;
    --version-source-root)
      VERSION_SOURCE_ROOT="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "ERROR: Unknown argument: $1"
      usage
      exit 1
      ;;
  esac
done

if [[ -z "$CLI_EXTENSIONS_ROOT" || -z "$PSGALLERY_ROOT" || -z "$OUTPUT_ROOT" ]]; then
  echo "ERROR: --cli-extensions-root, --ps-modules-root, and --output-root are required."
  usage
  exit 1
fi

ARTIFACT_VERSION_SUFFIX="${ARTIFACT_VERSION_SUFFIX-$(read_artifact_config artifact_version_suffix preview)}"
CLI_ARCHIVE_STEM="${CLI_ARCHIVE_STEM:-$(read_artifact_config cli_archive_stem azcli)}"
CLI_LEAN_ARCHIVE_STEM="${CLI_LEAN_ARCHIVE_STEM:-$(read_artifact_config cli_lean_archive_stem azcli-lean)}"
CLI_EXTENSIONS_ARCHIVE_STEM="${CLI_EXTENSIONS_ARCHIVE_STEM:-$(read_artifact_config cli_extensions_archive_stem azcli-extensions)}"
PS_CMDLETS_ARCHIVE_STEM="${PS_CMDLETS_ARCHIVE_STEM:-$(read_artifact_config ps_cmdlets_archive_stem azps-cmdlets)}"
INSTALL_MODULE_SCRIPT="${INSTALL_MODULE_SCRIPT:-$PACKAGING_ROOT/powershell/InstallModule.ps1}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
NOTICE_FILE="${NOTICE_FILE:-$REPO_ROOT/NOTICE}"
LICENSE_FILE="${LICENSE_FILE:-$REPO_ROOT/LICENSE}"
BUILTIN_CLI_EXTENSION_PACKAGES=(extension accounts config)

copy_legal_files() {
  local destination_root="$1"

  if [[ ! -f "$NOTICE_FILE" ]]; then
    echo "ERROR: NOTICE file was not found: $NOTICE_FILE"
    exit 1
  fi

  if [[ ! -f "$LICENSE_FILE" ]]; then
    echo "ERROR: LICENSE file was not found: $LICENSE_FILE"
    exit 1
  fi

  cp "$NOTICE_FILE" "$destination_root/NOTICE"
  cp "$LICENSE_FILE" "$destination_root/LICENSE"
}

resolve_config_label() {
  local key="$1"
  local default_value="$2"
  read_artifact_config "$key" "$default_value"
}

infer_artifact_version() {
  local source_root="$1"
  local stems=("$CLI_LEAN_ARCHIVE_STEM" "$CLI_ARCHIVE_STEM")
  local labels=(
    "$(resolve_config_label linux_musl_x64_label linux-musl-x64)"
    "$(resolve_config_label linux_musl_arm64_label linux-musl-arm64)"
    "$(resolve_config_label linux_x64_label linux-x64)"
    "$(resolve_config_label linux_arm64_label linux-arm64)"
    "$(resolve_config_label windows_x64_label win-x64)"
    "$(resolve_config_label windows_arm64_label win-arm64)"
    "$(resolve_config_label macos_arm64_label macos-arm64)"
    "$(resolve_config_label macos_x64_label macos-x86_64)"
  )

  local file name base stem label version
  while IFS= read -r file; do
    name="$(basename "$file")"
    base="$name"
    base="${base%.tar.gz}"
    base="${base%.zip}"

    for stem in "${stems[@]}"; do
      [[ "$base" == "$stem"-* ]] || continue
      for label in "${labels[@]}"; do
        [[ "$base" == *-"$label" ]] || continue
        version="${base#"$stem"-}"
        version="${version%-"$label"}"
        if [[ -n "$version" && "$version" != "$base" ]]; then
          printf '%s\n' "$version"
          return 0
        fi
      done
    done
  done < <(find "$source_root" -maxdepth 1 -type f \( -name '*.tar.gz' -o -name '*.zip' \) | sort)

  return 1
}

if [[ -z "$ARTIFACT_VERSION" ]]; then
  if [[ -n "$VERSION_SOURCE_ROOT" && -d "$VERSION_SOURCE_ROOT" ]]; then
    ARTIFACT_VERSION="$(infer_artifact_version "$VERSION_SOURCE_ROOT" || true)"
  fi
fi

if [[ -z "$ARTIFACT_VERSION" ]]; then
  echo "ERROR: Artifact version was not provided and could not be inferred."
  exit 1
fi

ARTIFACT_VERSION_SEGMENT="$ARTIFACT_VERSION"
if [[ -n "$ARTIFACT_VERSION_SUFFIX" && "$ARTIFACT_VERSION_SEGMENT" != *"-$ARTIFACT_VERSION_SUFFIX" ]]; then
  ARTIFACT_VERSION_SEGMENT="$ARTIFACT_VERSION_SEGMENT-$ARTIFACT_VERSION_SUFFIX"
fi

create_archive() {
  local source_root="$1"
  local package_glob="$2"
  local archive_path="$3"
  local description="$4"
  local exclude_builtin_cli_extensions="${5:-false}"
  local -a discovered_package_paths=()
  local -a package_paths=()
  local -a package_names=()
  local package_path package_name package_name_lower builtin_package

  if [[ ! -d "$source_root" ]]; then
    echo "ERROR: $description source root not found: $source_root"
    exit 1
  fi

  mapfile -t discovered_package_paths < <(find "$source_root" -maxdepth 1 -type f -name "$package_glob" | sort)
  for package_path in "${discovered_package_paths[@]}"; do
    if [[ "$exclude_builtin_cli_extensions" == "true" ]]; then
      package_name="$(basename "$package_path")"
      package_name_lower="${package_name,,}"
      for builtin_package in "${BUILTIN_CLI_EXTENSION_PACKAGES[@]}"; do
        if [[ "$package_name_lower" == "azcli.extensions.$builtin_package."*.nupkg ]]; then
          echo "Skipping built-in CLI extension package: $package_name"
          continue 2
        fi
      done
    fi

    package_paths+=("$package_path")
  done

  if [[ ${#package_paths[@]} -eq 0 ]]; then
    echo "ERROR: No $description packages matched '$package_glob' under $source_root"
    exit 1
  fi

  for package_path in "${package_paths[@]}"; do
    package_names+=("$(basename "$package_path")")
  done

  local staging_root
  staging_root="$(mktemp -d)"
  trap 'rm -rf "$staging_root"' RETURN

  for package_path in "${package_paths[@]}"; do
    cp "$package_path" "$staging_root/"
  done
  copy_legal_files "$staging_root"

  mkdir -p "$(dirname "$archive_path")"
  rm -f "$archive_path"
  tar -czf "$archive_path" -C "$staging_root" "${package_names[@]}" NOTICE LICENSE
  echo "Created $archive_path"
}

create_ps_cmdlets_archive() {
  local source_root="$1"
  local archive_path="$2"

  if [[ ! -d "$source_root" ]]; then
    echo "ERROR: PowerShell cmdlet source root not found: $source_root"
    exit 1
  fi

  if [[ ! -f "$INSTALL_MODULE_SCRIPT" ]]; then
    echo "ERROR: InstallModule.ps1 was not found: $INSTALL_MODULE_SCRIPT"
    exit 1
  fi

  mapfile -t package_names < <(python3 - "$source_root" <<'PY'
import os
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

source_root = sys.argv[1]
packages = {name: os.path.join(source_root, name) for name in os.listdir(source_root) if name.endswith('.nupkg')}
rollups = sorted(name for name in packages if re.match(r'^Az\.\d', name))
if len(rollups) != 1:
    raise SystemExit(f"ERROR: Expected exactly one Az.<version>.nupkg rollup package under {source_root}; found {len(rollups)}.")

rollup = rollups[0]
selected = [rollup]

with zipfile.ZipFile(packages[rollup]) as archive:
    nuspec_names = [name for name in archive.namelist() if name.endswith('.nuspec')]
    if not nuspec_names:
        raise SystemExit(f"ERROR: Rollup package has no nuspec: {rollup}")
    root = ET.fromstring(archive.read(nuspec_names[0]))

namespace = ''
if root.tag.startswith('{'):
    namespace = root.tag.split('}', 1)[0] + '}'

dependencies_root = root.find(f'.//{namespace}dependencies')
if dependencies_root is None:
    raise SystemExit(f"ERROR: Rollup package has no dependencies: {rollup}")

dependencies = []
for dependency in dependencies_root.iter(f'{namespace}dependency'):
    dependency_id = dependency.attrib.get('id')
    dependency_version = dependency.attrib.get('version')
    if dependency_id and dependency_id.startswith('Az.'):
        dependencies.append((dependency_id, dependency_version))

if not dependencies:
    raise SystemExit(f"ERROR: Rollup package has no Az.* dependencies: {rollup}")

for dependency_id, dependency_version in dependencies:
    package_name = f'{dependency_id}.{dependency_version}.nupkg'
    if package_name not in packages:
        raise SystemExit(f"ERROR: Rollup dependency package is missing: {package_name}")
    selected.append(package_name)

for name in selected:
    print(name)
PY
  )

  local staging_root
  staging_root="$(mktemp -d)"
  trap 'rm -rf "$staging_root"' RETURN

  for package_name in "${package_names[@]}"; do
    cp "$source_root/$package_name" "$staging_root/"
  done
  cp "$INSTALL_MODULE_SCRIPT" "$staging_root/InstallModule.ps1"
  copy_legal_files "$staging_root"

  mkdir -p "$(dirname "$archive_path")"
  rm -f "$archive_path"
  tar -czf "$archive_path" -C "$staging_root" "${package_names[@]}" InstallModule.ps1 NOTICE LICENSE
  echo "Created $archive_path"
}

mkdir -p "$OUTPUT_ROOT"
create_archive "$CLI_EXTENSIONS_ROOT" 'azcli.extensions.*.nupkg' "$OUTPUT_ROOT/$CLI_EXTENSIONS_ARCHIVE_STEM-$ARTIFACT_VERSION_SEGMENT.tar.gz" 'CLI extension' true
create_ps_cmdlets_archive "$PSGALLERY_ROOT" "$OUTPUT_ROOT/$PS_CMDLETS_ARCHIVE_STEM-$ARTIFACT_VERSION_SEGMENT.tar.gz"