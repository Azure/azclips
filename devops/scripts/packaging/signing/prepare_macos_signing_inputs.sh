#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "Usage: $0 <source-root> <work-root> <architecture> <rid>"
  exit 2
fi

source_root="$1"
work_root="$2"
current_arch="$3"
current_rid="$4"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
entitlements_source="$PACKAGING_ROOT/macos/signing/azclips-runtime.entitlements.plist"
tarball_dir="$work_root/tarballs"
contents_dir="$work_root/contents"
mapping_tsv="$work_root/file-mapping.tsv"
to_be_signed_dir="$work_root/toBeSigned"
hashes_tsv="$work_root/zip-hashes-before.tsv"
asset_arch_label="macos-$current_arch"
if [[ "$current_arch" == "x64" ]]; then
  asset_arch_label="macos-x86_64"
fi

rm -rf "$tarball_dir" "$contents_dir" "$to_be_signed_dir"
rm -f "$mapping_tsv" "$hashes_tsv" "$work_root/azclips-runtime.entitlements.plist"
mkdir -p "$tarball_dir" "$contents_dir"
: > "$mapping_tsv"
if [[ ! -f "$entitlements_source" ]]; then
  echo "ERROR: macOS signing entitlements file was not found: $entitlements_source"
  exit 1
fi
cp "$entitlements_source" "$work_root/azclips-runtime.entitlements.plist"

verify_dotnet_entitlements() {
  local candidate="$1"
  local entitlements
  entitlements=$(codesign -d --entitlements - "$candidate" 2>&1 || true)
  for entitlement in \
    'com.apple.security.cs.allow-jit' \
    'com.apple.security.cs.allow-unsigned-executable-memory' \
    'com.apple.security.cs.disable-library-validation'; do
    if ! printf '%s\n' "$entitlements" | grep -q "$entitlement"; then
      echo "ERROR: Required .NET entitlement '$entitlement' missing after pre-sign: $candidate"
      return 1
    fi
  done
}

requires_dotnet_entitlements() {
  case "$(basename "$1")" in
    az|azclips-agent)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

is_recognized_code_bundle() {
  local candidate="$1"

  case "$(basename "$candidate")" in
    *.framework)
      [[ -f "$candidate/Resources/Info.plist" ]] ||
        [[ -n "$(find "$candidate/Versions" -mindepth 3 -maxdepth 3 -path '*/Resources/Info.plist' -print -quit 2>/dev/null)" ]]
      ;;
    *.app|*.xpc|*.appex|*.plugin|*.bundle)
      [[ -f "$candidate/Contents/Info.plist" ]]
      ;;
    *)
      return 1
      ;;
  esac
}

