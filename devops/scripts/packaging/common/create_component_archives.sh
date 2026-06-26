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
Usage: create_component_archives.sh --cli-extensions-root DIR --psgallery-root DIR --output-root DIR [--artifact-version VERSION] [--version-source-root DIR]

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
    --psgallery-root)
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
  echo "ERROR: --cli-extensions-root, --psgallery-root, and --output-root are required."
  usage
  exit 1
fi

ARTIFACT_VERSION_SUFFIX="${ARTIFACT_VERSION_SUFFIX-$(read_artifact_config artifact_version_suffix preview)}"
CLI_ARCHIVE_STEM="${CLI_ARCHIVE_STEM:-$(read_artifact_config cli_archive_stem azcli)}"
CLI_LEAN_ARCHIVE_STEM="${CLI_LEAN_ARCHIVE_STEM:-$(read_artifact_config cli_lean_archive_stem azcli-lean)}"
CLI_EXTENSIONS_ARCHIVE_STEM="${CLI_EXTENSIONS_ARCHIVE_STEM:-$(read_artifact_config cli_extensions_archive_stem azcli-extensions)}"
PS_CMDLETS_ARCHIVE_STEM="${PS_CMDLETS_ARCHIVE_STEM:-$(read_artifact_config ps_cmdlets_archive_stem azps-cmdlets)}"

resolve_config_label() {
  local key="$1"
  local default_value="$2"
  read_artifact_config "$key" "$default_value"
}

infer_artifact_version() {
  local source_root="$1"
  local stems=("$CLI_LEAN_ARCHIVE_STEM" "$CLI_ARCHIVE_STEM")
  local labels=(
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

  if [[ ! -d "$source_root" ]]; then
    echo "ERROR: $description source root not found: $source_root"
    exit 1
  fi

  mapfile -t package_paths < <(find "$source_root" -maxdepth 1 -type f -name "$package_glob" | sort)
  if [[ ${#package_paths[@]} -eq 0 ]]; then
    echo "ERROR: No $description packages matched '$package_glob' under $source_root"
    exit 1
  fi

  package_names=()
  for package_path in "${package_paths[@]}"; do
    package_names+=("$(basename "$package_path")")
  done

  mkdir -p "$(dirname "$archive_path")"
  rm -f "$archive_path"
  tar -czf "$archive_path" -C "$source_root" "${package_names[@]}"
  echo "Created $archive_path"
}

mkdir -p "$OUTPUT_ROOT"
create_archive "$CLI_EXTENSIONS_ROOT" 'azcli.extensions.*.nupkg' "$OUTPUT_ROOT/$CLI_EXTENSIONS_ARCHIVE_STEM-$ARTIFACT_VERSION_SEGMENT.tar.gz" 'CLI extension'
create_archive "$PSGALLERY_ROOT" 'Az.*.nupkg' "$OUTPUT_ROOT/$PS_CMDLETS_ARCHIVE_STEM-$ARTIFACT_VERSION_SEGMENT.tar.gz" 'PowerShell cmdlet'