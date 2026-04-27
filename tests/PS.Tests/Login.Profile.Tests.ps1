#requires -Modules @{ ModuleName='Pester'; ModuleVersion='5.0.0' }

BeforeAll {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')
    Import-Module (Join-Path $repoRoot 'src/PS/Login/Login.psd1') -Force | Out-Null

    $script:AuthSvc = [Core.DependencyInjection.CirrusServiceProvider]::GetRequiredService(
        [Core.Abstractions.IAuthContextService])

    $script:Prefix = 'pstst' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
    $script:CirrusRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) '.cirrus'
    $script:ProfilesDir = Join-Path $script:CirrusRoot 'profiles'
    $script:ActivePtr = Join-Path $script:CirrusRoot 'active-profile'

    function Save-TestProfile {
        param([string]$Suffix, [string]$Tenant = 't', [string]$Sub = 's')
        $name = $script:Prefix + $Suffix
        $ctx = [Core.Abstractions.AuthContext]::new($Tenant, $Sub)
        $script:AuthSvc.SaveProfileAsync($name, $ctx, $null, $null,
            [System.Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
        return $name
    }
}

AfterAll {
    if (Test-Path $script:ProfilesDir) {
        Get-ChildItem $script:ProfilesDir -Filter ($script:Prefix + '*.json') -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $script:ActivePtr) {
        $v = (Get-Content $script:ActivePtr -Raw).Trim()
        if ($v.StartsWith($script:Prefix)) { Remove-Item $script:ActivePtr -Force -ErrorAction SilentlyContinue }
    }
}

Describe 'Cirrus.Login profile cmdlets' {

    Context 'Module surface' {
        It 'exports the new profile cmdlets' {
            $names = (Get-Command -Module Login).Name
            $names | Should -Contain 'Get-CirrusProfile'
            $names | Should -Contain 'Select-CirrusProfile'
            $names | Should -Contain 'Remove-CirrusProfile'
            $names | Should -Contain 'Rename-CirrusProfile'
        }
    }

    Context 'Get-CirrusProfile (list)' {
        It 'returns saved profiles' {
            $name = Save-TestProfile -Suffix 'list1' -Tenant 'tenant-list' -Sub 'sub-list'

            $result = Get-CirrusProfile | Where-Object { $_.ProfileName -eq $name }

            $result | Should -Not -BeNullOrEmpty
            $result.TenantId | Should -Be 'tenant-list'
            $result.SubscriptionId | Should -Be 'sub-list'
        }
    }

    Context 'Select-CirrusProfile' {
        It 'sets the active profile' {
            $name = Save-TestProfile -Suffix 'sel1' -Tenant 'tenant-sel' -Sub 'sub-sel'

            Select-CirrusProfile -Name $name | Out-Null
            $active = Get-CirrusProfile -Active

            $active.ProfileName | Should -Be $name
            $active.TenantId | Should -Be 'tenant-sel'
            $active.SubscriptionId | Should -Be 'sub-sel'
        }

        It 'throws when the profile does not exist' {
            $missing = $script:Prefix + 'missing'
            { Select-CirrusProfile -Name $missing -ErrorAction Stop } | Should -Throw
        }
    }

    Context 'Get-CirrusProfile -Active' {
        It 'reflects the most recent Select-CirrusProfile' {
            $first  = Save-TestProfile -Suffix 'act1' -Tenant 't1' -Sub 's1'
            $second = Save-TestProfile -Suffix 'act2' -Tenant 't2' -Sub 's2'

            Select-CirrusProfile -Name $first  | Out-Null
            Select-CirrusProfile -Name $second | Out-Null

            (Get-CirrusProfile -Active).ProfileName | Should -Be $second
        }
    }

    Context 'Remove-CirrusProfile' {
        It 'removes a non-active profile' {
            $name = Save-TestProfile -Suffix 'rem1'
            Remove-CirrusProfile -Name $name -Confirm:$false | Out-Null
            (Get-CirrusProfile | Where-Object { $_.ProfileName -eq $name }) | Should -BeNullOrEmpty
        }

        It 'refuses to remove the active profile without -Force' {
            $name = Save-TestProfile -Suffix 'rem2'
            Select-CirrusProfile -Name $name | Out-Null
            { Remove-CirrusProfile -Name $name -Confirm:$false -ErrorAction Stop } | Should -Throw
            # cleanup
            Remove-CirrusProfile -Name $name -Force -Confirm:$false | Out-Null
        }

        It 'removes the active profile when -Force is set and clears the pointer' {
            $name = Save-TestProfile -Suffix 'rem3'
            Select-CirrusProfile -Name $name | Out-Null
            Remove-CirrusProfile -Name $name -Force -Confirm:$false | Out-Null
            { Get-CirrusProfile -Active -ErrorAction Stop } | Should -Throw '*No active profile*'
        }
    }

    Context 'Rename-CirrusProfile' {
        It 'renames a profile and updates the active pointer' {
            $oldName = Save-TestProfile -Suffix 'ren1' -Tenant 't-ren' -Sub 's-ren'
            $newName = $script:Prefix + 'ren1new'
            Select-CirrusProfile -Name $oldName | Out-Null

            Rename-CirrusProfile -Name $oldName -NewName $newName -Confirm:$false | Out-Null

            $allProfiles = @(Get-CirrusProfile)
            $renamed = $allProfiles | Where-Object { $_.ProfileName -eq $newName }
            $original = $allProfiles | Where-Object { $_.ProfileName -eq $oldName }

            $renamed | Should -Not -BeNullOrEmpty
            $renamed.TenantId | Should -Be 't-ren'
            $original | Should -BeNullOrEmpty
            (Get-CirrusProfile -Active).ProfileName | Should -Be $newName
        }

        It 'throws when source profile does not exist' {
            { Rename-CirrusProfile -Name ($script:Prefix + 'nope') -NewName ($script:Prefix + 'target') -Confirm:$false -ErrorAction Stop } |
                Should -Throw
        }
    }
}
