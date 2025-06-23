# PowerShell Module for Azure Cirrus
# This module provides cmdlets for Azure Cirrus cloud management using Rust backend

# Import the nested module (initialization happens automatically via CirrusModuleInitializer)
Import-Module "$PSScriptRoot\Cirrus.dll" -Force

# Module variables
$script:ModuleVersion = "1.0.0"
$script:ModuleName = "Cirrus"

# Collect all functions to export (starting with built-in functions)
$script:FunctionsToExport = @(
    'Get-CirrusVersion'
    # 'Invoke-CirrusCommand', 
    # 'New-CirrusResource',
    # 'Test-DynamicParams',
    # 'Test-DynamicParamsSimple',
    # 'Debug-DynamicParams',
    # 'Get-CirrusCmdlets'
)

# Dynamic cmdlet creation
try {
    Write-Host "Creating dynamic cmdlets..." -ForegroundColor Cyan
    
    # Get builtin cmdlets from Rust library (call only once)
    $builtinCmdlets = [Cirrus.Core.PSWrapper]::GetBuiltinCmdlets()
    
    if ($builtinCmdlets -and $builtinCmdlets.Count -gt 0) {
        Write-Host "Found $($builtinCmdlets.Count) builtin cmdlets" -ForegroundColor Green

        # Generate all function scripts at once using PSCmdlet template
        $allFunctionScripts = [Cirrus.Core.PSCmdlet]::GenerateFunctionScripts($builtinCmdlets)

        if (![string]::IsNullOrEmpty($allFunctionScripts)) {
            # Execute all function definitions at once
            Invoke-Expression $allFunctionScripts
            
            # Add all valid cmdlet names to export list
            foreach ($cmdlet in $builtinCmdlets) {
                if (![string]::IsNullOrEmpty($cmdlet.Name) -and ![string]::IsNullOrEmpty($cmdlet.Uri)) {
                    $script:FunctionsToExport += $cmdlet.Name
                    Write-Verbose "Created function: $($cmdlet.Name)"
                }
            }
        }

        Write-Host "✓ Dynamic cmdlets created successfully" -ForegroundColor Green
    }
    else {
        Write-Host "No builtin cmdlets found" -ForegroundColor Yellow
    }
}
catch {
    Write-Warning "Failed to create dynamic cmdlets: $_"
    Write-Host "This may happen if the Rust library is not properly initialized" -ForegroundColor Yellow
}

# # Create 6400 fake functions for performance testing using PSCmdlet template
# try {
#     Write-Host "Creating 6400 fake functions for testing..." -ForegroundColor Cyan
    
#     # Build fake CmdletInfo array
#     $fakeCmdlets = @()
#     for ($i = 1; $i -le 6400; $i++) {
#         $fakeCmdlet = New-Object Cirrus.Core.CmdletInfo
#         $fakeCmdlet.Name = "Invoke-Fake$i"
#         $fakeCmdlet.Uri = "fake://test/function$i"
#         $fakeCmdlets += $fakeCmdlet
        
#         # Show progress every 1000 cmdlets during creation
#         if ($i % 1000 -eq 0) {
#             Write-Host "Built $i fake cmdlet infos..." -ForegroundColor Blue
#         }
#     }
    
#     Write-Host "Generating all fake function scripts using PSCmdlet template..." -ForegroundColor Cyan
    
#     # Generate all fake function scripts at once using PSCmdlet template
#     $allFakeFunctionScripts = [Cirrus.Core.PSCmdlet]::GenerateFunctionScripts($fakeCmdlets)
    
#     if (![string]::IsNullOrEmpty($allFakeFunctionScripts)) {
#         Write-Host "Executing all fake function definitions..." -ForegroundColor Cyan
        
#         # Execute all fake function definitions at once
#         Invoke-Expression $allFakeFunctionScripts
        
#         # Add all fake function names to export list
#         foreach ($fakeCmdlet in $fakeCmdlets) {
#             $script:FunctionsToExport += $fakeCmdlet.Name
#         }
        
