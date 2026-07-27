# Dev Container Configuration

This directory contains the configuration for the azclips development container.

## Files

- **devcontainer.json**: Main dev container configuration
- **docker-compose.yml**: Docker Compose configuration for multi-container setup
- **Dockerfile**: Custom Docker image with .NET 10.0 SDK
- **post-create.sh**: Post-creation setup script
- **post-start.sh**: Post-start setup script

## Services

The dev container uses Docker Compose to run multiple services:

### App Container
The main development container with .NET 10.0 SDK and all required tools.

### Zipkin Container
A local OpenTelemetry trace collector and UI for development and debugging. Zipkin automatically starts with the dev container and is accessible at:
- **URL**: http://localhost:9411
- **Service Name**: Look for traces from the AzureCLI service

The application is configured to automatically export telemetry to Zipkin when running in DEBUG mode. No manual setup required!

## npm Registry Configuration

The container installs Node tooling (pnpm and yarn are installed by the `node`
dev container feature) and the repo itself contains npm workspaces under
`toolkit/` and `wiki/`. All of that requires a reachable npm registry.

By default the container uses the public registry, so a plain clone works with
no extra setup:

```
https://registry.npmjs.org/
```

### Using a proxy feed

Some networks block direct access to `registry.npmjs.org`. The symptom is a
container build that fails while installing the `node` feature:

```
npm error code ERR_SSL_SSL/TLS_ALERT_HANDSHAKE_FAILURE
ERROR: Feature "Node.js (via nvm), yarn and pnpm." (ghcr.io/devcontainers/features/node) failed
```

Note that Node itself installs fine in this case — it is downloaded from
`nodejs.org`, not from the npm registry. The failure happens on the next step,
when the feature runs `npm install -g` for pnpm and yarn.

To point the container at a different registry, create an untracked
`.devcontainer/.env` file:

```
NPM_REGISTRY=https://your-proxy-feed.example.com/npm/
```

Docker Compose loads this file automatically, and it is already covered by the
repository `.gitignore`. Then rebuild the container:

1. Press `F1` or `Ctrl+Shift+P`
2. Select "Dev Containers: Rebuild Container"

The value is applied as the `NPM_REGISTRY` build argument, which sets
`NPM_CONFIG_REGISTRY` in the image — so it applies both while dev container
features install and later when you run `npm` inside the container.

Configuring the host's own npm is not sufficient. The container does not inherit
host npm configuration, and on machines where the registry is set by policy the
setting typically lives in a global `npmrc` rather than in `~/.npmrc`, so
bind-mounting `~/.npmrc` does not help either.

> **Note**: This is intentionally not committed to the repository. Proxy feeds
> are usually organization-specific and unreachable from other networks, so
> hard-coding one would break builds for everyone outside that network.

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
- `NPM_CONFIG_REGISTRY`: npm registry used inside the container (see [npm Registry Configuration](#npm-registry-configuration))
- `ZIPKIN_ENDPOINT`: Points to the Zipkin container (`http://zipkin:9411/api/v2/spans`)

## Using Zipkin for Trace Visualization

Zipkin is automatically started when you open the dev container. To view traces:

1. Build and run the application in DEBUG mode:
   ```bash
   dotnet build -c Debug azclips.slnx
   ./artifacts/Debug/Core.Cli/az group create --Name mygroup --Location eastus --SubscriptionId <your-subscription-id>
   ```
   
   > **Note**: For authentication, use `az login` or other secure methods instead of passing tokens via command-line arguments.

2. Open the Zipkin UI at http://localhost:9411

3. Click "Run Query" or search for traces from the "AzureCLI" service

4. Click on any trace to see detailed timing and spans

The Zipkin exporter is only enabled in DEBUG builds, so make sure to build with `-c Debug` to see traces.

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

### Zipkin Not Accessible

If you cannot access Zipkin at http://localhost:9411:

1. Check that the Zipkin container is running:
   ```bash
   docker ps | grep zipkin
   ```

2. Verify port forwarding is configured:
   ```bash
   # In VS Code, check the "Ports" tab in the terminal panel
   ```

3. Restart the dev container if needed:
   - Press `F1` or `Ctrl+Shift+P`
   - Select "Dev Containers: Rebuild Container"

### No Traces Appearing in Zipkin

If Zipkin is running but you don't see any traces:

1. Make sure you built the application in DEBUG mode:
   ```bash
   dotnet build -c Debug azclips.slnx
   ```

2. Verify the ZIPKIN_ENDPOINT environment variable:
   ```bash
   echo $ZIPKIN_ENDPOINT
   # Should output: http://zipkin:9411/api/v2/spans
   ```

3. Check that you're running the DEBUG build:
   ```bash
   ./artifacts/Debug/Core.Cli/az --version
   ```

## Post-Create Setup

The `post-create.sh` script runs after the container is created and:

- Installs the `Microsoft.PowerShell.PlatyPS` and `Az.Accounts` PowerShell modules
- Adds the built `az` CLI location to `PATH` in `~/.bashrc`
- Creates the browser symlinks used for authentication

To re-run the post-create script manually:

```bash
./.devcontainer/post-create.sh
```
