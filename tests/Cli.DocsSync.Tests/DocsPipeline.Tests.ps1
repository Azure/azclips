BeforeAll {
    function Get-PipelineScript {
        param([string]$Path, [string]$End)
        $yaml = Get-Content -Raw -LiteralPath $Path
        $pattern = '(?m)^    - pwsh: \|\r?\n(?<body>(?: {8}[^\r\n]*\r?\n)+?)      ' + [regex]::Escape($End)
        $match = [regex]::Match($yaml, $pattern)
        if (-not $match.Success) { throw "Cannot find pipeline script ending in $End" }
        [scriptblock]::Create([regex]::Replace($match.Groups['body'].Value, '(?m)^ {8}', ''))
    }

    $script:ValidateTag = Get-PipelineScript "$PSScriptRoot/../../.azure-pipelines/templates/dist/stages/sync-preview-docs.yml" 'name: ValidateTag'
    $jobYaml = Get-Content -Raw "$PSScriptRoot/../../.azure-pipelines/templates/dist/jobs/sync-preview-docs.yml"
    $jobMatch = [regex]::Match($jobYaml, '(?m)^      script: \|\r?\n(?<body>(?: {8}[^\r\n]*\r?\n)+?)    env:')
    if (-not $jobMatch.Success) { throw 'Cannot find docs sync job script' }
    $jobScript = [regex]::Replace($jobMatch.Groups['body'].Value, '(?m)^ {8}', '')
    $jobScript = $jobScript.Replace('"$(Build.SourcesDirectory)/build"', '$env:FAKE_BUILD_DIR')
    $script:RunSync = [scriptblock]::Create($jobScript)
    $restoreMatch = [regex]::Match($jobYaml, '(?m)^        inlineScript: \|\r?\n(?<body>(?: {10}[^\r\n]*\r?\n)+?)      env:')
    if (-not $restoreMatch.Success) { throw 'Cannot find CLI2Yaml restore script' }
    $script:RestoreCli2Yaml = [scriptblock]::Create([regex]::Replace($restoreMatch.Groups['body'].Value, '(?m)^ {10}', ''))
    $script:EnvironmentNames = @('RELEASE_TAG', 'EXPECTED_COMMIT', 'DOCS_COMMIT', 'SYNC_MODE', 'FETCH_CLI2YAML', 'SYNC_SCRIPT', 'AGENT_TEMPDIRECTORY', 'FAKE_BUILD_DIR', 'BUILD_SOURCESDIRECTORY', 'ARTIFACTS_CREDENTIALPROVIDER_EXTERNAL_FEED_ENDPOINTS', 'VSS_NUGET_EXTERNAL_FEED_ENDPOINTS', 'ARTIFACTS_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED', 'NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED', 'AZCLIPS_CLI_SYNC_LOAD_ONLY')
}

