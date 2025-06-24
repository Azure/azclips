@{
    RootModule           = 'Cirrus.psm1'
    ModuleVersion        = '0.0.1'
    GUID                 = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'
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

    # File list including help files in proper directory structure
    FileList = @(
        'Cirrus.psm1',
        'Cirrus.dll',
        'cirrus_ps.dll',
        'Cirrus-help.xml'
    )
} 