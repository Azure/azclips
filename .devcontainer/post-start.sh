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

# --- Make "localhost" resolve to IPv4 only -------------------------------------
#
# MSAL's interactive flow listens on a loopback port and has the browser redirect
# back to it. .NET resolves the "localhost" in that redirect URI and binds the
# first address it gets, which is ::1 here -- so the listener ends up IPv6-only.
#
# VS Code's port forwarder connects to the container over IPv4, so it can never
# reach an IPv6-only listener: the browser accepts the connection on the host and
# then hangs forever waiting for a response that cannot arrive.
#
# Dropping the "localhost" alias from the ::1 entry forces the listener onto
# 127.0.0.1, which the forwarder can reach. ip6-localhost/ip6-loopback are kept,
# so anything genuinely wanting IPv6 loopback still resolves.
#
# Docker regenerates /etc/hosts on every container start, which is why this lives
# here rather than in the Dockerfile. /etc/hosts is a bind mount, so it must be
# overwritten in place -- sed -i and mv both fail with "Device or resource busy".
# Fields are compared exactly: a substring match would also hit the "localhost"
# inside "ip6-localhost" and mangle it.
if awk '$1 == "::1" { for (i = 2; i <= NF; i++) if ($i == "localhost") found = 1 } END { exit found ? 0 : 1 }' /etc/hosts; then
    tmp="$(mktemp)"
    awk '$1 == "::1" {
             line = $1
             for (i = 2; i <= NF; i++) if ($i != "localhost") line = line "\t" $i
             print line
             next
         }
         { print }' /etc/hosts > "${tmp}"
    sudo cp "${tmp}" /etc/hosts
    rm -f "${tmp}"
    echo "✓ localhost pinned to IPv4 (required for interactive az login)"
else
    echo "✓ localhost already resolves to IPv4 only"
fi