# Scenario 7: Find the right command, or call REST when there isn't one

**User goal:** "I want to find the right command quickly, complete it as I type, and fall back to authenticated REST when there isn't a dedicated command."

**Successful outcome:** The user can find a command through help and tab completion, create a resource that has no dedicated command with a REST request, and clean up everything the scenario created.

## Prerequisites

- AzCLIPS CLI and/or the preview Az PowerShell modules, installed and signed in as described in [Scenario 1](https://github.com/Azure/azclips/blob/docs/scenario-2-feedback/docs/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- For tab completion in the CLI paths: bash, zsh or PowerShell, with the preview `az` active as set up in Scenario 1.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash**, **AzCLIPS CLI in PowerShell** or **AzCLIPS PowerShell**. Each path follows the same steps.

- **1. Discover commands:** Read the built-in help, including by running a command with no parameters, and create a small test resource group.
- **2. Complete commands as you type:** Turn on tab completion for this shell and complete command groups, subcommands and parameters with Tab.
- **3. Use authenticated REST:** Create a container registry, which has no dedicated command, with a REST PUT and a JSON payload, and read it back.
- **4. Clean up:** Delete the test group and close the shell that has tab completion turned on.

## AzCLIPS CLI in Bash

```bash
sub="<test-subscription-id>"
location="<region>"
# Unique suffix so this run's resources don't collide
run=$(date +%Y%m%d%H%M%S)
rg="azclips-find-$run"
registry="azclips$(openssl rand -hex 6)"
```

**1. Discover commands**

```bash
az --help
az group create --help
az group create -h
az group create
```

Running a command with no parameters shows its help instead of an error. Then create the test resource group:

```bash
az group create --subscription-id "$sub" --name "$rg" --location "$location"
```

**2. Complete commands as you type**

The completion scripts are installed with the CLI. Turn them on for this shell only. In bash:

```bash
source ~/.local/share/azcli/completions/az.completions.bash
```

In zsh:

```zsh
autoload -U compinit && compinit
source ~/.local/share/azcli/completions/az.completions.zsh
```

Type each line below and press Tab where shown:

```text
az <Tab>                  # command groups
az group <Tab>            # subcommands
az group create --<Tab>   # parameters
```

**3. Use authenticated REST**

A container registry has no dedicated command, so create one with a REST `PUT`. The URI follows the resource provider's REST API reference: `/subscriptions/{subscription}/resourceGroups/{group}/providers/Microsoft.ContainerRegistry/registries/{name}`. The request body holds the registry's properties, here a location and a SKU.

Every request needs an API version. Ask the resource provider which versions the `registries` resource type supports and take the newest stable one:

```bash
api_version=$(az rest --method GET --uri "/subscriptions/$sub/providers/Microsoft.ContainerRegistry?api-version=2021-04-01" --query "resourceTypes[?resourceType=='registries'] | [0].apiVersions" --output tsv | tr '\t' '\n' | grep -v preview | head -n 1)
registry_uri="/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.ContainerRegistry/registries/$registry?api-version=$api_version"
```

If the provider is not registered in your subscription, register it first:

```bash
az rest --method POST --uri "/subscriptions/$sub/providers/Microsoft.ContainerRegistry/register?api-version=2021-04-01"
```

Create the registry with a `PUT` and a JSON payload:

```bash
az rest --method PUT --uri "$registry_uri" --body "{\"location\":\"$location\",\"sku\":{\"name\":\"Basic\"}}" --headers Content-Type=application/json
```

`az rest` sends one request and does not wait for the operation to finish. Read the registry back until `provisioningState` is `Succeeded`:

```bash
az rest --method GET --uri "$registry_uri" --query properties.provisioningState --output tsv
```

**4. Clean up**

```bash
az group delete --subscription-id "$sub" --name "$rg"
az group exists --subscription-id "$sub" --name "$rg"
```

Deleting the group also deletes the registry. Close the shell to remove tab completion.

## AzCLIPS CLI in PowerShell

```powershell
$sub = '<test-subscription-id>'
$location = '<region>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-find-$run"
$registry = "azclips$([guid]::NewGuid().ToString('N').Substring(0, 12))"
```

**1. Discover commands**

```powershell
az --help
az group create --help
az group create -h
az group create
```

Running a command with no parameters shows its help instead of an error. Then create the test resource group:

```powershell
az group create --subscription-id $sub --name $rg --location $location
```

**2. Complete commands as you type**

The completion script is installed with the CLI. Turn it on for this session only:

```powershell
. "$env:LOCALAPPDATA\Programs\azcli\completions\az.completions.ps1"
```

Type each line below and press Tab where shown:

```text
az <Tab>                  # command groups
az group <Tab>            # subcommands
az group create --<Tab>   # parameters
```

**3. Use authenticated REST**

A container registry has no dedicated command, so create one with a REST `PUT`. The URI follows the resource provider's REST API reference: `/subscriptions/{subscription}/resourceGroups/{group}/providers/Microsoft.ContainerRegistry/registries/{name}`. The request body holds the registry's properties, here a location and a SKU.

Every request needs an API version. Ask the resource provider which versions the `registries` resource type supports and take the newest stable one:

```powershell
$providers = az rest --method GET --uri "/subscriptions/$sub/providers/Microsoft.ContainerRegistry?api-version=2021-04-01" | ConvertFrom-Json
$apiVersion = ($providers.resourceTypes | Where-Object resourceType -eq 'registries').apiVersions | Where-Object { $_ -notlike '*preview*' } | Select-Object -First 1
$registryUri = "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.ContainerRegistry/registries/${registry}?api-version=$apiVersion"
```

If the provider is not registered in your subscription, register it first:

```powershell
az rest --method POST --uri "/subscriptions/$sub/providers/Microsoft.ContainerRegistry/register?api-version=2021-04-01"
```

Create the registry with a `PUT` and a JSON payload:

```powershell
$body = @{ location = $location; sku = @{ name = 'Basic' } } | ConvertTo-Json -Compress
az rest --method PUT --uri $registryUri --body $body --headers Content-Type=application/json
```

`az rest` sends one request and does not wait for the operation to finish. Read the registry back until `provisioningState` is `Succeeded`:

```powershell
az rest --method GET --uri $registryUri --query properties.provisioningState --output tsv
```

**4. Clean up**

```powershell
az group delete --subscription-id $sub --name $rg
az group exists --subscription-id $sub --name $rg
```

Deleting the group also deletes the registry. Close the session to remove tab completion.

## AzCLIPS PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$sub = '<test-subscription-id>'
$location = '<region>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-find-$run"
$registry = "azclips$([guid]::NewGuid().ToString('N').Substring(0, 12))"
```

**1. Discover commands**

```powershell
Get-Help New-AzResourceGroup -Examples
New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location
```

**2. Complete commands as you type**

PowerShell completes cmdlet and parameter names for loaded modules. Type each line below and press Tab where shown:

```text
Get-AzResource<Tab>        # cmdlet names
New-AzResourceGroup -<Tab>   # parameters
```

**3. Use authenticated REST**

A container registry has no dedicated cmdlet, so create one with a REST `PUT`. The URI follows the resource provider's REST API reference: `/subscriptions/{subscription}/resourceGroups/{group}/providers/Microsoft.ContainerRegistry/registries/{name}`. The payload holds the registry's properties, here a location and a SKU.

Every request needs an API version. Ask the resource provider which versions the `registries` resource type supports and take the newest stable one:

```powershell
$providers = (Invoke-AzRestMethod -Method GET -Uri "/subscriptions/$sub/providers/Microsoft.ContainerRegistry?api-version=2021-04-01").Content | ConvertFrom-Json
$apiVersion = ($providers.resourceTypes | Where-Object resourceType -eq 'registries').apiVersions | Where-Object { $_ -notlike '*preview*' } | Select-Object -First 1
$registryUri = "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.ContainerRegistry/registries/${registry}?api-version=$apiVersion"
```

If the provider is not registered in your subscription, register it first:

```powershell
Invoke-AzRestMethod -Method POST -Uri "/subscriptions/$sub/providers/Microsoft.ContainerRegistry/register?api-version=2021-04-01"
```

Create the registry with a `PUT` and a JSON payload:

```powershell
$payload = @{ location = $location; sku = @{ name = 'Basic' } } | ConvertTo-Json -Compress
Invoke-AzRestMethod -Method PUT -Uri $registryUri -Payload $payload -Headers @{ 'Content-Type' = 'application/json' }
```

The cmdlet sends one request and does not wait for the operation to finish. Read the registry back until `provisioningState` is `Succeeded`:

```powershell
((Invoke-AzRestMethod -Method GET -Uri $registryUri).Content | ConvertFrom-Json).properties.provisioningState
```

**4. Clean up**

```powershell
Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force
Get-AzResourceGroup -SubscriptionId $sub -Name $rg -ErrorAction SilentlyContinue
```

Deleting the group also deletes the registry.
