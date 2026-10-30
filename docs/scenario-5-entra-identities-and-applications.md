# Scenario 5: Look up identities and register an application in Microsoft Entra ID

**User goal:** "I want to find the right users and groups in my tenant and register an application for them to use."

**Successful outcome:** The intended user, group and membership are found, a single-tenant application registration exists with the intended settings, and the registration is removed afterward.

## Prerequisites

- The AzCLIPS CLI and/or the preview Az PowerShell modules, installed and signed in as described in [Scenario 1](https://github.com/Azure/azclips/blob/main/docs/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- A test user and a test group in your tenant, with permission to read them through Microsoft Graph and to create application registrations.

Use a new application registration, not an existing application. Do not share tenant-wide user data from list output.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash**, **AzCLIPS CLI in PowerShell** or **AzCLIPS PowerShell**. Each path follows the same steps.

- **1. Find identities:** Look up a test user and group, list the group's members and check that the user belongs to it.
- **2. Register the application:** Create a single-tenant application registration.
- **3. Inspect the registration:** Show and list the application, and record its object ID and client ID.
- **4. Clean up:** Delete the registration and confirm it is gone.

## AzCLIPS CLI in Bash

```bash
set -euo pipefail
user_id="<test-user-object-ID>"
group_id="<test-group-object-ID>"
# Unique suffix so this run's resources don't collide
run=$(date +%Y%m%d%H%M%S)
app_name="azclips-app-$run"
```

**1. Find identities**

```bash
az msgraph user show --id "$user_id"
az msgraph user list
az msgraph group show --group "$group_id"
az msgraph group list
az msgraph group member list --group "$group_id"
az msgraph group member check --group "$group_id" --member-id "$user_id"
```

**2. Register the application**

```bash
app_object_id=$(az msgraph app create --display-name "$app_name" --sign-in-audience AzureADMyOrg --query id --output tsv)
```

**3. Inspect the registration**

```bash
az msgraph app show --id "$app_object_id"
az msgraph app list --display-name "$app_name"
client_id=$(az msgraph app show --id "$app_object_id" --query appId --output tsv)
```

**4. Clean up**

```bash
az msgraph app delete --id "$app_object_id"
az msgraph app show --id "$app_object_id" || echo "Application not found, as expected."
```

## AzCLIPS CLI in PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$userId = '<test-user-object-ID>'
$groupId = '<test-group-object-ID>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$appName = "azclips-app-$run"
```

**1. Find identities**

```powershell
az msgraph user show --id $userId
az msgraph user list
az msgraph group show --group $groupId
az msgraph group list
az msgraph group member list --group $groupId
az msgraph group member check --group $groupId --member-id $userId
```

**2. Register the application**

```powershell
$app = az msgraph app create --display-name $appName --sign-in-audience AzureADMyOrg --output json | ConvertFrom-Json
$appObjectId = $app.id
```

**3. Inspect the registration**

```powershell
az msgraph app show --id $appObjectId
az msgraph app list --display-name $appName
$clientId = $app.appId
```

**4. Clean up**

```powershell
az msgraph app delete --id $appObjectId
az msgraph app show --id $appObjectId
```

The final lookup reports that the application is not found.

## AzCLIPS PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$userId = '<test-user-object-ID>'
$groupId = '<test-group-object-ID>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$appName = "azclips-app-$run"
```

**1. Find identities**

```powershell
Get-AzMsGraphUser -Id $userId
Get-AzMsGraphUser
Get-AzMsGraphGroup -ObjectId $groupId
Get-AzMsGraphGroup
Get-AzMsGraphGroupMember -GroupObjectId $groupId
```

**2. Register the application**

```powershell
$app = New-AzMsGraphApplication -DisplayName $appName -SignInAudience AzureADMyOrg
$appObjectId = $app.Id
```

**3. Inspect the registration**

```powershell
Get-AzMsGraphApplication -Id $appObjectId
Get-AzMsGraphApplication -DisplayName $appName
$clientId = $app.AppId
```

**4. Clean up**

```powershell
Remove-AzMsGraphApplication -Id $appObjectId
Get-AzMsGraphApplication -Id $appObjectId
```

The final lookup reports that the application is not found.
