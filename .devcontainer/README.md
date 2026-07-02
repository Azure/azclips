# Dev Container Configuration

This directory contains the configuration for the azclips development container.

## Files

- **devcontainer.json**: Main dev container configuration
- **docker-compose.yml**: Docker Compose configuration for multi-container setup
- **Dockerfile**: Custom Docker image with .NET 9.0 SDK
- **post-create.sh**: Post-creation setup script
- **post-start.sh**: Post-start setup script

## Services

The dev container uses Docker Compose to run multiple services:

### App Container
The main development container with .NET 9.0 SDK and all required tools.

### Zipkin Container
A local OpenTelemetry trace collector and UI for development and debugging. Zipkin automatically starts with the dev container and is accessible at:
- **URL**: http://localhost:9411
- **Service Name**: Look for traces from the AzureCLI service

The application is configured to automatically export telemetry to Zipkin when running in DEBUG mode. No manual setup required!

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
- `ZIPKIN_ENDPOINT`: Points to the Zipkin container (`http://zipkin:9411/api/v2/spans`)

## Using Zipkin for Trace Visualization

Zipkin is automatically started when you open the dev container. To view traces:

1. Build and run the application in DEBUG mode:
   ```bash
   dotnet build -c Debug cirrus.slnx
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
   dotnet build -c Debug cirrus.slnx
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

- Builds the solution
- Sets up PowerShell modules
- Configures any additional tools

To re-run the post-create script manually:

```bash
./.devcontainer/post-create.sh
```
