#!/bin/sh
set -eu

source_root="${1:?Source root is required.}"
powershell_version="${2:?PowerShell version is required.}"
dotnet_runtime_version="${3:?.NET runtime version is required.}"
work_root="${4:-/opt}"

dotnet_root="$work_root/dotnet"
pwsh_root="$work_root/pwsh"
install_script="$work_root/dotnet-install.sh"
mkdir -p "$work_root"

apk add --no-cache \
  bash \
  ca-certificates \
  curl \
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
