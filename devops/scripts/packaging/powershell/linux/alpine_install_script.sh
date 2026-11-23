#!/bin/sh
set -eu

powershell_version="${1:?PowerShell version is required.}"
work_root="${2:-/opt}"

powershell_platform=linux-musl
pwsh_root="$work_root/pwsh"
mkdir -p "$work_root"

apk add --no-cache \
  bash \
  ca-certificates \
  curl \
  jq \
  gzip \
  icu-libs \
  krb5-libs \
  libgcc \
  libstdc++ \
  openssl \
  tar \
  zlib

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$script_dir/../shared/bootstrap_powershell.sh"
echo "PowerShell installed at: $pwsh_path"
