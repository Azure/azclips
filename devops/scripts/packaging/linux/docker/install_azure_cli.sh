#!/usr/bin/env bash
set -euo pipefail

if command -v az >/dev/null 2>&1; then
  az version
  exit 0
fi

if ! command -v apt-get >/dev/null 2>&1 || ! command -v dpkg >/dev/null 2>&1; then
  echo "ERROR: Azure CLI bootstrap requires a Debian-based agent with apt-get and dpkg."
  exit 1
fi

source /etc/os-release
suite="${VERSION_CODENAME:-}"
if [[ -z "$suite" ]]; then
  echo "ERROR: Unable to determine the Debian distribution codename."
  exit 1
fi

architecture="$(dpkg --print-architecture)"

temp_dir="$(mktemp -d)"
trap 'rm -rf "$temp_dir"' EXIT

sudo apt-get update
sudo env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
  ca-certificates \
  curl

repo_package_url="https://packages.microsoft.com/config/ubuntu/${suite}/packages-microsoft-prod.deb"
curl --fail --silent --show-error --location "$repo_package_url" --output "$temp_dir/packages-microsoft-prod.deb"
sudo dpkg -i "$temp_dir/packages-microsoft-prod.deb"

cat >"$temp_dir/azure-cli.sources" <<EOF
Types: deb
URIs: https://packages.microsoft.com/repos/azure-cli/
Suites: $suite
Components: main
Architectures: $architecture
Signed-By: /usr/share/keyrings/microsoft-prod.gpg
EOF
sudo install -D -m 0644 "$temp_dir/azure-cli.sources" /etc/apt/sources.list.d/azure-cli.sources

sudo apt-get update
sudo env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends azure-cli

az version
