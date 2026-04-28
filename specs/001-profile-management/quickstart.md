# Quickstart: Profile Management

**Feature**: 001-profile-management

## Prerequisites

- Cirrus CLI built: `dotnet build cirrus.slnx`
- CLI deployed: `./scripts/deploy.sh`

## CLI Workflow

### 1. Login and create a profile

```bash
# Login with browser auth, create a profile named "dev"
./bin/azv2 login --out-profile dev

# Login with device code flow, explicit subscription
./bin/azv2 login --out-profile prod --use-device-code --subscription 66666666-7777-8888-9999-000000000000
```

### 2. List profiles

```bash
./bin/azv2 profile list
#   NAME    TENANT                                SUBSCRIPTION                          AUTH
# * dev     aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee  11111111-2222-3333-4444-555555555555  browser
#   prod    ffffffff-gggg-hhhh-iiii-jjjjjjjjjjjj  66666666-7777-8888-9999-000000000000  device-code
```

### 3. Show profile details

```bash
./bin/azv2 profile show dev
```

### 4. Switch active profile

```bash
./bin/azv2 profile switch prod
# Switched to profile: prod
```

### 5. Run a command with a per-command override

```bash
# Use "dev" profile for this one command; active stays "prod"
./bin/azv2 group list --profile dev
```

### 6. Modify a profile

```bash
./bin/azv2 profile modify dev --default location=westus2
```

### 7. Logout

```bash
# Logout from active profile
./bin/azv2 logout

# Logout from a specific profile
./bin/azv2 logout --profile dev
```

### 8. Remove a profile

```bash
./bin/azv2 profile remove prod
```

## PowerShell Workflow

```powershell
Import-Module ./bin/Cirrus.psd1

# Login and create a profile
Connect-CirrusAccount -OutProfile dev

# List all profiles
Get-CirrusProfile -List

# Show specific profile
Get-CirrusProfile -Name dev

# Switch active profile
Switch-CirrusProfile -Name prod

# Modify defaults
Set-CirrusProfile -Name dev -Default @{location = "eastus"}

# Logout
Disconnect-CirrusAccount

# Remove a profile
Remove-CirrusProfile -Name prod -Force
```

## Verification

After completing the quickstart, verify:

1. `profiles.json` exists at the expected path:
   - macOS: `~/Library/Application Support/.cirrus/profiles.json`
   - Windows: `%LOCALAPPDATA%\.cirrus\profiles.json`
2. File permissions are owner-only (Unix: `ls -la` shows `-rw-------`)
3. `profile list` shows the correct active profile marker
4. Commands executed after `profile switch` use the new profile's tenant/subscription
