#!/usr/bin/env bash
set -euo pipefail

IMAGE_TAG="${IMAGE_TAG:?ERROR: IMAGE_TAG is required}"
IMAGE_FLAVOR="${IMAGE_FLAVOR:?ERROR: IMAGE_FLAVOR is required}"
PLATFORM="${PLATFORM:?ERROR: PLATFORM is required}"
EXPECTED_VERSION="${EXPECTED_VERSION:-}"
SMOKE_TEST_SCRIPT="${SMOKE_TEST_SCRIPT:?ERROR: SMOKE_TEST_SCRIPT is required}"
IMAGE_ARCHIVE="${IMAGE_ARCHIVE:-}"

if [[ -n "$IMAGE_ARCHIVE" ]]; then
  docker load --input "$IMAGE_ARCHIVE"
else
  docker pull --platform "$PLATFORM" "$IMAGE_TAG"
fi

actual_arch="$(docker image inspect "$IMAGE_TAG" --format '{{.Architecture}}')"
expected_arch="${PLATFORM#linux/}"
if [[ "$actual_arch" != "$expected_arch" ]]; then
  echo "ERROR: Expected '$IMAGE_TAG' architecture '$expected_arch', found '$actual_arch'."
  exit 1
fi

IMAGE_TAG="$IMAGE_TAG" \
IMAGE_FLAVOR="$IMAGE_FLAVOR" \
PLATFORM="$PLATFORM" \
EXPECTED_VERSION="$EXPECTED_VERSION" \
"$SMOKE_TEST_SCRIPT"
