function Get-CirrusConfig {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string] $Key,

        [Parameter(Mandatory = $false)]
        [switch] $AsJson
    )

    begin {
        # Start telemetry activity
        $script:activity = Start-CmdletActivity -CmdletName "Get-CirrusConfig" -Parameters $PSBoundParameters

        $engine = [Core.DependencyInjection.CirrusServiceProvider]::GetRequiredService([Core.Abstractions.IEngine])
    }

    process {
        try {
            $extensionName = "Config"
            $commandName = "Get-CirrusConfig"
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New()

            if ($PSBoundParameters.ContainsKey('Key')) {
                $parameters.Add('Key', [object]$Key)
            }
            if ($PSBoundParameters.ContainsKey('AsJson')) {
                $parameters.Add('Json', [object][bool]$AsJson)
            }

            $moduleName = $MyInvocation.MyCommand.ModuleName
            $moduleVersion = if ($MyInvocation.MyCommand.Version) { $MyInvocation.MyCommand.Version.ToString() } else { $null }
            $invocation = [Core.Abstractions.CommandInvocation]::New($extensionName, $commandName, $parameters, $false, $moduleName, $moduleVersion)
            Invoke-CirrusEngineCommand -Engine $engine -Invocation $invocation -PSCmdlet $PSCmdlet

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
