#!/bin/sh
set -eu

powershell_version="${1:?PowerShell version is required.}"
work_root="${2:-/opt}"

powershell_platform=linux
pwsh_root="$work_root/pwsh"
mkdir -p "$work_root"

# Ubuntu currently shares the Debian prerequisites, but has its own entrypoint so they can diverge later.
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y --no-install-recommends \
  ca-certificates \
  curl \
  gzip \
  jq \
  libcurl4-openssl-dev \
  libicu-dev \
  libkrb5-3 \
  libssl-dev \
  tar \
  zlib1g
rm -rf /var/lib/apt/lists/*

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$script_dir/../shared/bootstrap_powershell.sh"
echo "PowerShell installed at: $pwsh_path"
