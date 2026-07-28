#!/bin/bash
# Post-start script - runs every time the container starts

set -euo pipefail

echo "🌐 Setting up browser launchers for authentication..."

# MSAL opens the sign-in page by invoking xdg-open, gnome-open, kfmclient or
# wslview directly -- it never reads $BROWSER. None of those tools exist in this
# image, so install a shim that forwards to VS Code's browser helper instead.
#
# The shim resolves the helper when it is *called*, not now. This script runs as
# a lifecycle command, where $BROWSER is empty, whereas the integrated terminal
# that actually runs `az login` does have $BROWSER set correctly.
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