# Installs the PowerShell 7 dependency shared by every non-Windows distribution.
# This bootstrap does not run module validation; pipeline steps invoke those
# phases separately. Sourced by each <distro>_install_script.sh after it has
# installed its own OS prerequisites.
#
# Inputs: source_root, powershell_version, dotnet_runtime_version, dotnet_root,
# pwsh_root and install_script.
# Output: pwsh_path.
sdk_version=$(sed -n 's/.*"version": "\([^"]*\)".*/\1/p' "$source_root/global.json" | head -n 1)

if [ -z "$sdk_version" ]; then
  echo "ERROR: Could not resolve the .NET SDK version from $source_root/global.json" >&2
  exit 1
fi

curl --fail --location --retry 5 --retry-delay 2 \
  https://dot.net/v1/dotnet-install.sh \
  --output "$install_script"
chmod +x "$install_script"
"$install_script" --version "$sdk_version" --install-dir "$dotnet_root"

export DOTNET_ROOT="$dotnet_root"
export PATH="$dotnet_root:$PATH"

# The PowerShell .NET tool is framework dependent, so the runtime it was built
# against has to be present alongside the SDK used to build the modules.
"$install_script" --runtime dotnet --channel "$dotnet_runtime_version" --install-dir "$dotnet_root"

"$dotnet_root/dotnet" --list-sdks
"$dotnet_root/dotnet" --list-runtimes

"$dotnet_root/dotnet" tool install \
  --tool-path "$pwsh_root" \
  PowerShell \
  --version "${powershell_version}.*"

pwsh_path="$pwsh_root/pwsh"
"$pwsh_path" -NoLogo -NoProfile -Command '$PSVersionTable'
