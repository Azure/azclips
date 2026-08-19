@{
    RootModule = ''
    ModuleVersion = '0.1.0'
    CompatiblePSEditions = 'Core', 'Desktop'
    GUID = '18e10219-c229-4cc0-bbb7-00db322466ef'
    Author = 'Microsoft Corporation'
    CompanyName = 'Microsoft Corporation'
    Copyright = 'Microsoft Corporation. All rights reserved.'
    Description = 'Azure PowerShell - Storage'
    PowerShellVersion = '5.1'
    DotNetFrameworkVersion = '4.7.2'

    RequiredModules = @(
        @{ ModuleName = '../Login/Login.psd1'; ModuleVersion = '0.1.0' }
    )

    RequiredAssemblies = '../../../artifacts/Debug/Core/Core.Abstractions.dll',
                         '../../../artifacts/Debug/Core/Core.Telemetry.dll',
                         '../../../artifacts/Debug/Core/Core.Runtime.dll',
                         '../../../artifacts/Debug/Core/Core.Engine.dll',
                         '../../../artifacts/Debug/Core/Core.AssemblyLoading.dll',
                         '../../../artifacts/Debug/Core/Core.ArgumentParsing.dll',
                         '../../../artifacts/Debug/Core/Core.DependencyInjection.dll',
                         '../../../artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Storage/Microsoft.Azure.Azclips.Extensions.Storage.dll'

    NestedModules = @('../../../artifacts/Debug/Microsoft.Azure.Azclips.Cmdlets.Storage.dll')
    FunctionsToExport = @()
    CmdletsToExport = 'Set-AzStorageBlobContent', 'Get-AzStorageBlobContent'

    PrivateData = @{
        PSData = @{
            Tags = 'Azure', 'Storage', 'Blob', 'AzClips'
            LicenseUri = 'https://aka.ms/azps-license'
            ProjectUri = 'https://github.com/Azure/azclips'
            ReleaseNotes = '* First preview'
        }
    }
}
