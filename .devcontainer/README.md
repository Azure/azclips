# Dev Container Configuration

This directory contains the configuration for the Cirrus development container.

## Files

- **devcontainer.json**: Main dev container configuration
- **Dockerfile**: Custom Docker image with .NET 9.0 SDK
- **post-create.sh**: Post-creation setup script

## Browser Authentication Setup

The dev container is configured to handle browser-based authentication flows (like Azure login) by forwarding browser requests to the host machine's default browser.

### How It Works

1. **VS Code Browser Helper**: VS Code automatically sets the `BROWSER` environment variable to a helper script that can open URLs on the host machine
2. **System Browser Symlinks**: The `postStartCommand` creates symlinks so common browser launchers (`xdg-open`, `gnome-open`, `kfmclient`) point directly to VS Code's browser helper
3. **MSAL Integration**: The Microsoft Authentication Library (MSAL) uses these system browsers to open authentication URLs

### Why This Approach?

- **No Device Code Flow**: Avoids the cumbersome device code authentication flow
- **Host Browser**: Opens authentication pages in your host machine's default browser
- **Seamless**: Works transparently with MSAL's `AcquireTokenInteractive` flow
- **No Extra Files**: Uses VS Code's built-in browser helper directly via symlinks
- **Automatic Setup**: Symlinks are created on container start via `postStartCommand`

### Testing

To verify the browser proxy is working:

```bash
# Check that xdg-open is available
which xdg-open

# Check the BROWSER environment variable
echo $BROWSER

# Test opening a URL (will open in host browser)
xdg-open https://www.bing.com
```

### Rebuilding the Container

If you make changes to the Dockerfile, rebuild the container:

1. Press `F1` or `Ctrl+Shift+P`
2. Select "Dev Containers: Rebuild Container"
3. Wait for the rebuild to complete

Note: The browser symlinks are set up automatically on container start, so you don't need to rebuild for authentication to work.

## Environment Variables

The devcontainer sets the following environment variables:

- `PATH`: Includes the built `az` CLI binary locations
- `BROWSER`: VS Code's browser helper (automatically set by VS Code)

## Troubleshooting

### Browser Not Opening

If authentication doesn't open a browser:

1. Check that `xdg-open` is available: `which xdg-open`
2. Verify BROWSER is set: `echo $BROWSER`
3. Test the proxy manually: `xdg-open https://www.bing.com`
4. Rebuild the container if symlinks are missing

### Authentication Errors

If you see errors about browser tools not being available:

1. The container may need to be rebuilt
2. Check that the symlinks exist:
   ```bash
   ls -la /usr/local/bin/xdg-open
   ls -la /usr/local/bin/gnome-open
   ls -la /usr/local/bin/kfmclient
   ```

## Post-Create Setup

The `post-create.sh` script runs after the container is created and:

- Builds the solution
- Sets up PowerShell modules
- Configures any additional tools

To re-run the post-create script manually:

```bash
./.devcontainer/post-create.sh
```
