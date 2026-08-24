# Skill: TspCompile
# Compiles a TypeSpec RP through our azclips emitter, producing azclips-ir.json.
# Params: { rp, tspDir }
#
# Returns @{ verdict, notes, buildOutput, logPath }

param(
    [Parameter(Mandatory)][string]$TodoId,
    [Parameter(Mandatory)][string]$ParamsJson,
    [Parameter(Mandatory)][string]$LogDir
)

$ErrorActionPreference = 'Stop'
$params = $ParamsJson | ConvertFrom-Json
$rp     = $params.rp
$tspDir = $params.tspDir

$log = Join-Path $LogDir "tsp-compile-$TodoId-$((Get-Date -Format 'yyyyMMdd-HHmmss')).log"
"[tsp-compile] start rp=$rp tspDir=$tspDir  $(Get-Date -Format o)" | Add-Content $log

if (-not (Test-Path $tspDir)) {
    return @{ verdict='fail'; notes="tspDir not found: $tspDir"; logPath=$log }
}

$emitterDir = "C:\Users\wangalex\source\repos\azclips\tools\AzclipsCodegenTsp\emitter"
if (-not (Test-Path (Join-Path $emitterDir 'dist/index.js'))) {
    "[tsp-compile] emitter not built at $emitterDir\dist" | Add-Content $log
    return @{ verdict='fail'; notes="emitter not built (run npm run build in $emitterDir)"; logPath=$log }
}

$outDir = "C:\Users\wangalex\source\repos\azclips\tools\AgentFactory\ir-output\$rp"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

