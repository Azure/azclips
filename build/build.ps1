# JAYSON: A custom build script to unblock performance benchmarking GitHub action where PS modules looking for specific Debug directories
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$GenerateHelpArtifacts
)

dotnet build "$PSScriptRoot/../cirrus.slnx" --configuration $Configuration

# Update .psd1 module manifests to reference the correct artifact paths
& "$PSScriptRoot/Set-ModuleConfiguration.ps1" -Configuration $Configuration

# Workaround for Microsoft.PowerShell.PlatyPS 1.0.1 `Export-MamlCommandHelp` not
# converting fenced code blocks under `## EXAMPLES` into `<dev:code>` elements.
# It instead emits the raw markdown (` ```powershell\n...\n``` `) into
# `<maml:introduction>`, which `Get-Help -Examples` then prints verbatim,
# fence markers and all.
#
# This repair walks each `<command:example>` and:
#   1. Finds `<maml:para>` entries that are wholly a fenced code block,
#   2. Joins their bodies (newline-separated) into `<dev:code>`,
#   3. Removes those `<maml:para>` entries from `<maml:introduction>`.
#
# Multiple fences within the same example are concatenated. Prose paragraphs
# stay in the introduction. If `<dev:code>` is already populated, we leave it.
function Repair-MamlExampleCodeBlocks
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path
    )

    [xml]$doc = Get-Content -Path $Path -Raw

    $nsmgr = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $nsmgr.AddNamespace('maml', 'http://schemas.microsoft.com/maml/2004/10')
    $nsmgr.AddNamespace('command', 'http://schemas.microsoft.com/maml/dev/command/2004/10')
    $nsmgr.AddNamespace('dev', 'http://schemas.microsoft.com/maml/dev/2004/10')

    $changed = $false
    $examples = $doc.SelectNodes('//command:example', $nsmgr)
    foreach ($ex in $examples)
    {
        $codeNode = $ex.SelectSingleNode('dev:code', $nsmgr)
        if ($codeNode -and -not [string]::IsNullOrWhiteSpace($codeNode.InnerText))
        {
            continue
        }

        $intro = $ex.SelectSingleNode('maml:introduction', $nsmgr)
        if (-not $intro) { continue }

        $codeFragments = New-Object System.Collections.Generic.List[string]
        $parasToRemove = New-Object System.Collections.Generic.List[System.Xml.XmlNode]
        foreach ($para in $intro.SelectNodes('maml:para', $nsmgr))
        {
            $text = $para.InnerText
            if ([string]::IsNullOrWhiteSpace($text)) { continue }

            # Match a paragraph whose ENTIRE content is one fenced block. The
            # opening fence may be tagged (` ```powershell `, ` ```pwsh `,
            # ` ```ps `) or bare. The closing fence is just ` ``` ` on its own
            # line. We don't try to merge prose-and-fence paragraphs because
            # PlatyPS always emits fences as their own `<maml:para>`.
            if ($text -match '(?ms)^\s*```[a-zA-Z0-9_-]*\s*\r?\n(?<body>.*?)\r?\n```\s*$')
            {
                $codeFragments.Add($Matches['body'])
                $parasToRemove.Add($para)
            }
        }

        if ($codeFragments.Count -eq 0) { continue }

        if (-not $codeNode)
        {
            $codeNode = $doc.CreateElement('dev', 'code', 'http://schemas.microsoft.com/maml/dev/2004/10')
            # `<dev:code>` belongs after `<maml:introduction>`. Insert it there
            # so the resulting XML round-trips back through Import-MamlCommandHelp.
            $ex.InsertAfter($codeNode, $intro) | Out-Null
        }
        $codeNode.InnerText = ($codeFragments -join "`n`n")

        foreach ($p in $parasToRemove)
        {
            $intro.RemoveChild($p) | Out-Null
        }

        $changed = $true
    }

    if ($changed)
    {
        $doc.Save($Path)
    }
}

