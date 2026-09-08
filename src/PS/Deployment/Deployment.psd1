@{
    RootModule = ''
    ModuleVersion = '0.1.0'
    GUID = '91ed2fe7-d66d-4a67-874a-c754ac684bf1'
    Author = 'Microsoft Corporation'
    CompanyName = 'Microsoft Corporation'
    Copyright = 'Microsoft Corporation. All rights reserved.'
    Description = 'Azure PowerShell - Deployment'
    PowerShellVersion = '5.1'
    RequiredModules = @(
        @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.0' }
    )
    RequiredAssemblies = '../../../artifacts/Debug/Core/Core.Abstractions.dll',
                         '../../../artifacts/Debug/Core/Core.Telemetry.dll',
                         '../../../artifacts/Debug/Core/Core.Runtime.dll',
                         '../../../artifacts/Debug/Core/Core.Engine.dll',
                         '../../../artifacts/Debug/Core/Core.AssemblyLoading.dll',
                         '../../../artifacts/Debug/Core/Core.ArgumentParsing.dll',
                         '../../../artifacts/Debug/Core/Core.DependencyInjection.dll',
                         '../../../artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Deployment/Microsoft.Azure.Azclips.Extensions.Deployment.dll'
    NestedModules = @('../../../artifacts/Debug/Microsoft.Azure.Azclips.Cmdlets.Deployment.dll')
    FunctionsToExport = @()
    CmdletsToExport = 'New-AzManagementGroupDeployment', 'New-AzManagementGroupDeploymentWhatIf'
}