#         Write-Host "✓ All 6400 fake functions created successfully using PSCmdlet template" -ForegroundColor Green
#     }
#     else {
#         Write-Warning "No fake function scripts were generated"
#     }
# }
# catch {
#     Write-Warning "Failed to create fake functions using PSCmdlet template: $_"
#     Write-Host "This may indicate an issue with the PSCmdlet.GenerateFunctionScripts method" -ForegroundColor Yellow
# }


function Get-CirrusVersion {
    <#
    .SYNOPSIS
    Gets the version information of the Cirrus module
    
    .DESCRIPTION
    Returns version information about both the PowerShell module and the underlying Rust library
    
    .EXAMPLE
    Get-CirrusVersion
    #>
    [CmdletBinding()]
    param()
    
    try {
        $rustVersion = [Cirrus.Core.PSWrapper]::GetVersion()
        $isInitialized = [Cirrus.Core.PSWrapper]::IsInitialized
        
        [PSCustomObject]@{
            PSModuleName       = $script:ModuleName
            PSModuleVersion    = $script:ModuleVersion
            RustLibraryVersion = $rustVersion
            IsInitialized      = $isInitialized
            LoadedAt           = Get-Date
        }
    }
    catch {
        [PSCustomObject]@{
            PSModuleName       = $script:ModuleName
            PSModuleVersion    = $script:ModuleVersion
            RustLibraryVersion = "Not available"
            IsInitialized      = $false
            LoadedAt           = Get-Date
            Error              = $_.Exception.Message
        }
    }
}

function Invoke-CirrusCommand {
    <#
    .SYNOPSIS
    Invokes a Cirrus command using the Rust backend
    
    .DESCRIPTION
    Executes a command through the Cirrus Rust library with the specified URI and parameters
    
    .PARAMETER Uri
    The command URI to execute
    
    .PARAMETER Parameters
    The parameters to pass to the command (as hashtable)
    
    .EXAMPLE
    Invoke-CirrusCommand -Uri "az://vm/list" -Parameters @{resourceGroup="mygroup"}
    
    .EXAMPLE
    Invoke-CirrusCommand -Uri "az://storage/create" -Parameters @{name="mystorageaccount"; location="eastus"}
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        
        [Parameter()]
        [hashtable]$Parameters = @{}
    )
    
    try {
        # Convert hashtable to JSON
        $parametersJson = $Parameters | ConvertTo-Json -Depth 10 -Compress
        
        # Call the Rust library
        $resultJson = [Cirrus.Core.PSWrapper]::RunCommand($Uri, $parametersJson)
        
        # Parse and return the result
        if ([string]::IsNullOrEmpty($resultJson) -or $resultJson -eq "{}") {
            Write-Host "Command executed successfully (no output)" -ForegroundColor Green
            return $null
        }
        
        $result = $resultJson | ConvertFrom-Json
        return $result
        
    }
    catch {
        Write-Error "Failed to execute Cirrus command: $_"
        throw
    }
}

# Module cleanup when being removed
$ExecutionContext.SessionState.Module.OnRemove = {
    Write-Host "Cirrus module is being removed" -ForegroundColor Yellow
}

