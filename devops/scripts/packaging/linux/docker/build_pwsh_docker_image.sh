#!/usr/bin/env bash
set -euo pipefail

# build_pwsh_docker_image.sh
# Builds and saves a Docker image containing PowerShell and pre-packaged Azclips
# PowerShell modules from the PSModules pipeline artifact.
#
# Required env vars:
#   MODULE_REPO_ROOT  Path to local-repo directory containing Az.*.nupkg
#
# Optional env vars:
#   PLATFORM                 linux/amd64|linux/arm64          (default: linux/amd64)
#   BASE_IMAGE               Final Microsoft runtime image   (default: mcr.microsoft.com/dotnet/runtime:10.0-azurelinux3.0)
#   POWERSHELL_BUILDER_IMAGE Microsoft SDK builder image     (default: mcr.microsoft.com/dotnet/sdk:10.0-azurelinux3.0)
#   IMAGE_VARIANT            Label used in output naming     (default: azurelinux3)
#   IMAGE_REPOSITORY  Image repository name            (default: azclips-powershell)
#   ARTIFACT_VERSION  Version used in image tag        (default: 0.0.0)
#   OUT_ROOT          Root output directory            (default: <repo>/devops/out/docker)
#   DOCKERFILE        Path to Dockerfile               (default: <packaging>/linux/docker/Dockerfile.pwsh)
#   BUILDX_BUILDER    Docker-container Buildx builder  (default: azclips-builder)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"

PLATFORM="${PLATFORM:-linux/amd64}"
BASE_IMAGE="${BASE_IMAGE:-mcr.microsoft.com/dotnet/runtime:10.0-azurelinux3.0}"
POWERSHELL_BUILDER_IMAGE="${POWERSHELL_BUILDER_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0-azurelinux3.0}"
IMAGE_VARIANT="${IMAGE_VARIANT:-azurelinux3}"
IMAGE_REPOSITORY="${IMAGE_REPOSITORY:-azclips-powershell}"
ARTIFACT_VERSION="${ARTIFACT_VERSION:-0.0.0}"
OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/docker}"
DOCKERFILE="${DOCKERFILE:-$PACKAGING_ROOT/linux/docker/Dockerfile.pwsh}"
BUILDX_BUILDER="${BUILDX_BUILDER:-azclips-builder}"
MODULE_REPO_ROOT="${MODULE_REPO_ROOT:?ERROR: MODULE_REPO_ROOT must point to a PS local-repo directory}"

if [[ ! -d "$MODULE_REPO_ROOT" ]]; then
  echo "ERROR: Module repository directory not found at $MODULE_REPO_ROOT"
  exit 1
fi

if [[ ! -f "$DOCKERFILE" ]]; then
  echo "ERROR: Dockerfile not found at $DOCKERFILE"
  exit 1
fi

case "$PLATFORM" in
  linux/amd64) ARCH_LABEL=amd64 ;;
  linux/arm64) ARCH_LABEL=arm64 ;;
  *)
    echo "ERROR: Unsupported PLATFORM '$PLATFORM'. Expected 'linux/amd64' or 'linux/arm64'."
    exit 1
    ;;
esac

BUILD_CONTEXT="$OUT_ROOT/context/pwsh-$IMAGE_VARIANT-$ARCH_LABEL"
LOCAL_REPO_CONTEXT="$BUILD_CONTEXT/local-repo"
rm -rf "$BUILD_CONTEXT"
mkdir -p "$LOCAL_REPO_CONTEXT"

cp -a "$MODULE_REPO_ROOT/." "$LOCAL_REPO_CONTEXT/"
cp "$PACKAGING_ROOT/powershell/InstallModule.ps1" "$BUILD_CONTEXT/InstallModule.ps1"
cp "$REPO_ROOT/LICENSE" "$BUILD_CONTEXT/LICENSE"
cp "$REPO_ROOT/NOTICE" "$BUILD_CONTEXT/NOTICE"

IMAGE_TAG="$IMAGE_REPOSITORY:$ARTIFACT_VERSION-$IMAGE_VARIANT-$ARCH_LABEL"
SAVE_DIR="$OUT_ROOT/Release/docker"
mkdir -p "$SAVE_DIR"
SANITIZED_REPOSITORY="${IMAGE_REPOSITORY//\//-}"
SAVE_PATH="$SAVE_DIR/${SANITIZED_REPOSITORY}-$ARTIFACT_VERSION-$IMAGE_VARIANT-$ARCH_LABEL.tar"

echo "Building docker image $IMAGE_TAG (platform=$PLATFORM, base=$BASE_IMAGE, builder=$POWERSHELL_BUILDER_IMAGE)"
rm -f "$SAVE_PATH"
if ! docker buildx inspect "$BUILDX_BUILDER" >/dev/null 2>&1; then
  docker buildx create --name "$BUILDX_BUILDER" --driver docker-container
fi
docker buildx inspect "$BUILDX_BUILDER" --bootstrap
docker buildx build \
  --builder "$BUILDX_BUILDER" \
  --platform "$PLATFORM" \
  --build-arg BASE_IMAGE="$BASE_IMAGE" \
  --build-arg POWERSHELL_BUILDER_IMAGE="$POWERSHELL_BUILDER_IMAGE" \
  --file "$DOCKERFILE" \
  --tag "$IMAGE_TAG" \
  --output "type=docker,dest=$SAVE_PATH,compression=gzip,compression-level=9,force-compression=true" \
  "$BUILD_CONTEXT"

echo "Loading exported image from $SAVE_PATH"
docker load --input "$SAVE_PATH"

echo "Created $SAVE_PATH"
echo "##vso[task.setvariable variable=DockerImageTag]$IMAGE_TAG"
echo "##vso[task.setvariable variable=DockerImageTarPath]$SAVE_PATH"
