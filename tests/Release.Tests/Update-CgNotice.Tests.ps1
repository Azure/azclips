BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '../../devops/scripts/compliance/Update-CgNotice.ps1'
}

Describe 'Update-CgNotice' {
    BeforeEach {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root | Out-Null
        $noticePath = Join-Path $root 'NOTICE'
        $generatedPath = Join-Path $root 'NOTICE.generated'
        [System.IO.File]::WriteAllText($noticePath, "Reviewed attribution`n")
    }

    It 'warns about a different generated NOTICE without replacing the packaged file' {
        [System.IO.File]::WriteAllText($generatedPath, "New official attribution`n")
        $before = [System.IO.File]::ReadAllBytes($noticePath)

        $messages = & $scriptPath -RepoRoot $root -GeneratedPath $generatedPath 6>&1

        ($messages | Out-String) | Should -Match 'task.logissue type=warning'
        [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($noticePath)) |
            Should -Be ([Convert]::ToBase64String($before))
    }

    It 'updates the stable PR NOTICE with official wording and normalized formatting' {
        [System.IO.File]::WriteAllText($generatedPath, "New official attribution  `r`nNext line`t`r`n")

        & $scriptPath -RepoRoot $root -GeneratedPath $generatedPath -UpdateRootNotice

        [System.IO.File]::ReadAllText($noticePath) | Should -BeExactly "New official attribution`nNext line`n"
        [System.IO.File]::ReadAllText($generatedPath) | Should -BeExactly "New official attribution  `r`nNext line`t`r`n"
    }

    It 'does not rewrite an equivalent NOTICE for formatting-only differences' {
        [System.IO.File]::WriteAllText($noticePath, "Reviewed attribution  `r`n")
        [System.IO.File]::WriteAllText($generatedPath, "Reviewed attribution`n")
        $before = [System.IO.File]::ReadAllBytes($noticePath)

        & $scriptPath -RepoRoot $root -GeneratedPath $generatedPath -UpdateRootNotice

        [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($noticePath)) |
            Should -Be ([Convert]::ToBase64String($before))
    }

    It 'fails on missing or empty official output without changing NOTICE (<State>, update=<Update>)' -TestCases @(
        @{ State = 'missing'; Update = $false }
        @{ State = 'missing'; Update = $true }
        @{ State = 'empty'; Update = $false }
        @{ State = 'empty'; Update = $true }
    ) {
        param($State, $Update)

        if ($State -eq 'empty') {
            [System.IO.File]::WriteAllText($generatedPath, " `r`n`t")
        }

        { & $scriptPath -RepoRoot $root -GeneratedPath $generatedPath -UpdateRootNotice:$Update } |
            Should -Throw '*Official CG NOTICE*'
        [System.IO.File]::ReadAllText($noticePath) | Should -BeExactly "Reviewed attribution`n"
    }

    It 'fails when the committed NOTICE is missing or empty (<State>)' -TestCases @(
        @{ State = 'missing' }
        @{ State = 'empty' }
    ) {
        param($State)

        [System.IO.File]::WriteAllText($generatedPath, "New official attribution`n")
        if ($State -eq 'missing') {
            Remove-Item -LiteralPath $noticePath
        }
        else {
            [System.IO.File]::WriteAllText($noticePath, '')
        }

        { & $scriptPath -RepoRoot $root -GeneratedPath $generatedPath } |
            Should -Throw '*Committed root NOTICE*'
    }
}