function New-CirrusResource {
    <#
    .SYNOPSIS
    最简单的动态参数示例
    
    .DESCRIPTION
    演示动态参数的最基本用法
    
    .PARAMETER ResourceType
    资源类型
    
    .EXAMPLE
    New-CirrusResource -ResourceType VM -Name "MyVM"
    New-CirrusResource -ResourceType Storage -AccountName "MyStorage"
    
    .NOTES
    Tab补全使用方法：
    1. 输入: New-CirrusResource -ResourceType VM 
    2. 按空格，然后输入 - 再按Tab，会显示 -Name 参数
    3. 或者直接输入: New-CirrusResource -ResourceType VM -N 然后Tab补全
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('VM', 'Storage')]
        [string]$ResourceType
    )
    
    DynamicParam {
        Write-Verbose "DynamicParam block called with ResourceType: $ResourceType"
        
        $paramDict = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
        
        # 使用if-eq语句，仿照Test-DynamicParamsSimple的实现
        if ($ResourceType -eq 'VM') {
            Write-Verbose "Adding Name parameter for VM"
            # VM需要Name参数
            $attr = New-Object System.Management.Automation.ParameterAttribute
            $attr.Mandatory = $true
            $attr.HelpMessage = 'VM名称'
            $param = New-Object System.Management.Automation.RuntimeDefinedParameter('Name', [string], $attr)
            $paramDict.Add('Name', $param)
        }
        elseif ($ResourceType -eq 'Storage') {
            Write-Verbose "Adding AccountName parameter for Storage"
            # Storage需要AccountName参数
            $attr = New-Object System.Management.Automation.ParameterAttribute
            $attr.Mandatory = $true
            $attr.HelpMessage = '存储账户名称'
            $param = New-Object System.Management.Automation.RuntimeDefinedParameter('AccountName', [string], $attr)
            $paramDict.Add('AccountName', $param)
        }
        
        Write-Verbose "Returning $($paramDict.Count) dynamic parameters"
        return $paramDict
    }
    
    Process {
        Write-Host "创建 $ResourceType 资源" -ForegroundColor Green
        
        # 简单显示参数
        $PSBoundParameters.GetEnumerator() | ForEach-Object {
            Write-Host "$($_.Key): $($_.Value)" -ForegroundColor Yellow
        }
    }
}

function Test-DynamicParams {
    <#
    .SYNOPSIS
    测试动态参数的Tab补全
    
    .DESCRIPTION
    用于测试和演示动态参数Tab补全的工作原理
    
    .EXAMPLE
    Test-DynamicParams -Type Create
    # 然后Tab补全会显示 -Name 参数
    
    .EXAMPLE  
    Test-DynamicParams -Type Delete
    # 然后Tab补全会显示 -Id 参数
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateSet('Create', 'Delete', 'List')]
        [string]$Type
    )
    
    DynamicParam {
        $params = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
        
        switch ($Type) {
            'Create' {
                $nameParam = New-Object System.Management.Automation.RuntimeDefinedParameter(
                    'Name', [string], @(
                        New-Object System.Management.Automation.ParameterAttribute @{ 
                            Mandatory   = $true
                            HelpMessage = '要创建的资源名称'
                        }
                    )
                )
                $params.Add('Name', $nameParam)
                
                $locationParam = New-Object System.Management.Automation.RuntimeDefinedParameter(
                    'Location', [string], @(
                        New-Object System.Management.Automation.ParameterAttribute @{ 
                            Mandatory   = $false
                            HelpMessage = '资源位置'
                        }
                        New-Object System.Management.Automation.ValidateSetAttribute(
                            'eastus', 'westus', 'centralus'
                        )
                    )
                )
                $params.Add('Location', $locationParam)
            }
            'Delete' {
                $idParam = New-Object System.Management.Automation.RuntimeDefinedParameter(
                    'Id', [string], @(
                        New-Object System.Management.Automation.ParameterAttribute @{ 
                            Mandatory   = $true
                            HelpMessage = '要删除的资源ID'
                        }
                    )
                )
                $params.Add('Id', $idParam)
            }
            'List' {
                $filterParam = New-Object System.Management.Automation.RuntimeDefinedParameter(
                    'Filter', [string], @(
                        New-Object System.Management.Automation.ParameterAttribute @{ 
                            Mandatory   = $false
                            HelpMessage = '过滤条件'
                        }
                    )
                )
                $params.Add('Filter', $filterParam)
            }
        }
        
        return $params
    }
    
    Process {
        Write-Host "执行 $Type 操作" -ForegroundColor Cyan
        $PSBoundParameters.GetEnumerator() | ForEach-Object {
            Write-Host "  $($_.Key) = $($_.Value)" -ForegroundColor Yellow
        }
    }
}

