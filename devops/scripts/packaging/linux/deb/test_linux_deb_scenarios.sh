#!/usr/bin/env bash
set -euo pipefail

DEB_PATH=/tmp/azcli.deb
UPGRADE_DEB_PATH=/tmp/azcli-upgrade.deb
HOME_ROOT=/tmp/home

apt_update() {
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -qq
}

assert_installed_layout() {
  test "$(dpkg-query -S /usr/bin/az)" = "azcli: /usr/bin/az"
  test "$(readlink /usr/bin/az)" = "/opt/azcli/az"
  test -f /usr/share/bash-completion/completions/az
  test -f /usr/share/zsh/vendor-completions/_az
  test -x /opt/azcli/azclips-agent
}

configure_test_home() {
  mkdir -p "$HOME_ROOT/.azclips"
  printf '%s\n' '{"agent":{"enabled":false},"telemetry":{"enabled":false}}' \
    > "$HOME_ROOT/.azclips/config.json"
}

clean_install() {
  apt_update
  apt-get install -y -qq "$DEB_PATH"
  dpkg-query -W -f='${Package} ${Version} ${Architecture} ${Status}\n' azcli
  assert_installed_layout
  configure_test_home
  (
    cd "$HOME_ROOT"
    HOME="$HOME_ROOT" az version --output json
    HOME="$HOME_ROOT" az --help >/dev/null
  )
  test ! -e "$HOME_ROOT/.azure"
  /opt/azcli/azclips-agent --help >/dev/null
  echo "PASS: clean Noble APT install and runtime smoke"
}

unmanaged_command() {
  printf 'unmanaged az\n' > /usr/bin/az
  chmod +x /usr/bin/az
  apt_update
  if apt-get install -y "$DEB_PATH" >/tmp/install.log 2>&1; then
    cat /tmp/install.log
    echo "ERROR: Installation unexpectedly replaced unmanaged /usr/bin/az." >&2
    exit 1
  fi
  grep -F "ERROR: /usr/bin/az already exists and is not owned by a Debian package." /tmp/install.log
  test "$(cat /usr/bin/az)" = "unmanaged az"
  package_status="$(dpkg-query -W -f='${Status}' azcli 2>/dev/null || true)"
  if [[ "$package_status" == "install ok installed" ]]; then
    echo "ERROR: azcli was installed after the rejected collision." >&2
    exit 1
  fi
  test ! -e /opt/azcli
  echo "PASS: unmanaged /usr/bin/az collision refused and preserved"
}

classic_conflict() {
  mkdir -p /tmp/classic/DEBIAN /tmp/classic/usr/bin
  cat > /tmp/classic/DEBIAN/control <<'EOF'
Package: azure-cli
Version: 2.99.0
Architecture: amd64
Maintainer: Test <test@example.com>
Description: Synthetic classic Azure CLI collision fixture
EOF
  printf '#!/bin/sh\necho classic\n' > /tmp/classic/usr/bin/az
  chmod +x /tmp/classic/usr/bin/az
  dpkg-deb --build /tmp/classic /tmp/azure-cli.deb >/dev/null
  dpkg -i /tmp/azure-cli.deb >/dev/null
  apt_update
  apt-get install --simulate "$DEB_PATH" | tee /tmp/simulate.log
  grep -E '^Remv azure-cli ' /tmp/simulate.log
  echo "PASS: APT simulation exposes automatic classic removal"
}

lifecycle() {
  apt_update
  apt-get install -y -qq "$DEB_PATH"
  configure_test_home
  mkdir -p "$HOME_ROOT/.azure"
  printf 'azclips-user-state\n' > "$HOME_ROOT/.azclips/state-marker"
  printf 'classic-user-state\n' > "$HOME_ROOT/.azure/state-marker"

  apt-get install --reinstall -y -qq "$DEB_PATH"
  assert_installed_layout

  if [[ -f "$UPGRADE_DEB_PATH" ]]; then
    original_version="$(dpkg-query -W -f='${Version}' azcli)"
    apt-get install -y -qq "$UPGRADE_DEB_PATH"
    upgraded_version="$(dpkg-query -W -f='${Version}' azcli)"
    if dpkg --compare-versions "$upgraded_version" le "$original_version"; then
      echo "ERROR: Upgrade version '$upgraded_version' is not newer than '$original_version'." >&2
      exit 1
    fi
    assert_installed_layout
  fi

  HOME="$HOME_ROOT" az version --output json >/dev/null
  apt-get remove -y -qq azcli
  assert_removed_layout
  assert_user_state

  reinstall_path="$DEB_PATH"
  [[ -f "$UPGRADE_DEB_PATH" ]] && reinstall_path="$UPGRADE_DEB_PATH"
  apt-get install -y -qq "$reinstall_path"
  assert_installed_layout
  apt-get purge -y -qq azcli
  assert_removed_layout
  assert_user_state
  echo "PASS: reinstall, upgrade, remove, reinstall, purge, and user-state lifecycle"
}

assert_removed_layout() {
  test ! -e /opt/azcli
  test ! -e /usr/bin/az
}

assert_user_state() {
  test -f "$HOME_ROOT/.azclips/state-marker"
  test -f "$HOME_ROOT/.azure/state-marker"
}

case "${1:-}" in
  clean-install) clean_install ;;
  unmanaged-command) unmanaged_command ;;
  classic-conflict) classic_conflict ;;
  lifecycle) lifecycle ;;
  *) echo "ERROR: Unknown scenario: ${1:-}" >&2; exit 2 ;;
esac