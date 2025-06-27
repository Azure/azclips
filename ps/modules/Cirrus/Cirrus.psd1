@{
    RootModule           = 'Cirrus.psm1'
    ModuleVersion        = '0.0.1'
    GUID                 = '78f00c69-868a-473d-832b-5190ac929cc8'
    Author               = 'Azure Cirrus Team'
    CompanyName          = 'Microsoft'
    Copyright            = '(c) Microsoft Corporation. All rights reserved.'
    Description          = 'Azure Cirrus PowerShell Module for Cloud Management'
    
    # Require PowerShell 7.2 or later (for TimeOnly support)
    PowerShellVersion    = '7.2'
    CompatiblePSEditions = @('Core')
    
    FunctionsToExport    = @('Add-AzurePlugin', 'Connect-AzureAccount', 'Disconnect-AzureAccount', 'Get-AzureConfig', 'Get-AzureConfigCloud', 'Get-AzurePlugin', 'Get-AzurePluginDemo', 'Get-CirrusVersion', 'Invoke-AzureAccountGet-access-token', 'Invoke-AzureConfigCloudActivate', 'Invoke-AzureConfigSet', 'Invoke-AzureVersion', 'New-AzureConfigCloud', 'New-AzurePluginDemo', 'Remove-AzureConfigCloud', 'Remove-AzurePlugin', 'Remove-AzurePluginDemo', 'Show-AzureConfigCloud', 'Show-AzurePlugin', 'Show-AzurePluginDemo', 'Update-AzureConfigCloud', 'Update-AzurePlugin', 'Update-AzurePluginDemo')
    CmdletsToExport      = '*'
    VariablesToExport    = '*'

    # File list including help files in proper directory structure
    FileList = @(
        'Cirrus.psm1',
        'Cirrus.dll',
        'cirrus_ps.dll',
        'Cirrus-help.xml'
    )
} 













