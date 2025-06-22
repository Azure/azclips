# PowerShell Module for Azure Cirrus
# This module provides cmdlets for Azure Cirrus cloud management using Rust backend

# Import the nested module
Import-Module "$PSScriptRoot\Cirrus.dll" -Force

# Initialize the Rust library
Write-Host "Initializing Cirrus module..." -ForegroundColor Cyan
try {
    [Cirrus.Core.PSWrapper]::Initialize()
    Write-Host "✓ Cirrus library initialized successfully" -ForegroundColor Green
}
catch {
    Write-Warning "Failed to initialize Cirrus library: $_"
    Write-Host "Make sure the Rust library (az_ps.dll) is built and available" -ForegroundColor Yellow
}

# Module variables
$script:ModuleVersion = "1.0.0"
$script:ModuleName = "Cirrus"

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
                            Mandatory = $true
                            HelpMessage = '要创建的资源名称'
                        }
                    )
                )
                $params.Add('Name', $nameParam)
                
                $locationParam = New-Object System.Management.Automation.RuntimeDefinedParameter(
                    'Location', [string], @(
                        New-Object System.Management.Automation.ParameterAttribute @{ 
                            Mandatory = $false
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
                            Mandatory = $true
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
                            Mandatory = $false
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

# Export the new functions
Export-ModuleMember -Function @(
    'Get-CirrusVersion',
    'Invoke-CirrusCommand', 
    'New-CirrusResource',
    'Test-DynamicParams',
    'Test-DynamicParamsSimple',
    'Debug-DynamicParams'
)
