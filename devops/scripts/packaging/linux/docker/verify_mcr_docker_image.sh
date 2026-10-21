#!/usr/bin/env bash
set -euo pipefail

VERSION="${VERSION:?ERROR: VERSION is required}"
METADATA_ROOT="${METADATA_ROOT:?ERROR: METADATA_ROOT is required}"
SOURCE_REPOSITORY="${SOURCE_REPOSITORY:?ERROR: SOURCE_REPOSITORY is required}"
TAG_SUFFIX="${TAG_SUFFIX:?ERROR: TAG_SUFFIX is required}"
IMAGE_FLAVOR="${IMAGE_FLAVOR:?ERROR: IMAGE_FLAVOR is required}"
PLATFORM="${PLATFORM:?ERROR: PLATFORM is required}"
SMOKE_TEST_SCRIPT="${SMOKE_TEST_SCRIPT:?ERROR: SMOKE_TEST_SCRIPT is required}"
VERIFY_SCRIPT="${VERIFY_SCRIPT:?ERROR: VERIFY_SCRIPT is required}"
MCR_REGISTRY_SERVER="${MCR_REGISTRY_SERVER:-mcr.microsoft.com}"
MCR_POLL_ATTEMPTS="${MCR_POLL_ATTEMPTS:-2}"
MCR_POLL_INTERVAL_SECONDS="${MCR_POLL_INTERVAL_SECONDS:-120}"

# MCR serves public/ and unlisted/ ACR repositories without the image-type prefix.
if [[ ! "$SOURCE_REPOSITORY" =~ ^(public|unlisted)/(.+)$ ]]; then
  echo "ERROR: Repository '$SOURCE_REPOSITORY' must start with 'public/' or 'unlisted/' to be anonymously pullable from MCR."
  exit 1
fi
mcr_repository="${BASH_REMATCH[2]}"

manifest_tag="$VERSION-$TAG_SUFFIX"
mcr_tag="$MCR_REGISTRY_SERVER/$mcr_repository:$manifest_tag"

# Versioned tags are immutable and published once, so availability on MCR plus the
# version check in the smoke test is sufficient. The classic release makes a single attempt after its
# 10-minute delay; this adds one retry after a 2-minute gap (2 attempts total).
available=false
for ((attempt = 1; attempt <= MCR_POLL_ATTEMPTS; attempt++)); do
  if docker buildx imagetools inspect "$mcr_tag" >/dev/null 2>&1; then
    available=true
    break
  fi
  echo "Attempt $attempt/$MCR_POLL_ATTEMPTS: '$mcr_tag' is not available on MCR yet."
  if ((attempt < MCR_POLL_ATTEMPTS)); then
    sleep "$MCR_POLL_INTERVAL_SECONDS"
  fi
done

if [[ "$available" != "true" ]]; then
  echo "ERROR: MCR image '$mcr_tag' was not available after $MCR_POLL_ATTEMPTS attempts."
  exit 1
fi

expected_version=""
if [[ "$IMAGE_FLAVOR" == "cli" ]]; then
  expected_version="$VERSION"
else
  powershell_version_file="$METADATA_ROOT/powershell-version.txt"
  if [[ ! -f "$powershell_version_file" ]]; then
    echo "ERROR: Published PowerShell image metadata is missing '$powershell_version_file'."
    exit 1
  fi
  expected_version="$(<"$powershell_version_file")"
fi

IMAGE_TAG="$mcr_tag" \
IMAGE_FLAVOR="$IMAGE_FLAVOR" \
PLATFORM="$PLATFORM" \
EXPECTED_VERSION="$expected_version" \
SMOKE_TEST_SCRIPT="$SMOKE_TEST_SCRIPT" \
"$VERIFY_SCRIPT"
