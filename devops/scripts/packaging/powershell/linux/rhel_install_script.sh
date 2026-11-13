#!/bin/sh
set -eu

powershell_version="${1:?PowerShell version is required.}"
work_root="${2:-/opt}"

powershell_platform=linux
pwsh_root="$work_root/pwsh"
mkdir -p "$work_root"

dnf install -y --allowerasing \
  ca-certificates \
  curl \
  gzip \
  jq \
  libgcc \
  libicu \
  libstdc++ \
  krb5-libs \
  openssl-libs \
  tar \
  zlib
dnf clean all

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$script_dir/../shared/bootstrap_powershell.sh"
echo "PowerShell installed at: $pwsh_path"