function Test-DynamicParamsSimple {
    <#
    .SYNOPSIS
    更简单的动态参数测试
    
    .DESCRIPTION
    用最简单的方式测试动态参数
    
    .PARAMETER Type
    类型参数
    
    .EXAMPLE
    Test-DynamicParamsSimple -Type A
    # 然后 Tab 补全应该显示 -ParamA
    
    .EXAMPLE
    Test-DynamicParamsSimple -Type B  
    # 然后 Tab 补全应该显示 -ParamB
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('A', 'B')]
        [string]$Type
    )
    
    DynamicParam {
        $dict = New-Object System.Management.Automation.RuntimeDefinedParameterDictionary
        
        if ($Type -eq 'A') {
            $attr = New-Object System.Management.Automation.ParameterAttribute
            $attr.Mandatory = $true
            $param = New-Object System.Management.Automation.RuntimeDefinedParameter('ParamA', [string], $attr)
            $dict.Add('ParamA', $param)
        }
        elseif ($Type -eq 'B') {
            $attr = New-Object System.Management.Automation.ParameterAttribute  
            $attr.Mandatory = $true
            $param = New-Object System.Management.Automation.RuntimeDefinedParameter('ParamB', [string], $attr)
            $dict.Add('ParamB', $param)
        }
        
        return $dict
    }
    
    Process {
        Write-Host "Type: $Type" -ForegroundColor Cyan
        $PSBoundParameters.GetEnumerator() | ForEach-Object {
            Write-Host "$($_.Key) = $($_.Value)" -ForegroundColor Yellow
        }
    }
}

function Debug-DynamicParams {
    <#
    .SYNOPSIS
    调试动态参数
    
    .DESCRIPTION
    帮助调试动态参数问题的函数
    #>
    [CmdletBinding()]
    param()
    
    Write-Host "=== 动态参数调试信息 ===" -ForegroundColor Cyan
    
    # 检查PowerShell版本
    Write-Host "PowerShell版本: $($PSVersionTable.PSVersion)" -ForegroundColor Yellow
    
    # 检查模块信息
    $module = Get-Module Cirrus
    if ($module) {
        Write-Host "Cirrus模块已加载，版本: $($module.Version)" -ForegroundColor Green
    }
    else {
        Write-Host "Cirrus模块未加载" -ForegroundColor Red
    }
    
    # 测试简单的动态参数
    Write-Host "`n测试建议:" -ForegroundColor Cyan
    Write-Host "1. 首先尝试: Test-DynamicParamsSimple -Type A -<Tab>" -ForegroundColor White
    Write-Host "2. 然后尝试: New-CirrusResource -ResourceType VM -<Tab>" -ForegroundColor White
    Write-Host "3. 如果还不行，请重新加载模块:" -ForegroundColor White
    Write-Host "   Remove-Module Cirrus -Force" -ForegroundColor Gray
    Write-Host "   Import-Module .\Cirrus.psm1 -Force" -ForegroundColor Gray
}

function Get-CirrusCmdlets {
    <#
    .SYNOPSIS
    获取所有可用的 Cirrus cmdlets
    
    .DESCRIPTION
    显示所有从 Rust 库动态创建的 cmdlets 信息
    
    .EXAMPLE
    Get-CirrusCmdlets
    #>
    [CmdletBinding()]
    param()
    
    try {
        $builtinCmdlets = [Cirrus.Core.PSWrapper]::GetBuiltinCmdlets()
        
        if ($builtinCmdlets -and $builtinCmdlets.Count -gt 0) {
            Write-Host "Available Cirrus Cmdlets:" -ForegroundColor Cyan
            Write-Host "=========================" -ForegroundColor Cyan
            
            $builtinCmdlets | ForEach-Object {
                if (![string]::IsNullOrEmpty($_.Name)) {
                    Write-Host "Name: $($_.Name)" -ForegroundColor Green
                    Write-Host "URI:  $($_.Uri)" -ForegroundColor Yellow
                    Write-Host "---" -ForegroundColor Gray
                }
            }
            
            Write-Host "`nTotal: $($builtinCmdlets.Count) cmdlets" -ForegroundColor Cyan
        }
        else {
            Write-Host "No builtin cmdlets found" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Error "Failed to get cmdlets: $_"
    }
}

# Export all functions (including dynamically created ones)
Export-ModuleMember -Function $script:FunctionsToExport
