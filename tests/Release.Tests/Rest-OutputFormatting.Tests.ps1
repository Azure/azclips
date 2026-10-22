BeforeAll {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    $sourceModule = Join-Path $repoRoot 'src/PS/Resources'
    $sourceManifest = Import-PowerShellDataFile (Join-Path $sourceModule 'Resources.psd1')
}

Describe 'REST response display' {
    It 'shows multiline content in a list without changing the pipeline object' {
        Test-Path -LiteralPath (Join-Path $repoRoot 'artifacts/Debug/RestResponse.format.ps1xml') | Should -BeTrue
        Add-Type -Path (Join-Path $repoRoot 'artifacts/Debug/Core/Core.Abstractions.dll')
        Add-Type -Path (Join-Path $repoRoot 'artifacts/Debug/Microsoft.Azure.Azclips.ExtensionRuntime/Microsoft.Azure.Azclips.ExtensionRuntime.dll')
        Add-Type -Path (Join-Path $repoRoot 'artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Resources/Microsoft.Azure.Azclips.Extensions.Resources.dll')
        Update-FormatData -PrependPath (Join-Path $sourceModule $sourceManifest.FormatsToProcess[0])
        $response = [Microsoft.Azure.Azclips.Extensions.Resources.Models.RestResponse]::new()
        $response.StatusCode = 200
        $response.Content = "{`n  `"name`": `"example`",`n  `"location`": `"eastus`"`n}"
        $response.Headers.Add('x-test', 'kept')
        $document = [System.Text.Json.JsonDocument]::Parse('{"name":"example","location":"eastus"}')
        try {
            $response.Json = $document.RootElement.Clone()
            $rendered = $response | Out-String -Width 100

            $rendered | Should -Match 'StatusCode\s+:\s+200'
            $rendered | Should -Match 'Content\s+:\s+\{'
            $rendered | Should -Match '"name": "example"'
            $rendered | Should -Match '"location": "eastus"'
            $rendered | Should -Match 'Headers\s+:'
            $rendered | Should -Match 'Json\s+:'
            $response.Content | Should -Be "{`n  `"name`": `"example`",`n  `"location`": `"eastus`"`n}"
            $response.Json.GetProperty('name').GetString() | Should -Be 'example'
            $response.Headers['x-test'] | Should -Be 'kept'
        }
        finally {
            $document.Dispose()
        }
    }
}

