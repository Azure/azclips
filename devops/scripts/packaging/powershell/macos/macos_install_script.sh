#!/bin/sh
set -eu

powershell_version="${1:?PowerShell version is required.}"

# Hosted macOS images already ship curl, jq, tar and the ICU libraries the .NET
# runtime needs, so there are no packages to install. /opt is not writable
# without elevation there, so keep the toolchain in the agent temp directory.
work_root="${2:-${AGENT_TEMPDIRECTORY:-${TMPDIR:-/tmp}}/azclips-ps-validation}"
powershell_platform=osx
pwsh_root="$work_root/pwsh"
mkdir -p "$work_root"

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$script_dir/../shared/bootstrap_powershell.sh"
echo "PowerShell installed at: $pwsh_path"
