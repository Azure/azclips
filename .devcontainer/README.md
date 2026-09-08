# Dev Container Configuration

This directory contains the configuration for the azclips development container.

## Files

- **devcontainer.json**: Main dev container configuration
- **docker-compose.yml**: Docker Compose configuration for the dev container
- **Dockerfile**: Custom Docker image with .NET 10.0 SDK
- **post-create.sh**: Post-creation setup script
- **post-start.sh**: Post-start setup script

## Services

The dev container runs a single `app` service via Docker Compose: the main
development container with the .NET 10.0 SDK and all required tools.

## npm Registry Configuration

The `node` dev container feature installs pnpm and yarn via npm, and the repo
has npm workspaces under `toolkit/` and `wiki/`, so the container needs a
reachable npm registry. It defaults to the public one, so a plain clone works
with no setup.

To use a proxy feed, create an untracked `.devcontainer/.env` file (copy `.env.example`).

```
NPM_REGISTRY=https://your-proxy-feed.example.com/npm/
```

## Browser Authentication Setup

The dev container is configured to handle browser-based authentication flows (like Azure login) by forwarding browser requests to the host machine's default browser.

### How It Works

1. **VS Code Browser Helper**: VS Code sets `BROWSER` in its integrated terminals to a helper script that opens URLs on the host machine
2. **Browser Launcher Shims**: `postStartCommand` installs `xdg-open`, `gnome-open`, `kfmclient` and `wslview` into `/usr/local/bin`, each forwarding to that helper
3. **MSAL Integration**: MSAL invokes those four tools *by name* to open sign-in URLs. It never reads `BROWSER` itself, which is why the shims are required
4. **Redirect Back**: MSAL listens on a loopback port and the browser redirects to it after sign-in. VS Code auto-forwards that port to the host within a second or two, so the redirect reaches the container
5. **IPv4 Loopback**: `postStartCommand` drops the `localhost` alias from the `::1` line in `/etc/hosts`. Without this MSAL binds `[::1]` only, and since VS Code's forwarder connects over IPv4 the sign-in page hangs forever after you authenticate

### Why This Approach?

- **No Device Code Flow**: Avoids the cumbersome device code authentication flow
- **Host Browser**: Opens authentication pages in your host machine's default browser
- **Seamless**: Works transparently with MSAL's `AcquireTokenInteractive` flow
- **Resilient**: The shims resolve the helper when called, so they survive a VS Code server update changing its path
- **Automatic Setup**: Installed on every container start via `postStartCommand`

### Testing

Run this **from a VS Code integrated terminal**:

```bash
# Check that the launchers are installed
which xdg-open gnome-open kfmclient wslview

# Check the BROWSER environment variable
echo $BROWSER

# Test opening a URL (will open in host browser)
xdg-open https://www.bing.com
```

The same commands run through `docker exec` will report `Command is only
available in WSL or inside a Visual Studio Code terminal`, because the helper
also needs `VSCODE_IPC_HOOK_CLI`, which only integrated terminals set.

### Rebuilding the Container

If you make changes to the Dockerfile, rebuild the container:

1. Press `F1` or `Ctrl+Shift+P`
2. Select "Dev Containers: Rebuild Container"
3. Wait for the rebuild to complete

Note: The browser launchers are installed automatically on container start, so you don't need to rebuild for authentication to work.

## Environment Variables

The devcontainer sets the following environment variables:

- `PATH`: Includes the built `az` CLI binary locations
- `BROWSER`: VS Code's browser helper (automatically set by VS Code)
- `NPM_CONFIG_REGISTRY`: npm registry used inside the container (see [npm Registry Configuration](#npm-registry-configuration))

## Troubleshooting

### Browser Not Opening

If authentication doesn't open a browser:

1. Check that `xdg-open` is available: `which xdg-open`
2. Verify BROWSER is set: `echo $BROWSER`
3. Test the proxy manually: `xdg-open https://www.bing.com`
4. Re-run `./.devcontainer/post-start.sh` if the launchers are missing

### Authentication Errors

If `az login` fails with `Unable to open a web page using xdg-open, gnome-open,
kfmclient or wslview tools`:

1. Confirm the launchers exist:
   ```bash
   ls -l /usr/local/bin/xdg-open /usr/local/bin/wslview
   ```
2. If they are missing, re-run the setup: `./.devcontainer/post-start.sh`
3. Run `az login` from a VS Code integrated terminal, not `docker exec`.

### Login Hangs After Signing In

If the browser opens, you sign in, and the page then loads forever without
returning to the terminal, the redirect is not reaching MSAL's loopback
listener. Check that `localhost` resolves to IPv4 only:

```bash
getent ahosts localhost | head -n 1   # expect 127.0.0.1, not ::1
```

If it returns `::1`, re-run `./.devcontainer/post-start.sh`. Docker rewrites
`/etc/hosts` on every container start, so this is reapplied each time.

## Post-Create Setup

The `post-create.sh` script runs after the container is created and:

- Installs the `Microsoft.PowerShell.PlatyPS` and `Az.Accounts` PowerShell modules
- Adds the built `az` CLI location to `PATH` in `~/.bashrc`

To re-run the post-create script manually:

```bash
./.devcontainer/post-create.sh
```