path_contains_recognized_code_bundle() {
  local content_root="$1"
  local relative_path="$2"
  local candidate="$content_root"
  local component

  while [[ -n "$relative_path" ]]; do
    component="${relative_path%%/*}"
    candidate="$candidate/$component"
    if is_recognized_code_bundle "$candidate"; then
      return 0
    fi
    if [[ "$relative_path" != */* ]]; then
      break
    fi
    relative_path="${relative_path#*/}"
  done

  return 1
}

create_zip_name() {
  local base_name="$1"
  local relative_path="$2"
  local safe_relative path_hash
  safe_relative=$(printf '%s' "$relative_path" | sed 's#[/\\]#__#g; s#[^A-Za-z0-9._-]#_#g')
  path_hash=$(printf '%s' "$relative_path" | shasum -a 256 | awk '{print substr($1, 1, 16)}')
  printf '%s__%s__%s.zip\n' "$base_name" "$safe_relative" "$path_hash"
}

append_mapping() {
  local zip_name="$1"
  local base_name="$2"
  local relative_path="$3"
  local item_type="$4"

  if awk -F '\t' -v zip_name="$zip_name" '$1 == zip_name { found = 1 } END { exit !found }' "$mapping_tsv"; then
    echo "ERROR: Duplicate macOS signing ZIP name generated: $zip_name"
    exit 1
  fi
  printf '%s\t%s\t%s\t%s\n' "$zip_name" "$base_name" "$relative_path" "$item_type" >> "$mapping_tsv"
}

raw_mac_assets=()
while IFS= read -r asset; do
  raw_mac_assets+=("$asset")
done < <(find "$source_root" -type f \( -name '*mac*.tar.gz' -o -name '*macos*.tar.gz' -o -name '*osx*.tar.gz' -o -name '*darwin*.tar.gz' \) | sort)

mac_assets=()
for asset in "${raw_mac_assets[@]}"; do
  name=$(basename "$asset" | tr '[:upper:]' '[:lower:]')
  if [[ "$name" == *"$current_rid"* || "$name" == *"$asset_arch_label"* || "$name" == *"darwin-$current_arch"* ]]; then
    mac_assets+=("$asset")
  fi
done

if [[ ${#mac_assets[@]} -eq 0 ]]; then
  echo "ERROR: No macOS release artifacts matched architecture $current_arch ($current_rid) under $source_root"
  exit 1
fi

echo "macOS architecture selected: $current_arch ($current_rid)"
printf 'macOS release artifacts (%s):\n' "${#mac_assets[@]}"
printf '  %s\n' "${mac_assets[@]}"

total_binaries=0
total_bundles=0
for asset in "${mac_assets[@]}"; do
  file_name=$(basename "$asset")
  base_name="${file_name%.tar.gz}"
  content_root="$contents_dir/$base_name"

  cp "$asset" "$tarball_dir/$file_name"
  mkdir -p "$content_root"
  tar -xzf "$asset" -C "$content_root"

  unknown_code_containers=0
  while IFS= read -r -d '' info_plist; do
    bundle_candidate="${info_plist%/Contents/Info.plist}"
    if is_recognized_code_bundle "$bundle_candidate"; then
      continue
    fi

    while IFS= read -r -d '' candidate_file; do
      if file "$candidate_file" | grep -q 'Mach-O'; then
        echo "ERROR: Unrecognized macOS code container contains Mach-O code: $bundle_candidate"
        unknown_code_containers=1
        break
      fi
    done < <(find "$bundle_candidate" -type f -print0)
  done < <(find "$content_root" -type f -path '*/Contents/Info.plist' -print0)

  if [[ "$unknown_code_containers" -ne 0 ]]; then
    echo 'ERROR: Refusing to sign a payload containing unrecognized macOS code containers.'
    exit 1
  fi

  while IFS= read -r -d '' bundle; do
    if ! is_recognized_code_bundle "$bundle"; then
      continue
    fi

    relative_path="${bundle#"$content_root"/}"
    parent_path=''
    if [[ "$relative_path" == */* ]]; then
      parent_path="${relative_path%/*}"
    fi
    if [[ -n "$parent_path" ]] && path_contains_recognized_code_bundle "$content_root" "$parent_path"; then
      continue
    fi

    zip_name=$(create_zip_name "$base_name" "$relative_path")
    append_mapping "$zip_name" "$base_name" "$relative_path" bundle
    total_bundles=$((total_bundles + 1))
  done < <(find "$content_root" -type d \( \
    -name '*.app' -o \
    -name '*.framework' -o \
    -name '*.xpc' -o \
    -name '*.appex' -o \
    -name '*.plugin' -o \
    -name '*.bundle' \
  \) -print0)

  while IFS= read -r -d '' binary; do
    if ! file "$binary" | grep -q 'Mach-O'; then
      continue
    fi

    relative_path="${binary#"$content_root"/}"
    if path_contains_recognized_code_bundle "$content_root" "$relative_path"; then
      continue
    fi
    if requires_dotnet_entitlements "$binary"; then
      chmod +x "$binary"
      codesign --force --sign - --options runtime --entitlements "$entitlements_source" "$binary"
      verify_dotnet_entitlements "$binary"
    fi

    zip_name=$(create_zip_name "$base_name" "$relative_path")
    append_mapping "$zip_name" "$base_name" "$relative_path" file
    total_binaries=$((total_binaries + 1))
  done < <(find "$content_root" -type f -print0)
done

if [[ $((total_binaries + total_bundles)) -eq 0 ]]; then
  echo 'ERROR: No Mach-O binaries or recognized code bundles were found in the selected macOS artifacts.'
  exit 1
fi

mkdir -p "$to_be_signed_dir"
: > "$hashes_tsv"

total_signing_zips=0
while IFS=$'\t' read -r zip_name base_name relative_path item_type; do
  [[ -z "$base_name" ]] && continue
  signing_path="$contents_dir/$base_name/$relative_path"
  zip_path="$to_be_signed_dir/$zip_name"

  case "$item_type" in
    file)
      [[ -f "$signing_path" ]] || { echo "ERROR: Signing input file not found: $signing_path"; exit 1; }
      ;;
    bundle)
      [[ -d "$signing_path" ]] || { echo "ERROR: Signing input bundle not found: $signing_path"; exit 1; }
      ;;
    *)
      echo "ERROR: Unsupported signing input type '$item_type' for $base_name/$relative_path"
      exit 1
      ;;
  esac

  (
    cd "$(dirname "$signing_path")"
    zip -r -y -q "$zip_path" "$(basename "$signing_path")"
  )
  zip_hash=$(shasum -a 256 "$zip_path" | awk '{print $1}')
  printf '%s\t%s\n' "$zip_name" "$zip_hash" >> "$hashes_tsv"
  total_signing_zips=$((total_signing_zips + 1))
done < "$mapping_tsv"

echo "Prepared $total_binaries Mach-O binaries and $total_bundles code bundles in $total_signing_zips symlink-preserving ZIPs for Developer ID signing."