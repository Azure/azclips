Describe 'Windows zip Resources command validation (step 11)' {
    BeforeAll {
        # These assertion-unit regressions do not replace Windows package lifecycle validation.
        $scriptPath = Join-Path $PSScriptRoot '../../devops/scripts/packaging/windows/zip/test_windows_zip_bundled_plus_ext.ps1'
        $source = Get-Content -LiteralPath $scriptPath -Raw
        $start = $source.IndexOf("Write-Host '[11/15] Verify Resources command resolves'", [StringComparison]::Ordinal)
        $end = $source.IndexOf("Write-Host '[12/15] Remove Resources extension'", [StringComparison]::Ordinal)
        if ($start -lt 0 -or $end -le $start) {
            throw 'Could not locate the Windows zip Resources command validation step.'
        }
        $resourcesCommandStep = [scriptblock]::Create("param(`$azExe)`n" + $source.Substring($start, $end - $start))

        function Invoke-ResourcesCommandValidation {
            param([string[]] $Output, [int] $ExitCode)

            $azExe = {
                $args.Count | Should -Be 2
                $args[0] | Should -BeExactly 'group'
                $args[1] | Should -BeExactly 'list'
                # Keep the fake native exit status local to this step invocation.
                Set-Variable -Name LASTEXITCODE -Scope 1 -Value $ExitCode
                $Output
            }
            & $resourcesCommandStep $azExe 6>$null
        }
    }

    It 'accepts <Name>' -TestCases @(
        @{ Name = 'the current missing-profile diagnostic'; Output = "Missing required argument '--subscription-id' and no value found in profile."; ExitCode = 1 }
        @{
            Name = 'a prefixed multiline missing-profile diagnostic'
            Output = @('az: command failed', "Error: Missing required argument '--subscription-id' and no value found in profile.", 'Usage: az group list [options]')
            ExitCode = 2
        }
        @{ Name = 'the legacy missing-profile diagnostic'; Output = "Missing required argument 'SubscriptionId' and no value found in profile."; ExitCode = 1 }
        @{ Name = 'the legacy required-parameter diagnostic'; Output = "Required parameter 'SubscriptionId' is missing"; ExitCode = 1 }
        @{ Name = 'the legacy must-be-provided diagnostic'; Output = 'SubscriptionId must be provided'; ExitCode = 1 }
        @{ Name = 'the legacy cannot-be-resolved diagnostic'; Output = 'SubscriptionId cannot be resolved'; ExitCode = 1 }
        @{ Name = 'an AcquireTokenSilent failure'; Output = 'AcquireTokenSilent failed'; ExitCode = 1 }
        @{ Name = 'a missing account or login hint'; Output = 'No account or login hint was passed'; ExitCode = 1 }
        @{ Name = 'a login request'; Output = 'Please run az login to sign in'; ExitCode = 1 }
        @{ Name = 'an AADSTS error'; Output = 'AADSTS50058: A silent sign-in request failed'; ExitCode = 1 }
        @{ Name = 'an authentication error'; Output = 'An authentication error occurred'; ExitCode = 1 }
        @{ Name = 'successful JSON output'; Output = '[]'; ExitCode = 0 }
        @{ Name = 'successful empty output'; Output = ''; ExitCode = 0 }
    ) {
        param($Output, $ExitCode)

        { Invoke-ResourcesCommandValidation -Output $Output -ExitCode $ExitCode } | Should -Not -Throw
    }

    It 'rejects <Name>' -TestCases @(
        @{
            Name = 'an unresolved command'
            Output = "Command 'group list' could not be found."
            ExitCode = 1
            ExpectedError = 'FAIL: Resources command not resolved after install.'
        }
        @{
            Name = 'an unresolved command even with the current missing-profile diagnostic'
            Output = @("Missing required argument '--subscription-id' and no value found in profile.", "Command 'group list' could not be found.")
            ExitCode = 1
            ExpectedError = 'FAIL: Resources command not resolved after install.'
        }
        @{
            Name = 'an unresolved command even with the legacy missing-profile diagnostic and exit zero'
            Output = @("Missing required argument 'SubscriptionId' and no value found in profile.", "Command 'group list' could not be found.")
            ExitCode = 0
            ExpectedError = 'FAIL: Resources command not resolved after install.'
        }
        @{ Name = 'an unexpected nonzero failure'; Output = 'Unexpected internal failure'; ExitCode = 7; ExpectedError = 'FAIL: group list exited with code 7.' }
        @{ Name = 'a missing profile value for another parameter'; Output = "Missing required argument '--tenant-id' and no value found in profile."; ExitCode = 2; ExpectedError = 'FAIL: group list exited with code 2.' }
        @{ Name = 'a bare subscription option mention'; Output = 'Usage: az group list --subscription-id <subscription>'; ExitCode = 2; ExpectedError = 'FAIL: group list exited with code 2.' }
        @{ Name = 'empty nonzero output'; Output = ''; ExitCode = 3; ExpectedError = 'FAIL: group list exited with code 3.' }
        @{ Name = 'a diagnostic without the literal final period'; Output = "Missing required argument '--subscription-id' and no value found in profile!"; ExitCode = 1; ExpectedError = 'FAIL: group list exited with code 1.' }
    ) {
        param($Output, $ExitCode, $ExpectedError)

        { Invoke-ResourcesCommandValidation -Output $Output -ExitCode $ExitCode } | Should -Throw $ExpectedError
    }
}
