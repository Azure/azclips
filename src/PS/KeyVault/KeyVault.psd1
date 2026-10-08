@{
    RootModule = ''
    ModuleVersion = '0.1.0'
    CompatiblePSEditions = @('Core')
    GUID = '91be1979-845d-4cb0-9462-e0f430c19fd6'
    Author = 'Microsoft Corporation'
    CompanyName = 'Microsoft Corporation'
    Description = 'Key Vault secret lifecycle commands for Azclips'
    PowerShellVersion = '7.6'
    RequiredModules = @(@{ ModuleName = 'Accounts'; ModuleVersion = '0.1.0' })
    RequiredAssemblies = @(
        '../../../artifacts/Debug/Core/Core.Abstractions.dll'
        '../../../artifacts/Debug/Core/Core.Telemetry.dll'
        '../../../artifacts/Debug/Core/Core.Runtime.dll'
        '../../../artifacts/Debug/Core/Core.Engine.dll'
        '../../../artifacts/Debug/Core/Core.AssemblyLoading.dll'
        '../../../artifacts/Debug/Core/Core.ArgumentParsing.dll'
        '../../../artifacts/Debug/Core/Core.DependencyInjection.dll'
        '../../../artifacts/Debug/Microsoft.Azure.Azclips.Extensions.KeyVault/Microsoft.Azure.Azclips.Extensions.KeyVault.dll'
    )
    NestedModules = @('../../../artifacts/Debug/Microsoft.Azure.Azclips.Cmdlets.KeyVault.dll')
    FunctionsToExport = @()
    CmdletsToExport = @('Set-AzKeyVaultSecret', 'Get-AzKeyVaultSecret', 'Update-AzKeyVaultSecret', 'Remove-AzKeyVaultSecret', 'Undo-AzKeyVaultSecretRemoval')
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('Azure', 'KeyVault', 'Azclips')
            LicenseUri = 'https://aka.ms/azps-license'
            ProjectUri = 'https://github.com/Azure/azclips'
            ReleaseNotes = 'Initial secret lifecycle support. Metadata output by default; plaintext retrieval requires AsPlainText.'
        }
    }
}