# Repo-relative path to the TypeSpec entrypoint being compiled (e.g.
# "specification/storage/Storage.Management"), recorded verbatim into
# azclips-ir.json's SourceSpec field so generated file headers can cite the
# exact spec source instead of a hard-coded "swagger" label. Derived by
# stripping the "specification\..." suffix out of the absolute $tspDir —
# every RP tspDir passed to this skill lives under
# <specs-repo>\specification\<...>.
$specsRepoMarker = 'specification'
$specIdx = $tspDir.IndexOf("\$specsRepoMarker\", [StringComparison]::OrdinalIgnoreCase)
$sourceSpec = if ($specIdx -ge 0) {
    ($tspDir.Substring($specIdx + 1) -replace '\\', '/')
} else {
    $tspDir -replace '\\', '/'
}

# SourceSpec alone only names a directory whose contents change over time —
# pin the exact git commit of the azure-rest-api-specs checkout so a
# generated file's provenance is actually reproducible/verifiable. Only
# trust HEAD if the compiled spec subtree itself has no uncommitted local
# changes; otherwise leave $sourceCommit empty rather than record a
# misleading SHA (the emitter treats "" as "unknown", not "assume HEAD").
$sourceCommit = ''
if ($specIdx -ge 0) {
    $specsRepoRoot = $tspDir.Substring(0, $specIdx)
    $specRelPath = $tspDir.Substring($specIdx + 1) -replace '\\', '/'
    try {
        $headSha = (& git -C $specsRepoRoot rev-parse HEAD 2>$null) | Select-Object -First 1
        $dirtyFiles = & git -C $specsRepoRoot status --porcelain -- $specRelPath 2>$null
        if ($LASTEXITCODE -eq 0 -and $headSha -and -not $dirtyFiles) {
            $sourceCommit = $headSha.Trim()
        } elseif ($dirtyFiles) {
            "[tsp-compile] specs checkout has uncommitted changes under $specRelPath — SourceCommit left empty" | Add-Content $log
        }
    } catch {
        "[tsp-compile] could not resolve specs repo commit: $($_.Exception.Message)" | Add-Content $log
    }
}

Push-Location $tspDir
try {
    # Compile from the spec dir so tsp finds the spec's node_modules (@typespec/compiler, rulesets,
    # azure-core, etc. all pinned to the version the tspconfig expects). Our emitter is passed by
    # absolute path — tsp loads it from anywhere.
    # NOTE: This requires `npm install` to have been run in the specs repo root at least once,
    # so the specs-pinned toolchain is present.
    $emitterOptions = @("--option", "@azclips/typespec-emitter.source-spec=$sourceSpec")
    if ($sourceCommit) {
        $emitterOptions += @("--option", "@azclips/typespec-emitter.source-commit=$sourceCommit")
    }
    $stdout = & npx --yes -p "@typespec/compiler" tsp compile . --emit "$emitterDir" --output-dir "$outDir" @emitterOptions 2>&1
    $stdout | Add-Content $log
    if ($LASTEXITCODE -ne 0) {
        return @{ verdict='fail'; notes="tsp compile exited $LASTEXITCODE"; buildOutput=($stdout | Out-String); logPath=$log }
    }
} finally {
    Pop-Location
}

$ir = Join-Path $outDir "azclips-ir.json"
if (-not (Test-Path $ir)) {
    # TypeSpec places emitter output under <outDir>/@<pkg>/<emitter-name>/. Probe there.
    $nested = Get-ChildItem $outDir -Recurse -Filter azclips-ir.json -EA SilentlyContinue | Select-Object -First 1
    if ($nested) { $ir = $nested.FullName }
}
if (-not (Test-Path $ir)) {
    return @{ verdict='fail'; notes="emitter produced no azclips-ir.json"; logPath=$log }
}
$size = (Get-Item $ir).Length
"[tsp-compile] wrote $ir ($size bytes)" | Add-Content $log

# ── AUTO-ENQUEUE DOCGEN + NAMEGRAPH ──
$repo = 'C:\Users\wangalex\source\repos\azclips'
$db = "$repo\tools\AgentFactory\factory.db"
$enqueueNote = ''
if (Test-Path $db) {
    try {
        Import-Module PSSQLite -ErrorAction Stop
        $id = "docgen-$($rp.ToLower())"
        $paramsStr = ('{{"rp":"{0}"}}' -f $rp)
        Invoke-SqliteQuery -DataSource $db -Query @"
INSERT OR IGNORE INTO todos(id, title, description, status, skill, params_json)
VALUES(@id, @t, @d, 'pending', 'docgen', @p);
"@ -SqlParameters @{ id=$id; t="Auto-docgen for $rp (after tsp compile)"; d="Enqueued by TspCompile skill"; p=$paramsStr } | Out-Null
        $enqueueNote = "; enqueued docgen-$($rp.ToLower())"

        # Also enqueue a name-graph snapshot so we can see what changed
        $ngId = "namegraph-$($rp.ToLower())-$(Get-Date -Format 'HHmmss')"
        Invoke-SqliteQuery -DataSource $db -Query @"
INSERT OR IGNORE INTO todos(id, title, description, status, skill, params_json)
VALUES(@id, @t, @d, 'pending', 'namegraph', @p);
"@ -SqlParameters @{ id=$ngId; t="Snapshot name graph for $rp"; d="Auto-enqueued after tsp compile"; p=$paramsStr } | Out-Null
        $enqueueNote += "; enqueued $ngId"

        # Also enqueue a dataflow snapshot alongside
        $dfId = "dataflow-$($rp.ToLower())-$(Get-Date -Format 'HHmmss')"
        Invoke-SqliteQuery -DataSource $db -Query @"
INSERT OR IGNORE INTO todos(id, title, description, status, skill, params_json)
VALUES(@id, @t, @d, 'pending', 'dataflow', @p);
"@ -SqlParameters @{ id=$dfId; t="Trace body-param dataflow for $rp"; d="Auto-enqueued after tsp compile"; p=$paramsStr } | Out-Null
        $enqueueNote += "; enqueued $dfId"

        "[tsp-compile] $enqueueNote" | Add-Content $log
    } catch {
        "[tsp-compile] auto-enqueue skipped: $($_.Exception.Message)" | Add-Content $log
    }
}

return @{
    verdict='ok'
    notes="ir=$ir size=$size sourceCommit=$(if ($sourceCommit) { $sourceCommit.Substring(0,7) } else { '(unknown)' })$enqueueNote"
    logPath=$log
    evidence = @{
        irPath         = $ir
        irBytes        = $size
        verdict        = 'ok'
        sourceSpec     = $sourceSpec
        sourceCommit   = $sourceCommit
        docgenEnqueued = if ($enqueueNote) { 1 } else { 0 }
    }
}
