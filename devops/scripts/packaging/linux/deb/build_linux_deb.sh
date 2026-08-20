#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/linux}"
RID="${RID:-linux-x64}"
PACKAGE_PROFILE="${PACKAGE_PROFILE:-full}"
PAYLOAD_DIR="${PAYLOAD_DIR:-$OUT_ROOT/publish/cli/$RID}"
PAYLOAD_MANIFEST="${PAYLOAD_MANIFEST:-$OUT_ROOT/manifests/cli/$RID/$PACKAGE_PROFILE/payload-manifest.json}"
PAYLOAD_VALIDATOR="$PACKAGING_ROOT/linux/common/validate_linux_payload.sh"
NFPM_CONFIG="${NFPM_CONFIG:-$SCRIPT_DIR/nfpm.yaml}"
NFPM_VERSION="${NFPM_VERSION:-2.46.3}"
PACKAGE_VERSION="${PACKAGE_VERSION:-$(dotnet msbuild src/CLI/Core.Cli.csproj -getProperty:Version -nologo | tail -n 1)}"

case "$RID" in
  linux-x64) DEB_ARCH="amd64" ;;
  *)
    echo "ERROR: Unsupported RID '$RID'. The first DEB slice supports only linux-x64." >&2
    exit 1
    ;;
esac

if ! command -v nfpm >/dev/null 2>&1; then
  echo "ERROR: nFPM is required to build the DEB." >&2
  exit 1
fi
ACTUAL_NFPM_VERSION="$(nfpm --version | sed -n 's/^GitVersion:[[:space:]]*//p')"
if [[ "$ACTUAL_NFPM_VERSION" != "$NFPM_VERSION" ]]; then
  echo "ERROR: nFPM $NFPM_VERSION is required; found '$ACTUAL_NFPM_VERSION'." >&2
  exit 1
fi
if ! command -v envsubst >/dev/null 2>&1; then
  echo "ERROR: envsubst is required to render the nFPM configuration." >&2
  exit 1
fi

for required_file in "$NFPM_CONFIG" "$SCRIPT_DIR/preinstall.sh" "$PAYLOAD_VALIDATOR" "$PAYLOAD_MANIFEST"; do
  if [[ ! -f "$required_file" ]]; then
    echo "ERROR: Required file was not found: $required_file" >&2
    exit 1
  fi
done

if [[ "$PACKAGE_PROFILE" != "full" ]]; then
  echo "ERROR: Native packages require PACKAGE_PROFILE=full." >&2
  exit 1
fi

bash "$PAYLOAD_VALIDATOR" --payload-dir "$PAYLOAD_DIR" --manifest "$PAYLOAD_MANIFEST"

DEB_PATH="${DEB_PATH:-$OUT_ROOT/Release/deb/azcli_${PACKAGE_VERSION}-1~noble_${DEB_ARCH}.deb}"
mkdir -p "$(dirname "$DEB_PATH")"
rm -f "$DEB_PATH"

export DEB_ARCH PACKAGE_VERSION PAYLOAD_DIR SCRIPT_DIR
RENDERED_NFPM_CONFIG="$(mktemp)"
trap 'rm -f "$RENDERED_NFPM_CONFIG"' EXIT
envsubst "\$DEB_ARCH \$PACKAGE_VERSION \$PAYLOAD_DIR \$SCRIPT_DIR" < "$NFPM_CONFIG" > "$RENDERED_NFPM_CONFIG"
nfpm package --config "$RENDERED_NFPM_CONFIG" --packager deb --target "$DEB_PATH"
echo "Created $DEB_PATH"