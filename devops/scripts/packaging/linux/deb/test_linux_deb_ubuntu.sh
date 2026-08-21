#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"

IMAGE="${IMAGE:-ubuntu:24.04}"
DEB_PATH="${DEB_PATH:-}"
UPGRADE_DEB_PATH="${UPGRADE_DEB_PATH:-}"
SCENARIO="${SCENARIO:-all}"
CONTAINER_TEST_SCRIPT="$SCRIPT_DIR/test_linux_deb_scenarios.sh"

usage() {
  echo "Usage: $0 --deb-path <PATH> [--upgrade-deb-path <PATH>] [--image <IMAGE>] [--scenario <NAME>]" >&2
  echo "Scenarios: all, clean-install, unmanaged-command, classic-conflict, lifecycle" >&2
  exit 2
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --deb-path) DEB_PATH="$2"; shift 2 ;;
    --upgrade-deb-path) UPGRADE_DEB_PATH="$2"; shift 2 ;;
    --image) IMAGE="$2"; shift 2 ;;
    --scenario) SCENARIO="$2"; shift 2 ;;
    *) usage ;;
  esac
done

if [[ -z "$DEB_PATH" ]]; then
  echo "ERROR: --deb-path is required." >&2
  usage
fi

DEB_PATH="$(realpath "$DEB_PATH")"
for required_file in "$DEB_PATH" "$CONTAINER_TEST_SCRIPT"; do
  if [[ ! -f "$required_file" ]]; then
    echo "ERROR: Required file was not found: $required_file" >&2
    exit 1
  fi
done

case "$SCENARIO" in
  all) scenarios=(clean-install unmanaged-command classic-conflict lifecycle) ;;
  clean-install|unmanaged-command|classic-conflict|lifecycle) scenarios=("$SCENARIO") ;;
  *) usage ;;
esac

docker_args=(
  run --rm
  --mount "type=bind,src=$DEB_PATH,dst=/tmp/azcli.deb,readonly"
  --mount "type=bind,src=$CONTAINER_TEST_SCRIPT,dst=/tmp/test-linux-deb-scenarios.sh,readonly"
)

if [[ -n "$UPGRADE_DEB_PATH" ]]; then
  UPGRADE_DEB_PATH="$(realpath "$UPGRADE_DEB_PATH")"
  if [[ ! -f "$UPGRADE_DEB_PATH" ]]; then
    echo "ERROR: Upgrade DEB was not found: $UPGRADE_DEB_PATH" >&2
    exit 1
  fi
  docker_args+=(--mount "type=bind,src=$UPGRADE_DEB_PATH,dst=/tmp/azcli-upgrade.deb,readonly")
fi

cd "$REPO_ROOT"
for scenario in "${scenarios[@]}"; do
  echo "Running Ubuntu DEB scenario: $scenario"
  docker "${docker_args[@]}" "$IMAGE" \
    bash /tmp/test-linux-deb-scenarios.sh "$scenario"
done

echo "Validated Ubuntu DEB in Docker: $DEB_PATH"