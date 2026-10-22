# Scenario 6: Create a Web App and connect it to an Entra application

**October 21 scope note:** Web App creation is excluded from PP (P1). This connected journey is retained for future review, not PP execution. Microsoft Graph reads/application CRUD in steps 1, 2 and the Graph portion of step 5 remain PP checks and can run independently without a Web App.

**Customer goal:** "I want to publish a web application and let users sign in through Microsoft Entra ID."

**Successful outcome:** The deployed page is reachable, an approved test user can sign in through the intended Entra application, and test Azure resources and the application registration are removed afterward.

## Prerequisites

- AzCLIPS CLI and preview PowerShell modules installed and authenticated in PowerShell 7.6+.
- An approved test user and group, Graph access and permission to create an application registration.
- A non-sensitive sample web application and approved runtime/deployment package.

## Shared inputs

Commands use PowerShell syntax. Stop on unexpected errors; check `$LASTEXITCODE` after CLI commands. Use a new application registration, not an existing production application.

```powershell
$ErrorActionPreference = 'Stop'
$az = '<full-path-to-AzCLIPS-executable>'
$sub = '<test-subscription-id>'
$location = '<approved-region>'
$userId = '<approved-test-user-object-ID>'
$groupId = '<approved-test-group-object-ID>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-web-$run"
$appName = "azclips-web-$run"
```

## Customer journey

CLI and PowerShell commands are alternatives. Creating an application registration does not automatically enable Web App authentication or create every required identity object.

| Step | Customer action | Commands (AzCLIPS CLI & AzCLIPS PowerShell) | Expected result / verification |
|---|---|---|---|
| **1. Identify the intended users** | Look up a test user, group and membership. | **AzCLIPS CLI:**<br>`& $az msgraph user show --id $userId`<br>`& $az msgraph user list`<br>`& $az msgraph group show --group $groupId`<br>`& $az msgraph group list`<br>`& $az msgraph group member list --group $groupId`<br>`& $az msgraph group member check --group $groupId --member-id $userId`<br><br>**AzCLIPS PowerShell:**<br>`Get-AzMsGraphUser -Id $userId`<br>`Get-AzMsGraphUser`<br>`Get-AzMsGraphGroup -ObjectId $groupId`<br>`Get-AzMsGraphGroup`<br>`Get-AzMsGraphGroupMember -GroupObjectId $groupId` | IDs and membership match the approved tenant fixtures. An empty list can be valid. Limit evidence to necessary IDs; do not capture tenant-wide personal data. Reading group membership does not configure application access restrictions. |
| **2. Register the application** | Create and inspect a single-tenant application registration. | **AzCLIPS CLI:**<br>`& $az msgraph app create --display-name $appName --sign-in-audience AzureADMyOrg`<br>`& $az msgraph app list --display-name $appName`<br><br>**AzCLIPS PowerShell:**<br>`New-AzMsGraphApplication -DisplayName $appName -SignInAudience AzureADMyOrg`<br>`Get-AzMsGraphApplication -DisplayName $appName`<br><br>Record returned application object `id` as `$appObjectId` and application/client `appId` as `$clientId`.<br>`& $az msgraph app show --id $appObjectId`<br>`Get-AzMsGraphApplication -Id $appObjectId` | Registration has the intended name and sign-in audience. Object ID and client ID are recorded distinctly. No application secret is created or printed. |
| **3. Publish the Web App** | Create a dedicated group, provision the Web App and deploy the sample application. | **AzCLIPS CLI:**<br>`& $az group create --subscription-id $sub --name $rg --location $location`<br><br>**AzCLIPS PowerShell:**<br>`New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location`<br><br>**Pending, both surfaces:** Web App creation commands, plan/runtime options, application-content deployment and endpoint inspection. | Deployed application returns its recognizable page at the intended HTTPS endpoint. Provisioning an empty Web App alone does not meet this checkpoint. |
| **4. Connect sign-in and verify** | Configure the registration and Web App to use each other, then sign in with the test user. | **Pending, AzCLIPS CLI and PowerShell:** redirect URI, Web App authentication configuration, required identity/consent setup and endpoint verification commands. Engineering must confirm whether this connection is supported by dedicated preview commands, an explicit REST variant or external setup.<br><br>**Workload verification:** open the application in a browser, start sign-in, authenticate with the approved test user and return to the application. | Redirect returns to the correct HTTPS application endpoint and establishes the intended signed-in session. Capture only redacted results, not tokens/cookies. A registration and a running website without successful sign-in do not satisfy the scenario. Do not claim group-based authorization unless separately configured and tested. |
| **5. Clean up** | Remove the Web App environment and application registration. | **AzCLIPS CLI:**<br>`& $az group delete --subscription-id $sub --name $rg`<br>`& $az group exists --subscription-id $sub --name $rg`<br>`& $az msgraph app delete --id $appObjectId`<br>`& $az msgraph app show --id $appObjectId`<br><br>**AzCLIPS PowerShell:**<br>`Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force`<br>`Remove-AzMsGraphApplication -Id $appObjectId`<br>`Get-AzMsGraphApplication -Id $appObjectId`<br><br>**Pending:** cleanup of any separately created authentication/identity artifacts from step 4. Sign out of the test browser session. | Group is absent and application lookup reports expected not-found, not an unrelated access error. Graph registration is cleaned up separately because group deletion does not remove it. Never delete the pre-existing user or group. |
