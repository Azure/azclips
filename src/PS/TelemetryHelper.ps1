# PowerShell Telemetry Helper
# Provides telemetry integration for PowerShell cmdlets using the Core libraries

# Global telemetry service instance
$script:TelemetryService = $null

function Get-TelemetryService {
    if ($null -eq $script:TelemetryService) {
        try {
            # Ensure Core.Engine is loaded and accessible
            # We assume the module loading this script has loaded the necessary DLLs via RequiredAssemblies

            $task = [Core.Engine.Bootstrapper]::ServiceProviderAsync
            $task.Wait()
            $provider = $task.Result
            $script:TelemetryService = $provider.GetService([Core.Abstractions.ITelemetryService])
        }
        catch {
            Write-Verbose "Telemetry initialization failed locally: $_"
        }
    }
    return $script:TelemetryService
}

function Start-CmdletActivity {
    <#
    .SYNOPSIS
        Starts a new telemetry activity for a cmdlet.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$CmdletName,

        [Parameter(Mandatory = $false)]
        [System.Collections.IDictionary]$Parameters
    )

    $service = Get-TelemetryService
    if ($null -ne $service) {
        try {
            # Call the extension method as a static method
            return [Core.Telemetry.TelemetryExtensions]::StartPowerShellActivity($service, $CmdletName, $Parameters)
        }
        catch {
            Write-Verbose "Failed to start activity: $_"
        }
    }
    return $null
}

function Complete-CmdletActivity {
    <#
    .SYNOPSIS
        Completes a telemetry activity.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        $Activity,

        [Parameter(Mandatory = $false)]
        [bool]$Success = $true,

        [Parameter(Mandatory = $false)]
        [string]$ErrorMessage = ""
    )

    if ($null -ne $Activity) {
        try {
            if ($Success) {
                $Activity.SetStatus([System.Diagnostics.ActivityStatusCode]::Ok) | Out-Null
            }
            else {
                $Activity.SetStatus([System.Diagnostics.ActivityStatusCode]::Error, $ErrorMessage) | Out-Null

                # Add error event tag
                $tags = [System.Diagnostics.ActivityTagsCollection]::new()
                $tags.Add("message", $ErrorMessage)
                $ev = [System.Diagnostics.ActivityEvent]::new("Error", [System.DateTimeOffset]::UtcNow, $tags)
                $Activity.AddEvent($ev) | Out-Null
            }
            $Activity.Dispose()
        }
        catch {
            Write-Verbose "Failed to complete activity: $_"
        }
    }
}

function Stop-Telemetry {
    <#
    .SYNOPSIS
        Stops and disposes the telemetry service.
    #>
    if ($null -ne $script:TelemetryService) {
        $script:TelemetryService.Dispose()
        $script:TelemetryService = $null
    }
}

# Export functions
Export-ModuleMember -Function Start-CmdletActivity, Complete-CmdletActivity, Stop-Telemetry