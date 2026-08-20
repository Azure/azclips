#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
cd "$REPO_ROOT"

OUT_ROOT="${OUT_ROOT:-$REPO_ROOT/devops/out/linux}"
RID="${RID:-linux-x64}"
PACKAGE_PROFILE="${PACKAGE_PROFILE:-full}"
PACKAGE_VERSION="${PACKAGE_VERSION:-$(dotnet msbuild src/CLI/Core.Cli.csproj -getProperty:Version -nologo | tail -n 1)}"
DEB_PATH="${DEB_PATH:-$OUT_ROOT/Release/deb/azcli_${PACKAGE_VERSION}-1~noble_amd64.deb}"
PAYLOAD_MANIFEST="${PAYLOAD_MANIFEST:-$OUT_ROOT/manifests/cli/$RID/$PACKAGE_PROFILE/payload-manifest.json}"
PAYLOAD_VALIDATOR="$PACKAGING_ROOT/linux/common/validate_linux_payload.sh"

for required_file in "$DEB_PATH" "$PAYLOAD_MANIFEST" "$PAYLOAD_VALIDATOR"; do
  if [[ ! -f "$required_file" ]]; then
    echo "ERROR: Required file was not found: $required_file" >&2
    exit 1
  fi
done

assert_field() {
  local field="$1"
  local expected="$2"
  local actual
  actual="$(dpkg-deb --field "$DEB_PATH" "$field")"
  if [[ "$actual" != "$expected" ]]; then
    echo "ERROR: $field is '$actual'; expected '$expected'." >&2
    exit 1
  fi
}

assert_field Package azcli
assert_field Version "$PACKAGE_VERSION-1~noble"
assert_field Architecture amd64
assert_field Conflicts azure-cli

SIM_ROOT="${SIM_ROOT:-$REPO_ROOT/devops/sim/linux-deb}"
rm -rf "$SIM_ROOT"
mkdir -p "$SIM_ROOT"
dpkg-deb --extract "$DEB_PATH" "$SIM_ROOT/root"
dpkg-deb --control "$DEB_PATH" "$SIM_ROOT/control"

bash "$PAYLOAD_VALIDATOR" \
  --payload-dir "$SIM_ROOT/root/opt/azcli" \
  --manifest "$PAYLOAD_MANIFEST"

if [[ "$(readlink "$SIM_ROOT/root/usr/bin/az")" != "/opt/azcli/az" ]]; then
  echo "ERROR: /usr/bin/az does not target /opt/azcli/az." >&2
  exit 1
fi

cmp "$SIM_ROOT/root/opt/azcli/completions/az.completions.bash" \
  "$SIM_ROOT/root/usr/share/bash-completion/completions/az"
cmp "$SIM_ROOT/root/opt/azcli/completions/az.completions.zsh" \
  "$SIM_ROOT/root/usr/share/zsh/vendor-completions/_az"

if [[ ! -x "$SIM_ROOT/control/preinst" ]]; then
  echo "ERROR: The DEB preinstall script is missing or not executable." >&2
  exit 1
fi

EXTRACTED_AZ="$SIM_ROOT/root/opt/azcli/az"
mkdir -p "$SIM_ROOT/home/.azclips"
printf '%s\n' '{"agent":{"enabled":false},"telemetry":{"enabled":false}}' \
  > "$SIM_ROOT/home/.azclips/config.json"
(
  cd "$SIM_ROOT/home"
  HOME="$SIM_ROOT/home" "$EXTRACTED_AZ" version --output json >/dev/null
  HOME="$SIM_ROOT/home" "$EXTRACTED_AZ" --help >/dev/null
)

echo "Validated Ubuntu DEB: $DEB_PATH"