Describe 'PowerShell formatting asset distribution' {
    It 'stages the formatting files declared in the source manifest' {
        $root = Join-Path $TestDrive 'staging'
        $buildRoot = Join-Path $root 'build'
        $moduleRoot = Join-Path $root 'src/PS/Resources'
        New-Item -ItemType Directory -Path $buildRoot, $moduleRoot -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot 'build/Set-ModuleConfiguration.ps1') -Destination $buildRoot
        Copy-Item -LiteralPath (Join-Path $sourceModule 'Resources.psd1') -Destination $moduleRoot
        foreach ($formatFile in $sourceManifest.FormatsToProcess) {
            Copy-Item -LiteralPath (Join-Path $sourceModule $formatFile) -Destination $moduleRoot
        }

        & (Join-Path $buildRoot 'Set-ModuleConfiguration.ps1') -Configuration Debug

        $stagedRoot = Join-Path $root 'artifacts/Debug/PS/Resources'
        $stagedManifest = Import-PowerShellDataFile (Join-Path $stagedRoot 'Resources.psd1')
        $stagedManifest.FormatsToProcess[0] | Should -Be $sourceManifest.FormatsToProcess[0]
        foreach ($formatFile in $stagedManifest.FormatsToProcess) {
            (Get-Content -LiteralPath (Join-Path $stagedRoot $formatFile) -Raw) |
                Should -Be (Get-Content -LiteralPath (Join-Path $sourceModule $formatFile) -Raw)
        }
        Remove-Item -LiteralPath (Join-Path $moduleRoot $sourceManifest.FormatsToProcess[0])
        { & (Join-Path $buildRoot 'Set-ModuleConfiguration.ps1') -Configuration Debug } | Should -Throw
    }

    It 'includes declared formatting files and references in packaged modules' {
        $scriptPath = Join-Path $repoRoot 'devops/scripts/packaging/powershell/package_az_modules.ps1'
        $tokens = $null
        $parseErrors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
        $parseErrors.Count | Should -Be 0
        foreach ($name in @('Copy-RequiredFile', 'Copy-ExtensionFromPackage', 'New-AzModulePackage')) {
            $function = $ast.Find({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
            }, $false)
            . ([scriptblock]::Create($function.Extent.Text))
        }

        $root = Join-Path $TestDrive 'packaging'
        $artifactsRoot = Join-Path $root 'artifacts'
        $psRoot = Join-Path $root 'src/PS'
        $moduleRoot = Join-Path $psRoot 'Resources'
        $helpRoot = Join-Path $moduleRoot 'en-US'
        $libraryRoot = Join-Path $root 'extension/lib/net10.0'
        New-Item -ItemType Directory -Path $artifactsRoot, $helpRoot, $libraryRoot -Force | Out-Null
        Set-Content -Path (Join-Path $artifactsRoot 'Microsoft.Azure.Azclips.Cmdlets.Resources.dll') -Value 'fixture cmdlet assembly'
        Set-Content -Path (Join-Path $helpRoot 'Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml') -Value '<helpItems />'
        Set-Content -Path (Join-Path $libraryRoot 'Microsoft.Azure.Azclips.Extensions.Resources.dll') -Value 'fixture extension assembly'
        foreach ($formatFile in $sourceManifest.FormatsToProcess) {
            Copy-Item -LiteralPath (Join-Path $sourceModule $formatFile) -Destination $moduleRoot
        }
        $archivePath = Join-Path $root 'extension.zip'
        Compress-Archive -Path (Join-Path $root 'extension/lib') -DestinationPath $archivePath
        Move-Item -LiteralPath $archivePath -Destination (Join-Path $artifactsRoot 'azcli.extensions.resources.0.1.3.nupkg')
        $definition = [pscustomobject]@{
            SourceModule = 'Resources'
            PackageName = 'Az.Resources'
            Version = $sourceManifest.ModuleVersion
            Guid = $sourceManifest.GUID
            ModuleList = @(@{ ModuleName = 'Az.Accounts'; ModuleVersion = '0.1.2' })
            CmdletsToExport = $sourceManifest.CmdletsToExport
            FormatsToProcess = $sourceManifest.FormatsToProcess
            IsRollup = $false
        }

        $packageRoot = New-AzModulePackage -Definition $definition -ArtifactsRoot $artifactsRoot `
            -SourcePsRoot $psRoot -ModulesRoot (Join-Path $root 'modules') -CoreAssemblies @('unused.dll')

        $packagedManifest = Import-PowerShellDataFile (Join-Path $packageRoot 'Az.Resources.psd1')
        @($packagedManifest.FormatsToProcess)[0] | Should -Be $sourceManifest.FormatsToProcess[0]
        foreach ($formatFile in $packagedManifest.FormatsToProcess) {
            (Get-Content -LiteralPath (Join-Path $packageRoot $formatFile) -Raw) |
                Should -Be (Get-Content -LiteralPath (Join-Path $sourceModule $formatFile) -Raw)
        }
        Remove-Item -LiteralPath (Join-Path $moduleRoot $sourceManifest.FormatsToProcess[0])
        {
            New-AzModulePackage -Definition $definition -ArtifactsRoot $artifactsRoot `
                -SourcePsRoot $psRoot -ModulesRoot (Join-Path $root 'modules') -CoreAssemblies @('unused.dll')
        } | Should -Throw '*Required file was not found*'
    }
}
