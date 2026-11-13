# Installs the PowerShell 7 dependency shared by every non-Windows distribution.
# This bootstrap does not run module validation; pipeline steps invoke those
# phases separately. Sourced by each <distro>_install_script.sh after it has
# installed its own OS prerequisites.
#
# Inputs: powershell_version, powershell_platform, work_root and pwsh_root.
# Output: pwsh_path.
if ! printf '%s\n' "$powershell_version" | grep -Eq '^[0-9]+\.[0-9]+(\.[0-9]+)?$'; then
  echo "ERROR: PowerShell version must be a numeric release line or exact version." >&2
  exit 1
fi

download_powershell_file() {
  download_attempt=1
  while ! curl --fail --location --silent --show-error \
    --connect-timeout 15 --max-time 180 --output "$2" "$1"; do
    rm -f "$2"
    if [ "$download_attempt" -ge 5 ]; then
      echo "ERROR: Download failed after $download_attempt attempts: $1" >&2
      return 1
    fi
    echo "Retrying download ($download_attempt/5): $1" >&2
    download_attempt=$((download_attempt + 1))
    sleep 2
  done
}

case "$(uname -m)" in
  x86_64|amd64) powershell_arch=x64 ;;
  aarch64|arm64) powershell_arch=arm64 ;;
  *) echo "ERROR: Unsupported PowerShell architecture: $(uname -m)" >&2; exit 1 ;;
esac
if [ "$powershell_platform:$powershell_arch" = "linux-musl:arm64" ]; then
  echo "ERROR: PowerShell does not publish a standalone linux-musl-arm64 archive." >&2
  exit 1
fi

if [ -e "$pwsh_root" ]; then
  echo "ERROR: PowerShell installation directory already exists: $pwsh_root" >&2
  exit 1
fi

resolved_version="$powershell_version"
if printf '%s\n' "$powershell_version" | grep -Eq '^[0-9]+\.[0-9]+$'; then
  command -v jq >/dev/null 2>&1 || { echo "ERROR: jq is required to resolve a PowerShell release line." >&2; exit 1; }
  download_powershell_file \
    'https://api.github.com/repos/PowerShell/PowerShell/releases?per_page=100' \
    "$work_root/powershell-releases.json"
  resolved_version=$(jq -er --arg line "$powershell_version" '
    [.[] | select(.draft == false and .prerelease == false) |
      .tag_name | select(test("^v[0-9]+\\.[0-9]+\\.[0-9]+$")) | ltrimstr("v") |
      select(startswith($line + "."))] |
    sort_by(split(".") | map(tonumber)) | last // error("No stable release for " + $line)
  ' "$work_root/powershell-releases.json")
fi

archive_name="powershell-$resolved_version-$powershell_platform-$powershell_arch.tar.gz"
release_url="https://github.com/PowerShell/PowerShell/releases/download/v$resolved_version"
archive_path="$work_root/$archive_name"
hashes_path="$work_root/powershell-hashes.sha256"
echo "Installing standalone PowerShell $resolved_version ($powershell_platform-$powershell_arch)"
download_powershell_file "$release_url/$archive_name" "$archive_path"
download_powershell_file "$release_url/hashes.sha256" "$hashes_path"

# Published manifests can be UTF-16LE or UTF-8, with a BOM and CRLF.
expected_hash=$(LC_ALL=C tr -d '\000\r' < "$hashes_path" |
  LC_ALL=C sed '1s/^[^0-9A-Fa-f]*//' |
  awk -v file="$archive_name" '$2 == file || $2 == "*" file { print tolower($1) }')
if ! printf '%s\n' "$expected_hash" | grep -Eq '^[0-9a-f]{64}$' ||
  [ "${#expected_hash}" -ne 64 ]; then
  echo "ERROR: Expected exactly one SHA256 checksum for $archive_name." >&2
  exit 1
fi
if command -v sha256sum >/dev/null 2>&1; then
  actual_hash=$(sha256sum "$archive_path" | awk '{ print $1 }')
else
  actual_hash=$(shasum -a 256 "$archive_path" | awk '{ print $1 }')
fi
if [ "$actual_hash" != "$expected_hash" ]; then
  echo "ERROR: SHA256 checksum mismatch for $archive_name." >&2
  exit 1
fi

mkdir -p "$pwsh_root"
tar -xzf "$archive_path" -C "$pwsh_root"

pwsh_path="$pwsh_root/pwsh"
chmod +x "$pwsh_path"
actual_version=$("$pwsh_path" -NoLogo -NoProfile -Command '$PSVersionTable.PSVersion.ToString()')
actual_version=$(printf '%s' "$actual_version" | tr -d '\r')
if [ "$actual_version" != "$resolved_version" ]; then
  echo "ERROR: Expected PowerShell $resolved_version, got $actual_version." >&2
  exit 1
fi
echo "Validated PowerShell $actual_version with its bundled .NET runtime."
