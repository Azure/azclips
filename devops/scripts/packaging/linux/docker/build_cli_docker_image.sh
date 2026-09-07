#!/usr/bin/env bash
set -euo pipefail

# build_cli_docker_image.sh
# Builds and saves a Docker image for the CLI. The image is built on top of
# the self-contained publish output already produced by
# build_linux_tarball.sh (consumed here as an existing tarball), so the
# Docker build stage does not re-publish the CLI.
#
# Required env vars:
#   TARBALL_PATH   Path to the CLI tarball (*.tar.gz) to extract into the image.
#
# Optional env vars (with defaults):
#   RID             linux-x64 | linux-arm64                        (default: linux-x64)
#   PLATFORM        Docker platform, e.g. linux/amd64|linux/arm64  (default: linux/amd64)
#   PACKAGE_PROFILE lean | full                                    (default: lean)
#   BASE_IMAGE      Base image reference                            (default: mcr.microsoft.com/dotnet/runtime-deps:10.0-noble)
#   IMAGE_VARIANT   Label used in tag/output naming                 (default: noble)
#   IMAGE_REPOSITORY Image repository name                          (default: azclips)
#   ARTIFACT_VERSION Version used in the image tag                  (default: 0.0.0)
#   OUT_ROOT        Root output directory                           (default: <repo>/devops/out/docker)
#   DOCKERFILE      Path to the Dockerfile                          (default: <packaging>/linux/docker/Dockerfile.cli)
#   BUILDX_BUILDER  Docker-container Buildx builder name            (default: azclips-builder)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"

RID="${RID:-linux-x64}"
PLATFORM="${PLATFORM:-linux/amd64}"
PACKAGE_PROFILE="${PACKAGE_PROFILE:-lean}"
BASE_IMAGE="${BASE_IMAGE:-mcr.microsoft.com/dotnet/runtime-deps:10.0-noble}"
IMAGE_VARIANT="${IMAGE_VARIANT:-noble}"
IMAGE_REPOSITORY="${IMAGE_REPOSITORY:-azclips}"
ARTIFACT_VERSION="${ARTIFACT_VERSION:-0.0.0}"
OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/docker}"
DOCKERFILE="${DOCKERFILE:-$PACKAGING_ROOT/linux/docker/Dockerfile.cli}"
BUILDX_BUILDER="${BUILDX_BUILDER:-azclips-builder}"
TARBALL_PATH="${TARBALL_PATH:?ERROR: TARBALL_PATH must point to an existing CLI tarball}"

if [[ ! -f "$TARBALL_PATH" ]]; then
  echo "ERROR: Tarball not found at $TARBALL_PATH"
  exit 1
fi

if [[ ! -f "$DOCKERFILE" ]]; then
  echo "ERROR: Dockerfile not found at $DOCKERFILE"
  exit 1
fi

case "$RID" in
  linux-x64)
    ARCH_LABEL=amd64
    EXPECTED_PLATFORM=linux/amd64
    ;;
  linux-arm64)
    ARCH_LABEL=arm64
    EXPECTED_PLATFORM=linux/arm64
    ;;
  *)
    echo "ERROR: Unsupported RID '$RID'. Expected 'linux-x64' or 'linux-arm64'."
    exit 1
    ;;
esac

if [[ "$PLATFORM" != "$EXPECTED_PLATFORM" ]]; then
  echo "ERROR: RID '$RID' requires PLATFORM '$EXPECTED_PLATFORM', but got '$PLATFORM'."
  exit 1
fi

case "$PACKAGE_PROFILE" in
  lean|full) ;;
  *)
    echo "ERROR: Unsupported PACKAGE_PROFILE '$PACKAGE_PROFILE'. Expected 'lean' or 'full'."
    exit 1
    ;;
esac

BUILD_CONTEXT="$OUT_ROOT/context/$IMAGE_VARIANT-$PACKAGE_PROFILE-$ARCH_LABEL"
CLI_EXTRACT_DIR="$BUILD_CONTEXT/cli"
rm -rf "$BUILD_CONTEXT"
mkdir -p "$CLI_EXTRACT_DIR"

echo "Extracting $TARBALL_PATH -> $CLI_EXTRACT_DIR"
tar -xzf "$TARBALL_PATH" -C "$CLI_EXTRACT_DIR"
cp "$REPO_ROOT/LICENSE" "$CLI_EXTRACT_DIR/LICENSE"
cp "$REPO_ROOT/NOTICE" "$CLI_EXTRACT_DIR/NOTICE"

IMAGE_TAG="$IMAGE_REPOSITORY:$ARTIFACT_VERSION-$IMAGE_VARIANT-$PACKAGE_PROFILE-$ARCH_LABEL"
SAVE_DIR="$OUT_ROOT/Release/docker"
mkdir -p "$SAVE_DIR"
SANITIZED_REPOSITORY="${IMAGE_REPOSITORY//\//-}"
SAVE_PATH="$SAVE_DIR/${SANITIZED_REPOSITORY}-$ARTIFACT_VERSION-$IMAGE_VARIANT-$PACKAGE_PROFILE-$ARCH_LABEL.tar"

echo "Building docker image $IMAGE_TAG (platform=$PLATFORM, base=$BASE_IMAGE)"
rm -f "$SAVE_PATH"
if ! docker buildx inspect "$BUILDX_BUILDER" >/dev/null 2>&1; then
  docker buildx create --name "$BUILDX_BUILDER" --driver docker-container
fi
docker buildx inspect "$BUILDX_BUILDER" --bootstrap
docker buildx build \
  --builder "$BUILDX_BUILDER" \
  --platform "$PLATFORM" \
  --build-arg BASE_IMAGE="$BASE_IMAGE" \
  --file "$DOCKERFILE" \
  --tag "$IMAGE_TAG" \
  --output "type=docker,dest=$SAVE_PATH,compression=gzip,compression-level=9,force-compression=true" \
  "$BUILD_CONTEXT"

echo "Loading exported image from $SAVE_PATH"
docker load --input "$SAVE_PATH"

echo "Created $SAVE_PATH"
echo "##vso[task.setvariable variable=DockerImageTag]$IMAGE_TAG"
echo "##vso[task.setvariable variable=DockerImageTarPath]$SAVE_PATH"
