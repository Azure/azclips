@{
    RootModule = ''
    ModuleVersion = '0.1.2'
    CompatiblePSEditions = @('Core')
    GUID = '91be1979-845d-4cb0-9462-e0f430c19fd6'
    Author = 'Microsoft Corporation'
    CompanyName = 'Microsoft Corporation'
    Description = 'Key Vault secret and certificate lifecycle commands for Azclips'
    PowerShellVersion = '7.6'
    RequiredModules = @(@{ ModuleName = 'Accounts'; ModuleVersion = '0.1.4' })
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
    CmdletsToExport = @('Set-AzKeyVaultSecret', 'Get-AzKeyVaultSecret', 'Update-AzKeyVaultSecret', 'Remove-AzKeyVaultSecret', 'Undo-AzKeyVaultSecretRemoval', 'Add-AzKeyVaultCertificate', 'Import-AzKeyVaultCertificate', 'Get-AzKeyVaultCertificate', 'Update-AzKeyVaultCertificate', 'Export-AzKeyVaultCertificate', 'Remove-AzKeyVaultCertificate', 'Undo-AzKeyVaultCertificateRemoval')
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('Azure', 'KeyVault', 'Azclips')
            LicenseUri = 'https://aka.ms/azps-license'
            ProjectUri = 'https://github.com/Azure/azclips'
            ReleaseNotes = @'
* Accept classic CLI and PowerShell policy inputs alongside REST policy JSON.
* Align certificate results with classic CLI fields and PowerShell properties. Add explicit Wait and PassThru behavior for certificate cmdlets.
* Add secret set, retrieval, listing, version listing, attribute update and soft-delete commands.
* Add Undo-AzKeyVaultSecretRemoval to recover a soft-deleted secret and its versions.
* Return secret metadata by default, with explicit plaintext retrieval through AsPlainText.
* Add certificate creation, import, retrieval, listing, version listing, attribute and policy updates, public download and soft-delete commands.
* Add Undo-AzKeyVaultCertificateRemoval to recover a soft-deleted certificate and its versions.
* Support encrypted PFX and PEM import, public-only DER and PEM export, and bounded certificate creation polling.
* Purge, backup and restore are not exposed in this preview.
'@
        }
    }
}