if ($GenerateHelpArtifacts)
{
    $repoRoot = Resolve-Path "$PSScriptRoot/.."
    $artifactsRoot = Join-Path $repoRoot "artifacts/$Configuration"
    $outputRoot = Join-Path $repoRoot "artifacts/help"

    # Ensure PlatyPS (and any other build-time PS modules) are installed and pinned.
    & "$PSScriptRoot/Install-BuildDependencies.ps1"

    dotnet run --project "$repoRoot/tools/Help/Cirrus.Help.Tool/Cirrus.Help.Tool.csproj" -- --artifacts-root "$artifactsRoot" --output-root "$outputRoot"
    $helpToolExitCode = $LASTEXITCODE

    # Generate MAML XML from per-module markdown using Microsoft.PowerShell.PlatyPS 1.x.
    # Pipeline: Import-MarkdownCommandHelp (reads legacy schema 2.0.0 + new schema)
    # → Export-MamlCommandHelp. The new tool reads `external help file:` from the .md
    # frontmatter and writes `<that-name>.xml` into a per-module subfolder, e.g.
    # `<outDir>/Compute/Microsoft.Azure.Cirrus.Cmdlets.Compute.dll-Help.xml`.
    #
    # Run the pipeline in an isolated child pwsh session: `dotnet build` loads its
    # own YamlDotNet into the parent process, and Microsoft.PowerShell.PlatyPS 1.0.1
    # bundles its own copy — same-name assembly conflict if loaded in one AppDomain.
    #
    # MAML is written into:
    #  - src/PS/<Module>/en-US (gitignored — see .gitignore `src/PS/*/en-US/`)
    #  - artifacts/Debug/PS/<Module>/en-US (next to the staged .psd1; what `Get-Help`
    #    resolves when the module is imported from artifacts).
    $psHelpXmlCount = 0
    $stagedPsRoot = Join-Path $artifactsRoot "PS"
    Get-ChildItem "$repoRoot/src/PS" -Directory | ForEach-Object {
        $moduleDir = $_
        $helpDir = Join-Path $moduleDir.FullName "help"
        if (-not (Test-Path $helpDir)) { return }
        if (-not (Get-ChildItem -Path $helpDir -Filter *.md -ErrorAction SilentlyContinue)) { return }

        $enUs = Join-Path $moduleDir.FullName "en-US"
        New-Item -ItemType Directory -Force -Path $enUs | Out-Null

        $tempOut = Join-Path ([System.IO.Path]::GetTempPath()) ("CirrusMaml-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $tempOut | Out-Null
        try
        {
            pwsh -NoProfile -Command {
                param($helpDirArg, $outDirArg)
                $ErrorActionPreference = 'Stop'
                Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1
                Get-ChildItem -Path $helpDirArg -Filter *.md -File |
                    ForEach-Object { Import-MarkdownCommandHelp -Path $_.FullName } |
                    Export-MamlCommandHelp -OutputFolder $outDirArg -Force | Out-Null
            } -Args $helpDir, $tempOut
            if ($LASTEXITCODE -ne 0) { throw "Microsoft.PowerShell.PlatyPS pipeline failed for module '$($moduleDir.Name)' (exit $LASTEXITCODE)." }

            $produced = Get-ChildItem -Path $tempOut -Recurse -Filter '*.xml' -File
            if (-not $produced) { throw "PlatyPS produced no MAML for module '$($moduleDir.Name)'." }

            foreach ($xml in $produced)
            {
                # Post-fix Microsoft.PowerShell.PlatyPS 1.0.1 quirk: Export-MamlCommandHelp
                # serializes triple-backtick fenced code blocks as literal `<maml:para>```...```</maml:para>`
                # instead of populating `<dev:code>`. `Get-Help -Examples` then renders the
                # raw markdown fence markers to the user. We walk each `<command:example>`,
                # extract the first fenced block out of `<maml:introduction>` and move its
                # body into `<dev:code>`. Surviving prose stays in `<maml:introduction>`.
                Repair-MamlExampleCodeBlocks -Path $xml.FullName

                Copy-Item -Path $xml.FullName -Destination (Join-Path $enUs $xml.Name) -Force

                $stagedEnUs = Join-Path $stagedPsRoot (Join-Path $moduleDir.Name "en-US")
                if (Test-Path (Join-Path $stagedPsRoot $moduleDir.Name))
                {
                    New-Item -ItemType Directory -Force -Path $stagedEnUs | Out-Null
                    Copy-Item -Path $xml.FullName -Destination $stagedEnUs -Force
                }
            }

            $psHelpXmlCount++
        }
        finally
        {
            Remove-Item -Path $tempOut -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Host "Generated MAML help for $psHelpXmlCount PowerShell module(s)."

    # Propagate the help tool's exit code so CI fails when help validation fails.
    if ($helpToolExitCode -ne 0)
    {
        exit $helpToolExitCode
    }
}
