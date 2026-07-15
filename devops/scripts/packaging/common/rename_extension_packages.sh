#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <package-root>" >&2
  exit 1
fi

PACKAGE_ROOT="$1"
PACKAGE_PREFIX="Microsoft.Azure.Azclips.Extensions."

if [[ ! -d "$PACKAGE_ROOT" ]]; then
  echo "ERROR: Package root not found: $PACKAGE_ROOT" >&2
  exit 1
fi

while IFS= read -r package_path; do
  package_name="$(basename "$package_path")"
  package_stem="${package_name%.nupkg}"
  extension_and_version="${package_stem#"$PACKAGE_PREFIX"}"
  extension_name="${extension_and_version%%.*}"
  version="${extension_and_version#"$extension_name."}"

  if [[ -z "$extension_name" || -z "$version" || "$extension_and_version" == "$version" ]]; then
    echo "ERROR: Could not infer extension name/version from $package_name" >&2
    exit 1
  fi

  release_name="azcli.extensions.${extension_name,,}.$version.nupkg"
  release_path="$(dirname "$package_path")/$release_name"

  rm -f "$release_path"
  mv "$package_path" "$release_path"
  echo "Renamed extension package: $package_name -> $release_name"
done < <(find "$PACKAGE_ROOT" -type f -name "$PACKAGE_PREFIX*.nupkg" | sort)