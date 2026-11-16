# Offline fixtures for standalone PowerShell release selection and provisioning.
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$bootstrapRoot = Join-Path $repoRoot 'devops/scripts/packaging/powershell'
$scratch = Join-Path ([IO.Path]::GetTempPath()) "ps-bootstrap-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $scratch
$releases = @'
[
  {"tag_name":"v7.6.9","draft":false,"prerelease":false},
  {"tag_name":"v7.7.0","draft":false,"prerelease":false},
  {"tag_name":"v7.6.12","draft":false,"prerelease":true},
  {"tag_name":"v7.6.11","draft":true,"prerelease":false},
  {"tag_name":"v7.6.10","draft":false,"prerelease":false}
]
'@

function Assert-Bootstrap {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Test-WindowsBootstrap {
    param([string]$Case)
    $root = Join-Path $scratch "windows $Case"
    $null = New-Item -ItemType Directory -Path $root
    $version = if ($Case -eq 'exact') { '7.6.9' } else { '7.6.10' }
    $architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $asset = "PowerShell-$version-win-$architecture.zip"
    $state = @{
        Downloads = [Collections.Generic.List[string]]::new()
        Attempts = 0
        Sleeps = 0
        Extractions = 0
        Executions = 0
        Version = $version
    }
    function Invoke-WebRequest {
        param([string]$Uri, [string]$OutFile, [int]$TimeoutSec)
        $state.Downloads.Add($Uri)
        Assert-Bootstrap ($TimeoutSec -eq 180) 'Windows downloads must have a bounded timeout.'
        if ($Uri -eq 'https://api.github.com/repos/PowerShell/PowerShell/releases?per_page=100') {
            [IO.File]::WriteAllText($OutFile, $(if ($Case -eq 'no-release') { '[]' } else { $releases }))
        }
        elseif ($Uri -eq "https://github.com/PowerShell/PowerShell/releases/download/v$version/$asset") {
            $state.Attempts++
            Assert-Bootstrap (-not (Test-Path -LiteralPath $OutFile)) 'Failed partial Windows downloads must be removed before retry.'
            [IO.File]::WriteAllText($OutFile, 'standalone fixture archive')
            if ($Case -eq 'exhausted' -or ($Case -eq 'retry' -and $state.Attempts -lt 3)) {
                throw 'FIXTURE_RESPONSE_ENDED'
            }
        }
        elseif ($Uri -eq "https://github.com/PowerShell/PowerShell/releases/download/v$version/hashes.sha256") {
            $hash = (Get-FileHash -LiteralPath (Join-Path $root $asset) -Algorithm SHA256).Hash
            if ($Case -eq 'bad-hash') { $hash = '0' * 64 }
            $hashAsset = if ($Case -eq 'missing-hash') { 'another.zip' } else { $asset }
            $encoding = if ($Case -eq 'exact') { [Text.UTF8Encoding]::new($false) } else { [Text.Encoding]::Unicode }
            [IO.File]::WriteAllText($OutFile, "$hash *$hashAsset`r`n", $encoding)
        }
        else { throw "Unexpected bootstrap download: $Uri" }
    }
    function Start-Sleep { param([int]$Seconds) $state.Sleeps++ }
    function Expand-Archive {
        param([string]$LiteralPath, [string]$DestinationPath)
        Assert-Bootstrap ((Split-Path -Leaf $LiteralPath) -ceq $asset) 'Windows selects the standalone ZIP for the native architecture.'
        $state.Extractions++
        Set-Item -LiteralPath "Function:\global:$(Join-Path $DestinationPath 'pwsh.exe')" -Value {
            $state.Executions++
            $global:LASTEXITCODE = if ($Case -eq 'execution-fails') { 42 } else { 0 }
            if ($Case -eq 'wrong-version') { '7.7.0' } else { $state.Version }
        }
    }
    $failure = $null
    try {
        $requested = if ($Case -eq 'exact') { $version } else { '7.6' }
        & (Join-Path $bootstrapRoot 'windows/windows_install_script.ps1') `
            -PowerShellVersion $requested -WorkRoot $root *> (Join-Path $root 'output.log')
    }
    catch { $failure = $_.Exception.Message }
    finally {
        $functionPath = "Function:\global:$(Join-Path (Join-Path $root 'pwsh') 'pwsh.exe')"
        if (Test-Path -LiteralPath $functionPath) { Remove-Item -LiteralPath $functionPath }
    }
    if ($Case -in 'exhausted', 'bad-hash', 'missing-hash', 'no-release', 'wrong-version', 'execution-fails') {
        $expected = switch ($Case) {
            exhausted { 'FIXTURE_RESPONSE_ENDED' }
            bad-hash { 'SHA256 checksum mismatch' }
            missing-hash { 'Expected exactly one SHA256 checksum' }
            no-release { 'No stable release' }
            wrong-version { 'Expected PowerShell 7.6.10' }
            execution-fails { 'exit code 42' }
        }
        Assert-Bootstrap ($failure -like "*$expected*") "Windows $Case must fail for the intended reason; got '$failure'."
        $expectedExtractions = if ($Case -in 'wrong-version', 'execution-fails') { 1 } else { 0 }
        Assert-Bootstrap ($state.Extractions -eq $expectedExtractions) 'Windows must verify the archive before extraction.'
    }
    else {
        Assert-Bootstrap ($null -eq $failure) "Windows $Case failed: $failure"
        Assert-Bootstrap ($state.Executions -eq 1) 'Windows must start and verify the installed PowerShell.'
        $expectedDownloads = if ($Case -eq 'exact') { 2 } elseif ($Case -eq 'retry') { 5 } else { 3 }
        Assert-Bootstrap ($state.Downloads.Count -eq $expectedDownloads) 'Windows release-line resolution and retries make only the expected requests.'
    }
    if ($Case -in 'retry', 'exhausted') {
        $attempts = if ($Case -eq 'retry') { 3 } else { 5 }
        Assert-Bootstrap ($state.Attempts -eq $attempts -and $state.Sleeps -eq ($attempts - 1)) 'Windows transport retries are bounded.'
    }
    Write-Host "PASS: standalone Windows bootstrap $Case"
}

try {
    foreach ($case in 'line', 'exact', 'retry', 'exhausted', 'bad-hash', 'missing-hash', 'no-release', 'wrong-version', 'execution-fails') {
        Test-WindowsBootstrap $case
    }

    $bash = if ($IsWindows) { Join-Path $env:ProgramFiles 'Git/bin/bash.exe' } else { (Get-Command bash -ErrorAction Stop).Source }
    $harness = Join-Path $scratch 'bootstrap-fixture.sh'
    [IO.File]::WriteAllText($harness, @'
#!/bin/sh
set -eu
bootstrap=$1
work_root=$2
fixture_case=$3
powershell_platform=$4
fixture_arch=$5
powershell_version=$6
if command -v cygpath >/dev/null 2>&1; then
  bootstrap=$(cygpath -u "$bootstrap")
  work_root=$(cygpath -u "$work_root")
fi
pwsh_root="$work_root/pwsh"
fixture_version=7.6.10
[ "$fixture_case" != exact ] || fixture_version=7.6.9
uname() { printf '%s\n' "$fixture_arch"; }
sleep() { printf 'sleep\n' >> "$work_root/calls"; }
dotnet() { echo 'Unexpected external dotnet invocation' >&2; return 99; }
curl() {
  fixture_output=
  fixture_url=
  while [ "$#" -gt 0 ]; do
    case "$1" in
      --output) fixture_output=$2; shift 2 ;;
      https:*) fixture_url=$1; shift ;;
      *) shift ;;
    esac
  done
  printf '%s\n' "$fixture_url" >> "$work_root/calls"
  case "$fixture_url" in
    'https://api.github.com/repos/PowerShell/PowerShell/releases?per_page=100')
      cp "$work_root/fixture-releases.json" "$fixture_output" ;;
    "https://github.com/PowerShell/PowerShell/releases/download/v$fixture_version/hashes.sha256")
      cp "$work_root/fixture-hashes" "$fixture_output" ;;
    "https://github.com/PowerShell/PowerShell/releases/download/v$fixture_version/powershell-$fixture_version-$powershell_platform-"*.tar.gz)
      [ ! -e "$fixture_output" ] || { echo 'Partial download was not removed' >&2; return 99; }
      cp "$work_root/fixture-archive" "$fixture_output"
      fixture_attempts=$(grep -c '\.tar\.gz$' "$work_root/calls")
      if [ "$fixture_case" = exhausted ]; then return 92; fi
      if [ "$fixture_case" = retry ] && [ "$fixture_attempts" -lt 3 ]; then
        if [ "$fixture_attempts" -eq 1 ]; then return 35; else return 56; fi
      fi ;;
    *) echo "Unexpected bootstrap download: $fixture_url" >&2; return 99 ;;
  esac
}
tar() {
  printf 'extract\n' >> "$work_root/calls"
  mkdir -p "$pwsh_root"
  printf '%s\n' '#!/bin/sh' "echo $fixture_version" > "$pwsh_root/pwsh"
  if [ "$fixture_case" = execution-fails ]; then printf 'exit 42\n' >> "$pwsh_root/pwsh"; fi
}
. "$bootstrap"
'@)
    $cases = @(
        @('linux-x64', 'linux', 'x86_64'),
        @('linux-arm64', 'linux', 'aarch64'),
        @('musl-x64', 'linux-musl', 'x86_64'),
        @('macos-x64', 'osx', 'x86_64'),
        @('macos-arm64', 'osx', 'arm64'),
        @('exact', 'linux', 'x86_64'),
        @('retry', 'linux', 'x86_64'),
        @('exhausted', 'linux', 'x86_64'),
        @('bad-hash', 'linux', 'x86_64'),
        @('missing-hash', 'linux', 'x86_64'),
        @('no-release', 'linux', 'x86_64'),
        @('execution-fails', 'linux', 'x86_64')
    )
    foreach ($case in $cases) {
        $name, $platform, $architecture = $case
        $root = Join-Path $scratch "shell $name"
        $null = New-Item -ItemType Directory -Path $root
        $version = if ($name -eq 'exact') { '7.6.9' } else { '7.6.10' }
        $requested = if ($name -eq 'exact') { $version } else { '7.6' }
        $arch = if ($architecture -in 'arm64', 'aarch64') { 'arm64' } else { 'x64' }
        $asset = "powershell-$version-$platform-$arch.tar.gz"
        $archive = Join-Path $root 'fixture-archive'
        [IO.File]::WriteAllText($archive, 'standalone fixture archive')
        $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
        if ($name -eq 'bad-hash') { $hash = '0' * 64 }
        $hashAsset = if ($name -eq 'missing-hash') { 'another.tar.gz' } else { $asset }
        $encoding = if ($name -eq 'exact') { [Text.UTF8Encoding]::new($false) } else { [Text.Encoding]::Unicode }
        [IO.File]::WriteAllText((Join-Path $root 'fixture-hashes'), "$hash *$hashAsset`r`n", $encoding)
        [IO.File]::WriteAllText((Join-Path $root 'fixture-releases.json'), $(if ($name -eq 'no-release') { '[]' } else { $releases }))
        $output = & $bash -c 'p=$1; shift; if command -v cygpath >/dev/null 2>&1; then p=$(cygpath -u "$p"); fi; exec sh "$p" "$@"' `
            '_' $harness (Join-Path $bootstrapRoot 'shared/bootstrap_powershell.sh') $root $name $platform $architecture $requested 2>&1
        $exitCode = $LASTEXITCODE
        $text = $output -join "`n"
        $calls = @(Get-Content -LiteralPath (Join-Path $root 'calls'))
        if ($name -eq 'execution-fails') {
            Assert-Bootstrap ($exitCode -eq 42) 'Shell must preserve a failing native PowerShell exit code even if it prints the expected version.'
        }
        elseif ($name -in 'exhausted', 'bad-hash', 'missing-hash', 'no-release') {
            $expected = switch ($name) {
                exhausted { 'Download failed after 5 attempts' }
                bad-hash { 'SHA256 checksum mismatch' }
                missing-hash { 'Expected exactly one SHA256 checksum' }
                no-release { 'No stable release' }
            }
            Assert-Bootstrap ($exitCode -ne 0 -and $text.Contains($expected)) "Shell $name must fail for the intended reason: $text"
            Assert-Bootstrap ($calls -notcontains 'extract') 'Shell must verify the archive before extraction.'
        }
        else {
            Assert-Bootstrap ($exitCode -eq 0) "Shell $name failed: $text"
            Assert-Bootstrap ($calls -contains "https://github.com/PowerShell/PowerShell/releases/download/v$version/$asset") 'Shell selects the correct standalone archive and patch.'
            Assert-Bootstrap ($text.Contains("Validated PowerShell $version")) 'Shell must start and verify the installed PowerShell.'
            $expectedRequests = if ($name -eq 'exact') { 2 } elseif ($name -eq 'retry') { 5 } else { 3 }
            Assert-Bootstrap (@($calls | Where-Object { $_ -like 'https:*' }).Count -eq $expectedRequests) 'Shell makes only the expected release and archive requests.'
        }
        if ($name -in 'retry', 'exhausted') {
            $attempts = if ($name -eq 'retry') { 3 } else { 5 }
            Assert-Bootstrap (@($calls | Where-Object { $_ -like '*.tar.gz' }).Count -eq $attempts) 'Shell retries TLS/stream transport errors with a bounded attempt count.'
            Assert-Bootstrap (@($calls | Where-Object { $_ -eq 'sleep' }).Count -eq ($attempts - 1)) 'Shell waits only between retry attempts.'
        }
        Write-Host "PASS: standalone shell bootstrap $name"
    }
    Write-Host 'All 21 standalone PowerShell bootstrap fixture scenarios passed.'
}
finally {
    Remove-Item -LiteralPath $scratch -Recurse -Force
}
