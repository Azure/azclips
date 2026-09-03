#!/usr/bin/env bash
set -euo pipefail

IMAGE_TAG="${IMAGE_TAG:?ERROR: IMAGE_TAG is required}"
IMAGE_FLAVOR="${IMAGE_FLAVOR:?ERROR: IMAGE_FLAVOR is required}"
PLATFORM="${PLATFORM:?ERROR: PLATFORM is required}"

expected_arch="${PLATFORM#linux/}"
actual_arch="$(docker image inspect "$IMAGE_TAG" --format '{{.Architecture}}')"
if [[ "$actual_arch" != "$expected_arch" ]]; then
  echo "ERROR: Expected image architecture '$expected_arch', found '$actual_arch'."
  exit 1
fi

case "$IMAGE_FLAVOR" in
  cli)
    docker run --rm --entrypoint /bin/sh "$IMAGE_TAG" -c 'test -f /app/LICENSE && test -f /app/NOTICE'
    version_output="$(docker run --rm "$IMAGE_TAG" --version)"
    printf '%s\n' "$version_output"
    if ! grep -Eq '^AzCli[[:space:]]+[^[:space:]]+' <<<"$version_output"; then
      echo "ERROR: CLI image did not report an AzCli version."
      exit 1
    fi
    ;;
  pwsh)
    docker run --rm --entrypoint /bin/sh "$IMAGE_TAG" -c 'test -f /usr/share/doc/azclips/LICENSE && test -f /usr/share/doc/azclips/NOTICE'
    docker run --rm "$IMAGE_TAG" -NoLogo -NoProfile -Command '$ErrorActionPreference = "Stop"; $powerShellVersion = $PSVersionTable.PSVersion.ToString(); Import-Module Az; $azVersion = (Get-Module Az -ErrorAction Stop).Version.ToString(); $command = Get-Command Connect-AzAccount -ErrorAction Stop; Write-Output "PowerShell=$powerShellVersion"; Write-Output "Az=$azVersion"; Write-Output "Command=$($command.Name)"'
    ;;
  *)
    echo "ERROR: Unsupported IMAGE_FLAVOR '$IMAGE_FLAVOR'. Expected 'cli' or 'pwsh'."
    exit 1
    ;;
esac
