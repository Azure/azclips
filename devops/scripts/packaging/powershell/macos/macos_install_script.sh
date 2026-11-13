#!/bin/sh
set -eu

source_root="${1:?Source root is required.}"
powershell_version="${2:?PowerShell version is required.}"
dotnet_runtime_version="${3:?.NET runtime version is required.}"

# Hosted macOS images already ship curl, tar and the ICU libraries the .NET
# runtime needs, so there are no packages to install. /opt is not writable
# without elevation there, so keep the toolchain in the agent temp directory.
work_root="${4:-${AGENT_TEMPDIRECTORY:-${TMPDIR:-/tmp}}/azclips-ps-validation}"
dotnet_root="$work_root/dotnet"
pwsh_root="$work_root/pwsh"
install_script="$work_root/dotnet-install.sh"
mkdir -p "$work_root"

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$script_dir/../shared/bootstrap_powershell.sh"
echo "PowerShell installed at: $pwsh_path"
