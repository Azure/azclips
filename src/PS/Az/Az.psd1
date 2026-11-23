@{
RootModule = ''
ModuleVersion = '0.1.8'
CompatiblePSEditions = @('Core')
GUID = '7c7b69d5-7b18-4e6c-9c1c-22d0a14e86c5'
Author = 'Microsoft'
CompanyName = 'Microsoft'
Copyright = '(c) Microsoft. All rights reserved.'
Description = 'Rollup module for Azure PowerShell cmdlets'
PowerShellVersion = '7.6'
RequiredModules = @(
    @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.5' },
    @{ ModuleName = '../Network/Network.psd1'; ModuleVersion = '0.1.4' },
    @{ ModuleName = '../KeyVault/KeyVault.psd1'; ModuleVersion = '0.1.3' },
    @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.6' },
    @{ ModuleName = '../Resources/Resources.psd1'; ModuleVersion = '0.1.6' },
    @{ ModuleName = '../ManagedServiceIdentity/ManagedServiceIdentity.psd1'; ModuleVersion = '0.1.4' },
    @{ ModuleName = '../Config/Config.psd1'; ModuleVersion = '0.1.6' },
    @{ ModuleName = '../MsGraph/MsGraph.psd1'; ModuleVersion = '0.1.6' },
    @{ ModuleName = '../Storage/Storage.psd1'; ModuleVersion = '0.1.2' }
)
FunctionsToExport = @()
CmdletsToExport = @()
VariablesToExport = '*'
AliasesToExport = @()
PrivateData = @{
    PSData = @{
        ReleaseNotes = @'
0.1.8 - 2026-11-15 20:17 UTC
Az.Accounts 0.1.5
* Initial sign-in no longer inherits argument defaults from the active profile; supply subscription ID explicitly.
* Managed identity sign-in now requires a subscription ID. Profile re-authentication no longer accepts a subscription ID.

Az.ManagedServiceIdentity 0.1.4
* Added federated identity credential lifecycle cmdlets
    - 'New-AzFederatedIdentityCredential', 'Get-AzFederatedIdentityCredential', 'Update-AzFederatedIdentityCredential', 'Remove-AzFederatedIdentityCredential'

Az.Resources 0.1.6
* Added tenant deployment cmdlets: 'New-AzTenantDeployment', 'Test-AzTenantDeployment', 'Get-AzTenantDeploymentWhatIfResult', 'Get-AzTenantDeployment', 'Stop-AzTenantDeployment', 'Remove-AzTenantDeployment', and 'Save-AzTenantDeploymentTemplate'.
    - 'New-AzTenantDeployment', 'Test-AzTenantDeployment', and 'Get-AzTenantDeploymentWhatIfResult' take '-Location' plus the resource group deployment template and parameter inputs ('-TemplateFile', '-TemplateUri', '-TemplateSpecId', '-QueryString', '-ParametersFile', '-ParametersUri', '-Parameter', '-ValidationLevel'). '-Name' is optional and defaults to a generated GUID. Tenant deployments always use 'Incremental' mode, so there is no '-Mode'.
    - 'New-AzTenantDeployment' validates before deploying and waits for the deployment to finish. '-NoWait' returns the initial 'DeploymentExtended' once the deployment is submitted; it does not create a PowerShell job.
    - 'Test-AzTenantDeployment' outputs 'DeploymentValidateResult' and throws on validation errors. 'Get-AzTenantDeploymentWhatIfResult' outputs the raw what-if result and accepts '-ResultFormat' ('FullResourcePayloads' or 'ResourceIdOnly').
    - 'Get-AzTenantDeployment' lists every tenant deployment, gets one by '-Name', or lists with an OData '-Filter'.
    - 'Save-AzTenantDeploymentTemplate' writes the exported template to '-Path' (default: the current location) and requires '-Force' to overwrite an existing file.
* Added subscription-scope deployment cmdlets 'New-AzSubDeployment', 'Test-AzSubDeployment', 'Get-AzSubDeployment', 'Stop-AzSubDeployment', 'Remove-AzSubDeployment', and 'Save-AzSubDeploymentTemplate'
    - Legacy '*-AzSubscriptionDeployment' and 'Save-AzSubscriptionDeploymentTemplate' names are accepted as aliases
    - 'Stop-AzSubDeployment', 'Remove-AzSubDeployment', 'Get-AzSubDeployment', and 'Save-AzSubDeploymentTemplate' accept a deployment piped from 'Get-AzSubDeployment' ('-InputObject') and use the subscription in its 'Id'; '-PassThru' on Stop/Remove outputs 'true' when the cancel or delete runs
    - 'New-AzSubDeployment' reports the deployment's ARM error, up to 3 failed operation errors, and the correlation id when the deployment fails
    - 'New-AzSubDeployment' validates the deployment before submitting it and throws 'Deployment validation failed.' without creating a deployment when validation fails
* Added 'Get-AzSubDeploymentWhatIfResult' to preview subscription-scope deployment changes
    - The legacy 'Get-AzSubscriptionDeploymentWhatIfResult' name is accepted as an alias
    - The preview always uses 'Incremental' mode (no '-Mode'), '-Name' is optional, and '-ResultFormat' selects 'FullResourcePayloads' or 'ResourceIdOnly'
* When '-Name' is omitted, 'New-AzSubDeployment', 'Test-AzSubDeployment', 'Test-AzResourceGroupDeployment', and 'Test-AzManagementGroupDeployment' name the deployment after the template file or URI instead of a GUID
    - A GUID is still used when no template file or URI is given, or the derived name is not a valid deployment name

Az.Storage 0.1.2
* Added Blob Storage data-plane cmdlets
    - 'New-AzStorageContainer', 'Remove-AzStorageContainer'
    - 'Get-AzStorageBlob', 'Remove-AzStorageBlob', 'Set-AzStorageBlobContent', 'Get-AzStorageBlobContent', 'Start-AzStorageBlobCopy', 'Copy-AzStorageBlob'
* Added Azure Files data-plane cmdlets
    - 'New-AzStorageShare', 'Get-AzStorageShare', 'Update-AzStorageShare', 'Remove-AzStorageShare'
    - 'New-AzStorageDirectory', 'Get-AzStorageDirectory', 'Remove-AzStorageDirectory'
    - 'New-AzStorageFile', 'Get-AzStorageFile', 'Remove-AzStorageFile', 'Set-AzStorageFileContent', 'Get-AzStorageFileContent', 'Start-AzStorageFileCopy'
* Data-plane cmdlets authenticate with '-StorageAccountName' and '-StorageAccountKey', or with '-SasToken'

'@
    }
}

}
