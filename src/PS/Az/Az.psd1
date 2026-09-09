@{
RootModule = ''
ModuleVersion = '0.1.2'
CompatiblePSEditions = @('Core')
GUID = '7c7b69d5-7b18-4e6c-9c1c-22d0a14e86c5'
Author = 'Microsoft'
CompanyName = 'Microsoft'
Copyright = '(c) Microsoft. All rights reserved.'
Description = 'Rollup module for Azure PowerShell cmdlets'
PowerShellVersion = '7.6'
RequiredModules = @(
    @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.0' },
    @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.0' },
    @{ ModuleName = '../Storage/Storage.psd1'; ModuleVersion = '0.1.0' },
    @{ ModuleName = '../Resources/Resources.psd1'; ModuleVersion = '0.1.0' },
    @{ ModuleName = '../Identity/Identity.psd1'; ModuleVersion = '0.1.0' },
    @{ ModuleName = '../Config/Config.psd1'; ModuleVersion = '0.1.0' }
)
FunctionsToExport = @()
CmdletsToExport = @()
VariablesToExport = '*'
AliasesToExport = @()
}
