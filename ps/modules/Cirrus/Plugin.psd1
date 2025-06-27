@{
    RootModule           = ''
    ModuleVersion        = ''
    GUID                 = ''
    Author               = 'Azure Cirrus Team'
    CompanyName          = 'Microsoft'
    Copyright            = '(c) Microsoft Corporation. All rights reserved.'
    Description          = 'Azure Cirrus PowerShell Module for Cloud Management'
    
    # Require PowerShell 7.2 or later (for TimeOnly support)
    PowerShellVersion    = '7.2'
    CompatiblePSEditions = @('Core')
    
    FunctionsToExport    = '*'
    CmdletsToExport      = '*'
    VariablesToExport    = '*'

    RequiredModules      = @('Cirrus')

    # File list including help files in proper directory structure
    FileList = @()
} 
