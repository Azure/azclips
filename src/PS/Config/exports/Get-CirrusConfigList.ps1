function Get-CirrusConfigList {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [switch] $IncludeDefaults,

        [Parameter(Mandatory = $false)]
        [switch] $AsJson
    )

    begin {
        # Start telemetry activity
        $script:activity = Start-CmdletActivity -CmdletName "Get-CirrusConfigList" -Parameters $PSBoundParameters

        $engine = [Core.DependencyInjection.CirrusServiceProvider]::GetRequiredService([Core.Abstractions.IEngine])
        $outputDelegate = [Core.Abstractions.Runtime.CommandOutputDelegate]{
            param([object]$value, [System.Threading.CancellationToken]$ct)
            $PSCmdlet.WriteObject($value)
            return [System.Threading.Tasks.Task]::CompletedTask
        }
    }

    process {
        try {
            $extensionName = "Config"
            $commandName = "Get-CirrusConfigList"
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New()

            if ($PSBoundParameters.ContainsKey('IncludeDefaults')) {
                $parameters.Add('All', [object]$true)
            }
            if ($PSBoundParameters.ContainsKey('AsJson')) {
                $parameters.Add('Json', [object]$true)
            }

            $moduleName = $MyInvocation.MyCommand.ModuleName
            $moduleVersion = if ($MyInvocation.MyCommand.Version) { $MyInvocation.MyCommand.Version.ToString() } else { $null }
            $invocation = [Core.Abstractions.CommandInvocation]::New($extensionName, $commandName, $parameters, $false, $moduleName, $moduleVersion)
            $response = $engine.Process($invocation, $outputDelegate, [System.Threading.CancellationToken]::None)

            # Mark activity as successful
            if ($null -ne $script:activity) {
                Complete-CmdletActivity -Activity $script:activity -Success $true
                $script:activity = $null
            }
        }
        catch {
            # Mark activity as failed
            if ($null -ne $script:activity) {
                Complete-CmdletActivity -Activity $script:activity -Success $false -ErrorMessage $_.Exception.Message
                $script:activity = $null
            }
            throw
        }
    }
}
