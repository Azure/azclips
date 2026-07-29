#!/bin/bash
# Post-start script - runs every time the container starts

set -euo pipefail

echo "🌐 Setting up browser launchers for authentication..."

# Install a shim for xdg-open to forward browser requests to VS Code's browser helper.
# This is needed for MSAL authentication to work in the dev container.
sudo tee /usr/local/bin/xdg-open >/dev/null <<'SHIM'
#!/bin/bash
# Opens a URL in the host's default browser via VS Code.

if [ -n "${BROWSER:-}" ] && [ -x "${BROWSER}" ]; then
    exec "${BROWSER}" "$@"
fi

# Fall back to locating the helper directly; its path contains the VS Code
# server commit hash, so it is globbed rather than hard-coded.
helper="$(ls -1t /vscode/vscode-server/bin/*/*/bin/helpers/browser.sh \
                 "${HOME}/.vscode-server"/bin/*/bin/helpers/browser.sh \
                 2>/dev/null | head -n 1)"
if [ -n "${helper}" ]; then
    exec "${helper}" "$@"
fi

if command -v code >/dev/null 2>&1; then
    exec code --openExternal "$@"
fi

echo "xdg-open: no VS Code browser helper found; cannot open $*" >&2
exit 1
SHIM

sudo chmod 0755 /usr/local/bin/xdg-open
for tool in gnome-open kfmclient wslview; do
    sudo ln -sf /usr/local/bin/xdg-open "/usr/local/bin/${tool}"
done

echo "✓ Browser launchers ready: xdg-open, gnome-open, kfmclient, wslview"

# --- Make "localhost" resolve to IPv4 only -------------------------------------
#
# Ensures localhost resolves to 127.0.0.1 instead of ::1 (IPv6).
# Required for MSAL interactive authentication via VS Code's port forwarder.
if awk '$1 == "::1" { for (i = 2; i <= NF; i++) if ($i == "localhost") found = 1 } END { exit found ? 0 : 1 }' /etc/hosts; then
    tmp="$(mktemp)"
    awk '$1 == "::1" {
             line = $1
             for (i = 2; i <= NF; i++) if ($i != "localhost") line = line "\t" $i
             print line
             next
         }
        { print }' /etc/hosts > "${tmp}"
    sudo tee /etc/hosts >/dev/null < "${tmp}"
    rm -f "${tmp}"
    echo "✓ localhost pinned to IPv4 (required for interactive az login)"
else
    echo "✓ localhost already resolves to IPv4 only"
fi