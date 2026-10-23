@{
    RootModule = ''
    ModuleVersion = '0.1.0'
    CompatiblePSEditions = @('Core')
    GUID = '68488fbd-a279-4397-a9ec-ab0c5557b43d'
    Author = 'Microsoft Corporation'
    CompanyName = 'Microsoft Corporation'
    Description = 'Profile-backed Blob and Azure Files workflows and explicit ARM account-key listing for Azclips'
    PowerShellVersion = '7.6'
    RequiredModules = @(@{ ModuleName = 'Accounts'; ModuleVersion = '0.1.2' })
    RequiredAssemblies = @(
        '../../../artifacts/Debug/Core/Core.Abstractions.dll'
        '../../../artifacts/Debug/Core/Core.Telemetry.dll'
        '../../../artifacts/Debug/Core/Core.Runtime.dll'
        '../../../artifacts/Debug/Core/Core.Engine.dll'
        '../../../artifacts/Debug/Core/Core.AssemblyLoading.dll'
        '../../../artifacts/Debug/Core/Core.ArgumentParsing.dll'
        '../../../artifacts/Debug/Core/Core.DependencyInjection.dll'
        '../../../artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Storage/Microsoft.Azure.Azclips.Extensions.Storage.dll'
    )
    NestedModules = @('../../../artifacts/Debug/Microsoft.Azure.Azclips.Cmdlets.Storage.dll')
    FunctionsToExport = @()
    CmdletsToExport = @(
        'New-AzStorageContext', 'Get-AzStorageAccountKey', 'Get-AzStorageBlob', 'Remove-AzStorageBlob'
        'Set-AzStorageBlobContent', 'Get-AzStorageBlobContent', 'Start-AzStorageBlobCopy'
        'Set-AzStorageFileContent', 'Get-AzStorageFileContent', 'Get-AzStorageFile', 'New-AzStorageDirectory'
    )
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('Azure', 'Storage', 'Azclips')
            LicenseUri = 'https://aka.ms/azps-license'
            ProjectUri = 'https://github.com/Azure/azclips'
            ReleaseNotes = 'Public-Azure Entra contexts, native Blob transfers/listing/removal/normal copy-start, Azure Files transfers/lookup/directories and explicit ARM account-key listing. Files OAuth requires explicit backup intent. Files copy sources support supplied SAS, supplied key or explicit source-profile ARM key lookup for source-only SAS signing; destination authentication remains Entra. No automatic key fallback.'
        }
    }
}
