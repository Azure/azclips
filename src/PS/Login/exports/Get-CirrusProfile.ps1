function Get-CirrusProfile {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [string] $Name,

        [Parameter(Mandatory = $false)]
        [string] $Profile
    )

    begin {
        $script:activity = Start-CmdletActivity -CmdletName "Get-CirrusProfile" -Parameters $PSBoundParameters

        $engine = [Core.DependencyInjection.CirrusServiceProvider]::GetRequiredService([Core.Abstractions.IEngine])
        $outputDelegate = [Core.Abstractions.Runtime.CommandOutputDelegate]{
            param([object]$value, [System.Threading.CancellationToken]$ct)
            $PSCmdlet.WriteObject($value)
            return [System.Threading.Tasks.Task]::CompletedTask
        }
    }

    process {
        try {
            $extensionName = "Login";
            # If Name is provided, use ShowProfileCommand (Get-CirrusProfile -Name); otherwise ListProfilesCommand
            if ($PSBoundParameters.ContainsKey('Name')) {
                $commandName = "ShowProfileCommand";
            } else {
                $commandName = "ListProfilesCommand";
            }
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            if ($PSBoundParameters.ContainsKey('Name')) {
                $parameters.Add('Name', [object]$Name);
            }
            if ($PSBoundParameters.ContainsKey('Profile')) {
                $parameters.Add('__profile', [object]$Profile);
            }
            $moduleName = $MyInvocation.MyCommand.ModuleName
            $moduleVersion = if ($MyInvocation.MyCommand.Version) { $MyInvocation.MyCommand.Version.ToString() } else { $null }
            $invocation = [Core.Abstractions.CommandInvocation]::New($extensionName, $commandName, $parameters, $false, $moduleName, $moduleVersion)
            $response = $engine.Process($invocation, $outputDelegate, [System.Threading.CancellationToken]::None);

            if ($null -ne $script:activity) {
                Complete-CmdletActivity -Activity $script:activity -Success $true
                $script:activity = $null
            }
        }
        catch {
            if ($null -ne $script:activity) {
                Complete-CmdletActivity -Activity $script:activity -Success $false -ErrorMessage $_.Exception.Message
                $script:activity = $null
            }
            throw
        }
    }

    end {
        if ($null -ne $script:activity) {
            Complete-CmdletActivity -Activity $script:activity -Success $true
            $script:activity = $null
        }
    }
}
