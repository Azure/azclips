@{
RootModule = ''
ModuleVersion = '0.1.6'
CompatiblePSEditions = @('Core')
GUID = '7c7b69d5-7b18-4e6c-9c1c-22d0a14e86c5'
Author = 'Microsoft'
CompanyName = 'Microsoft'
Copyright = '(c) Microsoft. All rights reserved.'
Description = 'Rollup module for Azure PowerShell cmdlets'
PowerShellVersion = '7.6'
RequiredModules = @(
    @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.3' },
    @{ ModuleName = '../Network/Network.psd1'; ModuleVersion = '0.1.2' },
    @{ ModuleName = '../KeyVault/KeyVault.psd1'; ModuleVersion = '0.1.1' },
    @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.4' },
    @{ ModuleName = '../Resources/Resources.psd1'; ModuleVersion = '0.1.4' },
    @{ ModuleName = '../ManagedServiceIdentity/ManagedServiceIdentity.psd1'; ModuleVersion = '0.1.2' },
    @{ ModuleName = '../Config/Config.psd1'; ModuleVersion = '0.1.4' },
    @{ ModuleName = '../MsGraph/MsGraph.psd1'; ModuleVersion = '0.1.4' },
    @{ ModuleName = '../Storage/Storage.psd1'; ModuleVersion = '0.1.0' }
)
FunctionsToExport = @()
CmdletsToExport = @()
VariablesToExport = '*'
AliasesToExport = @()
PrivateData = @{
    PSData = @{
        ReleaseNotes = @'
0.1.6 - 2026-10-26 20:00 UTC
Az.KeyVault 0.1.1
* Accept classic CLI and PowerShell policy inputs alongside REST policy JSON.
* Align certificate results with classic CLI fields and PowerShell properties. Add explicit Wait and PassThru behavior for certificate cmdlets.
* Add secret set, retrieval, listing, version listing, attribute update and soft-delete commands.
* Add Undo-AzKeyVaultSecretRemoval to recover a soft-deleted secret and its versions.
* Return secret metadata by default, with explicit plaintext retrieval through AsPlainText.
* Add certificate creation, import, retrieval, listing, version listing, attribute and policy updates, public download and soft-delete commands.
* Add Undo-AzKeyVaultCertificateRemoval to recover a soft-deleted certificate and its versions.
* Support encrypted PFX and PEM import, public-only DER and PEM export, and bounded certificate creation polling.
* Purge, backup and restore are not exposed in this preview.

Az.Network 0.1.2
* Added 'New-AzVirtualNetworkSubnetConfig'.
* Added 'New-AzNetworkInterface', 'Get-AzNetworkInterface' and 'Remove-AzNetworkInterface'
    - 'New-AzNetworkInterface' creates a network interface with a single IP configuration bound to '-SubnetId', optionally associating a public IP address ('-PublicIpAddressId') and a network security group ('-NetworkSecurityGroupId').
    - 'Get-AzNetworkInterface' serves both the single-interface 'Get' set and the 'List' set; omitting '-ResourceGroupName' lists every network interface in the subscription.
* Added 'New-AzNetworkSecurityGroup', 'Get-AzNetworkSecurityGroup' and 'Remove-AzNetworkSecurityGroup'
    - 'Get-AzNetworkSecurityGroup' serves both the single-group 'Get' set and the 'List' set; omitting '-ResourceGroupName' lists every network security group in the subscription.

Az.Resources 0.1.4
* Added management-group deployment create, get, stop, remove, and test cmdlets.
    - 'New-AzManagementGroupDeployment' and 'Test-AzManagementGroupDeployment' take the same template and parameter inputs as the resource group deployment cmdlets ('-TemplateFile', '-TemplateUri', '-TemplateSpecId', '-ParametersFile', '-ParametersUri', '-Parameter', '-Mode', '-ValidationLevel').
    - 'New-AzManagementGroupDeployment' validates using 'Incremental' mode before deploying with the selected '-Mode' and outputs 'DeploymentExtended'.
    - 'Test-AzManagementGroupDeployment' always validates using 'Incremental' mode, outputs 'DeploymentValidateResult', and throws on validation errors.
    - 'Get-AzManagementGroupDeployment', 'Stop-AzManagementGroupDeployment', and 'Remove-AzManagementGroupDeployment' accept '-Id'.
    - 'Remove-AzManagementGroupDeployment' sends delete directly; removing a nonexistent deployment succeeds without output unless '-PassThru' is supplied.
    - 'Stop-AzManagementGroupDeployment -PassThru' outputs 'true' only when the cancellation runs, not under '-WhatIf' or a declined confirmation.

'@
    }
}

}