Describe 'Standalone docs pipeline orchestration' {
    BeforeEach {
        $script:PreviousEnvironment = @{}
        foreach ($name in $script:EnvironmentNames) {
            $script:PreviousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
        }
        $script:Repo = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $script:Repo 'build') -Force | Out-Null
        git -C $script:Repo init --initial-branch=main --quiet
        git -C $script:Repo config user.email 'test@example.com'
        git -C $script:Repo config user.name 'Test'
        foreach ($file in @('Sync-AzClipsCliDocs.ps1', 'Sync-AzclipsPowerShellDocs.ps1', 'Invoke-DocsReleaseCheckout.ps1')) {
            Set-Content -LiteralPath (Join-Path $script:Repo "build/$file") -Value '# fixture'
        }
        git -C $script:Repo add --all
        git -C $script:Repo commit -m 'release' --quiet
        git -C $script:Repo tag 'v1.2.3.20260624T091500Z'
        $script:Commit = (git -C $script:Repo rev-parse HEAD).Trim()
        $env:RELEASE_TAG = 'v1.2.3.20260624T091500Z'
        $env:EXPECTED_COMMIT = 'none'
    }

    AfterEach {
        foreach ($name in $script:EnvironmentNames) {
            [Environment]::SetEnvironmentVariable($name, $script:PreviousEnvironment[$name])
        }
    }

    It 'validates the tag and exposes its resolved commit' {
        $env:EXPECTED_COMMIT = $script:Commit
        Push-Location $script:Repo
        try {
            $output = & $script:ValidateTag 6>&1
            ($output -join "`n") | Should -Match ([regex]::Escape("variable=DocsCommit;isOutput=true]$script:Commit"))
        } finally { Pop-Location }
    }

    It 'rejects malformed, missing, mismatched and unsupported tags' {
        Push-Location $script:Repo
        try {
            $env:RELEASE_TAG = 'not-a-release'
            { & $script:ValidateTag } | Should -Throw '*Select a stable release tag*'
            $env:RELEASE_TAG = 'v1.2.3.20260625T091500Z'
            { & $script:ValidateTag } | Should -Throw '*not found*'
            $env:RELEASE_TAG = 'v1.2.3.20260624T091500Z'
            $env:EXPECTED_COMMIT = '0000000000000000000000000000000000000000'
            { & $script:ValidateTag } | Should -Throw '*does not match*'
            $env:EXPECTED_COMMIT = 'none'
            git rm -q build/Sync-AzclipsPowerShellDocs.ps1
            git commit -m 'remove script' --quiet
            git tag 'v1.2.3.20260626T091500Z'
            $env:RELEASE_TAG = 'v1.2.3.20260626T091500Z'
            { & $script:ValidateTag } | Should -Throw '*does not contain*'
        } finally { Pop-Location }
    }

    It 'runs dry-run without Push and rejects a different tag commit' {
        $fake = Join-Path $script:Repo 'build/fake.ps1'
        Set-Content -LiteralPath $fake -Value @'
param([string]$ReleaseTag, [string]$TargetRepoPath, [switch]$FetchIfMissing, [switch]$Push)
$global:CapturedSyncArgs = $PSBoundParameters
'@
        $env:DOCS_COMMIT = $script:Commit
        $env:SYNC_MODE = 'dry-run'
        $env:FETCH_CLI2YAML = 'True'
        $env:SYNC_SCRIPT = 'fake.ps1'
        $env:FAKE_BUILD_DIR = Join-Path $script:Repo 'build'
        $env:AGENT_TEMPDIRECTORY = $TestDrive
        Push-Location $script:Repo
        try {
            & $script:RunSync
            $global:CapturedSyncArgs.ReleaseTag | Should -Be $env:RELEASE_TAG
            $global:CapturedSyncArgs.FetchIfMissing | Should -BeTrue
            $global:CapturedSyncArgs.ContainsKey('Push') | Should -BeFalse
            $global:CapturedSyncArgs.TargetRepoPath | Should -Be (Join-Path $TestDrive 'azclips-docs-dry-run-target')
            $env:DOCS_COMMIT = '0000000000000000000000000000000000000000'
            { & $script:RunSync } | Should -Throw '*no longer matches*'
        } finally {
            Pop-Location
            Remove-Variable -Name CapturedSyncArgs -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'restores CLI2Yaml using process-scoped workload identity credentials' {
        $fake = Join-Path $script:Repo 'build/Sync-AzClipsCliDocs.ps1'
        $dll = Join-Path $TestDrive 'CLI2Yaml.dll'
        Set-Content -LiteralPath $dll -Value 'fixture'
        Set-Content -LiteralPath $fake -Value @'
function Get-Cli2YamlDllPath {
    param($PackageName, $Feed, [switch]$FetchIfMissing)
    $global:CapturedFeedCredentials = $env:VSS_NUGET_EXTERNAL_FEED_ENDPOINTS
    $global:CapturedFetch = @{ PackageName = $PackageName; Feed = $Feed; FetchIfMissing = $FetchIfMissing }
    return $env:FAKE_DLL
}
'@
        $previousDll = $env:FAKE_DLL
        $env:FAKE_DLL = $dll
        $env:BUILD_SOURCESDIRECTORY = $script:Repo
        $env:DOCS_COMMIT = $script:Commit
        Mock git {
            $global:LASTEXITCODE = 0
            '{"cli2yamlFeed":"https://apidrop.pkgs.visualstudio.com/_packaging/ReferenceAutomation/nuget/v3/index.json","cli2yamlPackage":"CLI2Yaml"}'
        }
        Mock az {
            $global:LASTEXITCODE = 0
            'test-token'
        }
        try {
            $output = & $script:RestoreCli2Yaml 6>&1
            ($output -join "`n") | Should -Not -Match 'test-token'
            $credential = ($global:CapturedFeedCredentials | ConvertFrom-Json).endpointCredentials[0]
            $credential.password | Should -Be 'test-token'
            $credential.endpoint | Should -Be $global:CapturedFetch.Feed
            $global:CapturedFetch.PackageName | Should -Be 'CLI2Yaml'
            $global:CapturedFetch.FetchIfMissing | Should -BeTrue
            $env:VSS_NUGET_EXTERNAL_FEED_ENDPOINTS | Should -BeNullOrEmpty
            $env:ARTIFACTS_CREDENTIALPROVIDER_EXTERNAL_FEED_ENDPOINTS | Should -BeNullOrEmpty
        } finally {
            $env:FAKE_DLL = $previousDll
            Remove-Variable -Name CapturedFeedCredentials, CapturedFetch -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'rejects a tagged config pointing at a different feed before acquiring credentials' {
        $env:DOCS_COMMIT = $script:Commit
        Mock git {
            $global:LASTEXITCODE = 0
            '{"cli2yamlFeed":"https://example.invalid/feed","cli2yamlPackage":"CLI2Yaml"}'
        }
        Mock az { throw 'Unexpected token request' }
        { & $script:RestoreCli2Yaml } | Should -Throw '*does not use the approved CLI2Yaml feed*'
        Should -Invoke az -Times 0 -Exactly
    }
}
