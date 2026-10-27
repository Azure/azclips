# Scenario 8: Complete a task when a command is missing or fails

**User goal:** "I want to find the right command, fall back to authenticated REST or a CLI extension when there isn't one, and recover quickly when a command fails."

**Successful outcome:** The user can inspect a test resource through a dedicated command and a REST request, confirm the extensions they need are installed, recover from a controlled failure with AI troubleshooting, and clean up everything the scenario created.

## Prerequisites

- AzCLIPS CLI and/or the preview Az PowerShell modules, installed as described in [Scenario 1](https://github.com/Azure/azclips/blob/docs/scenario-2-feedback/docs/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- You must be signed in. AI troubleshooting requires sign-in and is available from the AzCLIPS CLI only.
- AI troubleshooting is off by default.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash** or **AzCLIPS CLI in PowerShell**. Each path follows the same steps.

- **1. Discover commands:** Read the built-in help and create a small test resource group.
- **2. Use authenticated REST:** Read the test group through a direct REST request using your existing sign-in.
- **3. Understand a failure:** Turn on AI troubleshooting in manual mode, request a group that does not exist and inspect the error.
- **4. Recover with AI troubleshooting (CLI only):** Recover from the latest failure, and optionally choose an older saved failure from the list.
- **5. Try interactive recovery (optional, CLI only):** Repeat recovery in interactive style, approving the proposed plan and then each command.
- **6. Check your extensions (optional, CLI only):** List the installed extensions and confirm `compute` is present. If it is missing, add it from the release's extensions bundle.
- **7. Clean up:** Delete the test group, remove the extension only if you added it, and turn AI troubleshooting back off.

## AzCLIPS CLI in Bash

```bash
sub="<test-subscription-id>"
location="<region>"
run=$(date +%Y%m%d%H%M%S)
rg="azclips-recovery-$run"
uri="/subscriptions/$sub/resourceGroups/$rg?api-version=2021-04-01"
```

**1. Discover commands**

```bash
az --help
az group create --help
az group create --subscription-id "$sub" --name "$rg" --location "$location"
```

**2. Use authenticated REST**

```bash
az rest --method GET --uri "$uri"
az group show --subscription-id "$sub" --name "$rg"
```

**3. Understand a failure**

Turn on AI troubleshooting in manual mode so the failure is saved, then request a group that does not exist.

```bash
az config set --key agent.recovery.trigger --value manual
az group show --subscription-id "$sub" --name "$rg-missing"
echo "Exit code: $?"
```

**4. Recover with AI troubleshooting**

```bash
az troubleshoot recover
```

To choose an older failure instead, list the saved failures and recover from one by ID:

```bash
az troubleshoot list --output table
az troubleshoot recover --id "<failure-id>"
```

Review the explanation and suggested fix, and apply a suggestion only if it is appropriate.

**5. Try interactive recovery (optional)**

```bash
az troubleshoot recover --style interactive
```

Approve the proposed plan, then approve each command before it runs.

**6. Check your extensions (optional)**

A full installation includes the `compute` extension. Confirm it is installed:

```bash
az extension list
az extension show --name compute
```

If `compute` is not listed, download the extensions bundle from the release you installed, unpack it and add the extension:

```bash
gh release download --repo Azure/azclips --pattern 'azcli-extensions-*.tar.gz' --dir . --clobber
mkdir -p azcli-extensions
tar -xzf ./azcli-extensions-*.tar.gz -C azcli-extensions
az extension add --path ./azcli-extensions/azcli.extensions.compute.*.nupkg
az extension list
```

**7. Clean up**

```bash
az group delete --subscription-id "$sub" --name "$rg"
az group exists --subscription-id "$sub" --name "$rg"
az config set --key agent.recovery.trigger --value off
```

Turning AI troubleshooting off also clears the saved failures. If you added `compute` in step 6, remove it with `az extension remove --name compute`.

## AzCLIPS CLI in PowerShell

```powershell
$sub = '<test-subscription-id>'
$location = '<region>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-recovery-$run"
$uri = "/subscriptions/$sub/resourceGroups/${rg}?api-version=2021-04-01"
```

**1. Discover commands**

```powershell
az --help
az group create --help
az group create --subscription-id $sub --name $rg --location $location
```

**2. Use authenticated REST**

```powershell
az rest --method GET --uri $uri
az group show --subscription-id $sub --name $rg
```

**3. Understand a failure**

Turn on AI troubleshooting in manual mode so the failure is saved, then request a group that does not exist.

```powershell
az config set --key agent.recovery.trigger --value manual
az group show --subscription-id $sub --name "$rg-missing"
"Exit code: $LASTEXITCODE"
```

**4. Recover with AI troubleshooting**

```powershell
az troubleshoot recover
```

To choose an older failure instead, list the saved failures and recover from one by ID:

```powershell
az troubleshoot list --output table
az troubleshoot recover --id "<failure-id>"
```

Review the explanation and suggested fix, and apply a suggestion only if it is appropriate.

**5. Try interactive recovery (optional)**

```powershell
az troubleshoot recover --style interactive
```

Approve the proposed plan, then approve each command before it runs.

**6. Check your extensions (optional)**

A full installation includes the `compute` extension. Confirm it is installed:

```powershell
az extension list
az extension show --name compute
```

If `compute` is not listed, download the extensions bundle from the release you installed, unpack it and add the extension:

```powershell
gh release download --repo Azure/azclips --pattern 'azcli-extensions-*.tar.gz' --dir . --clobber
New-Item -ItemType Directory -Force -Path azcli-extensions | Out-Null
$bundle = Get-ChildItem azcli-extensions-*.tar.gz | Sort-Object LastWriteTime -Descending | Select-Object -First 1
tar -xzf $bundle.FullName -C azcli-extensions
$compute = Get-ChildItem azcli-extensions/azcli.extensions.compute.*.nupkg | Select-Object -First 1
az extension add --path $compute.FullName
az extension list
```

**7. Clean up**

```powershell
az group delete --subscription-id $sub --name $rg
az group exists --subscription-id $sub --name $rg
az config set --key agent.recovery.trigger --value off
```

Turning AI troubleshooting off also clears the saved failures. If you added `compute` in step 6, remove it with `az extension remove --name compute`.