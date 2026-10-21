const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const { createHash } = require('node:crypto');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const initialConfig = {
  'agent.enabled': false,
  'agent.sessionStyle': 'interactive',
  'agent.trace.path': 'original-traces',
  unrelated: { enabled: true, retries: 3, value: null },
};
const initialConfigurationBytes = Buffer.from(
  `\uFEFF${JSON.stringify(initialConfig, null, '\t').replace(/\n/g, '\r\n')}\r\n`,
);

const fakeAzclips = String.raw`
$ErrorActionPreference = 'Stop'
$root = $env:AZCLIPS_EVAL_TEST_ROOT
$scenario = Get-Content (Join-Path $root 'scenario.json') -Raw | ConvertFrom-Json
Add-Content (Join-Path $root 'agent-mode-guard.txt') "$($args -join ' ')|$env:AZCLIPS_IN_AGENT_MODE"
if ($args[0] -eq 'agent' -and $args[1] -eq 'tool' -and $args[2] -eq 'env') {
    if ($scenario.environmentWarning) { Write-Error 'Environment diagnostic on stderr' -ErrorAction Continue }
    $signedIn = $scenario.signedOut -ne $true
    $subscription = if ($scenario.missingSubscription) { '' } else { '11111111-1111-4111-8111-111111111111' }
    $version = if ($scenario.missingVersion) { '' } else { '0.1.3+0123456789abcdef0123456789abcdef01234567' }
    @{ signedIn = $signedIn; subscriptionId = $subscription; azclipsVersion = $version } | ConvertTo-Json -Compress
    $global:LASTEXITCODE = 0
    return
}
if ($args[0] -eq 'agent' -and $args[1] -eq 'tool' -and $args[2] -eq 'resource') {
    $resourceCalls = Join-Path $root 'resource-calls.txt'
    if (Test-Path -LiteralPath (Join-Path $root 'configuration-calls.txt')) { Add-Content $resourceCalls 'after-configuration' }
    Add-Content $resourceCalls ($args -join ' ')
    if ($args[3] -eq 'refresh') {
        if ($scenario.refreshFails) {
            'ARM refresh failed'
            $global:LASTEXITCODE = 5
            return
        }
        '→ resources (ARG, paged)'
        $status = if ($scenario.refreshStatus) { $scenario.refreshStatus } else { 'ok' }
        @{ status = $status; tenantId = 'tenant'; elapsedSeconds = 1.5 } | ConvertTo-Json -Compress
        '{' + [Environment]::NewLine + '  "recoveryWarning": "Configuration agent.enabled is obsolete."' + [Environment]::NewLine + '}'
        $global:LASTEXITCODE = 0
        return
    }
    if ($args[3] -eq 'find') {
        $found = if ($scenario.fixtureMissing) { @() } else {
            @(@{ name = $args[5]; subscriptionId = '11111111-1111-4111-8111-111111111111' })
        }
        @{
            query = @{ namePattern = $args[5] }
            cache = @{ resources = 42; loadedAtUtc = '2026-10-13T00:00:00Z' }
            matchCount = $found.Count
            matches = $found
        } | ConvertTo-Json -Compress -Depth 5
        $global:LASTEXITCODE = 0
        return
    }
    throw "Unexpected fake resource command: $($args -join ' ')"
}
$statePath = Join-Path $env:AZCLIPS_HOME 'config.json'
$json = if (Test-Path -LiteralPath $statePath) { Get-Content -LiteralPath $statePath -Raw }
$state = if ([string]::IsNullOrWhiteSpace($json)) { @{} } else { $json | ConvertFrom-Json -AsHashtable }
$operation = $args[1]
$key = $args[3]
if ($operation -eq 'info') {
    Add-Content (Join-Path $root 'setting-reads.txt') 'config info --keys-only'
    if ($scenario.noKeyListing) {
        'Unknown command: config info'
        $global:LASTEXITCODE = 1
        return
    }
    $listed = if ($scenario.listedKeys) { @($scenario.listedKeys) } else {
        @('agent.provider', 'agent.builtin.scope', 'agent.builtin.modelId', 'agent.recovery', 'agent.recovery.style', 'agent.trace.enabled', 'agent.trace.path')
    }
    ConvertTo-Json -InputObject $listed
    if ($scenario.keyListingWarning) {
        '{' + [Environment]::NewLine + '  "recoveryWarning": "Configuration agent.enabled is obsolete."' + [Environment]::NewLine + '}'
    }
    $global:LASTEXITCODE = 0
    return
}
if ($operation -eq 'get') {
    Add-Content (Join-Path $root 'setting-reads.txt') $key
    if (@($scenario.unsupportedSettings) -contains $key) {
        "Unknown configuration key: $key"
        $global:LASTEXITCODE = 1
        return
    }
    $value, $source = if ($scenario.settingValues -and $null -ne $scenario.settingValues.$key) { $scenario.settingValues.$key, 'global config' }
    elseif (@($scenario.nullSettings) -contains $key) { $null, 'global config' }
    elseif ($state.ContainsKey($key)) { $state[$key], 'global config' } else { "default-$key", 'default' }
    if ($scenario.jsonConfigOutput) {
        ConvertTo-Json -InputObject ([ordered]@{ key = $key; value = $value; source = $source })
        if ($scenario.keyListingWarning) {
            '{' + [Environment]::NewLine + '  "recoveryWarning": "Configuration agent.enabled is obsolete."' + [Environment]::NewLine + '}'
        }
    }
    else {
        # Same rendering as the older ConfigCommandBase.FormatValue.
        $shown = if ($null -eq $value) { 'null' } elseif ($value -is [bool]) { $value.ToString().ToLowerInvariant() } else { $value }
        "$key = $shown [$source]"
    }
    $global:LASTEXITCODE = 0
    return
}
Add-Content (Join-Path $root 'configuration-calls.txt') ($args -join ' ')
if ($operation -eq 'set') {
    if ($scenario.failSetKey -eq $key -and -not (Test-Path (Join-Path $root 'set-failed'))) {
        New-Item (Join-Path $root 'set-failed') -ItemType File | Out-Null
        'Configuration set rejected'
        $global:LASTEXITCODE = 7
        return
    }
    $state[$key] = switch -CaseSensitive ([string]$args[5]) {
        'true' { $true }
        'false' { $false }
        default { [string]$args[5] }
    }
}
elseif ($operation -eq 'delete') {
    [void]$state.Remove($key)
}
else {
    throw "Unexpected fake command: $($args -join ' ')"
}
$state | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $statePath
$global:LASTEXITCODE = 0
`;

const driver = String.raw`
$ErrorActionPreference = 'Stop'
$root = $env:AZCLIPS_EVAL_TEST_ROOT
$scenario = Get-Content (Join-Path $root 'scenario.json') -Raw | ConvertFrom-Json
$env:AZCLIPS_EXE = 'original-executable'
$env:PROMPTFOO_DISABLE_TELEMETRY = 'false'
$env:PROMPTFOO_DISABLE_UPDATE = 'false'
$env:AZCLIPS_EVALUATION_SIGNED_IN = 'original-signed-in'
$env:AZCLIPS_EVALUATION_SUBSCRIPTION_ID = 'original-subscription'
$env:AZCLIPS_EVALUATION_RECOVERY_MODE = 'original-recovery-mode'
$env:AZCLIPS_EVALUATION_TARGET = 'original-target'
$env:AZCLIPS_EVALUATION_RESOURCE_CACHE = 'original-resource-cache'
$env:AZCLIPS_IN_AGENT_MODE = $scenario.hostAgentMode

function global:npx {
    if ($args[0] -ne '--no-install') { throw 'Expected package-local Promptfoo without installation' }
    $firstIndex = [array]::IndexOf([object[]]$args, '--filter-first-n')
    if ($firstIndex -ge 0) {
        $warmupRepeat = [array]::IndexOf([object[]]$args, '--repeat')
        if ($warmupRepeat -lt 0 -or [int]$args[$warmupRepeat + 1] -ne 1) { throw 'The warm-up must run each selected case once' }
        $warmupOutput = $args[[array]::IndexOf([object[]]$args, '--output') + 1]
        $warmupConfiguration = Get-Content -LiteralPath (Join-Path $env:AZCLIPS_HOME 'config.json') -Raw | ConvertFrom-Json -AsHashtable
        $warmupTraces = $warmupConfiguration['agent.trace.path']
        New-Item -ItemType Directory -Path $warmupTraces -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $warmupTraces 'warmup-trace.jsonl') -Value '{}'
        Add-Content (Join-Path $root 'warmup-calls.txt') "warmup|$($args[$firstIndex + 1])|$warmupOutput|$env:AZCLIPS_EVALUATION_WARMUP_OBSERVATIONS"
        if (-not $scenario.failWarmup) {
            '{"results":{"results":[{"success":true}]}}' | Set-Content -LiteralPath $warmupOutput -Encoding utf8
        }
        $global:LASTEXITCODE = if ($null -ne $scenario.warmupExitCode) { [int]$scenario.warmupExitCode } else { 0 }
        return
    }
    $repeatIndex = [array]::IndexOf([object[]]$args, '--repeat')
    $expectedRepeat = if ($null -ne $scenario.repeat) { [int]$scenario.repeat } else { 1 }
    if ($repeatIndex -lt 0 -or [int]$args[$repeatIndex + 1] -ne $expectedRepeat) {
        throw 'Expected the requested independent repeat count'
    }
    if ($env:PROMPTFOO_DISABLE_TELEMETRY -ne 'true' -or $env:PROMPTFOO_DISABLE_UPDATE -ne 'true') {
        throw 'Promptfoo telemetry and update checks must be disabled during evaluation'
    }
    if ($env:AZCLIPS_EVALUATION_SIGNED_IN -ne 'true' -or
        $env:AZCLIPS_EVALUATION_SUBSCRIPTION_ID -ne '11111111-1111-4111-8111-111111111111') {
        throw 'Expected the actual active-profile context'
    }
    $outputIndex = [array]::IndexOf([object[]]$args, '--output') + 1
    $output = $args[$outputIndex]
    Add-Content (Join-Path $root 'calls.txt') "evaluate|$output"
    $configPath = Join-Path $env:AZCLIPS_HOME 'config.json'
    $configuration = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable
    @{
        target = $env:AZCLIPS_EVALUATION_TARGET
        resourceCache = $env:AZCLIPS_EVALUATION_RESOURCE_CACHE
        agentSettings = $env:AZCLIPS_EVALUATION_AGENT_SETTINGS
        warmupObservations = $env:AZCLIPS_EVALUATION_WARMUP_OBSERVATIONS
        agentModeGuard = $env:AZCLIPS_IN_AGENT_MODE
        tracesBeforeEvaluation = @(Get-ChildItem -LiteralPath $configuration['agent.trace.path'] -Filter '*.jsonl' -File -ErrorAction SilentlyContinue).Count
    } | ConvertTo-Json | Set-Content (Join-Path $root 'run-facts.json')
    $expectedMode = if ($scenario.recoveryMode) { $scenario.recoveryMode } else { 'automatic' }
    if ($env:AZCLIPS_EVALUATION_RECOVERY_MODE -ne $expectedMode) { throw 'Wrong provider recovery mode' }
    if ($expectedMode -eq 'manual') {
        if ($configuration['agent.recovery'] -ne 'manual') { throw 'Manual recovery must be enabled' }
    }
    elseif ($configuration['agent.enabled'] -isnot [bool] -or $configuration['agent.enabled'] -ne $true -or
        $configuration['agent.sessionStyle'] -ne 'oneshot') {
        throw 'Automatic recovery must use the legacy configuration contract'
    }
    if ($configuration['agent.trace.enabled'] -isnot [bool] -or $configuration['agent.trace.enabled'] -ne $true -or
        $configuration['agent.trace.path'] -ne [IO.Path]::GetFullPath((Join-Path $root 'traces'))) {
        throw "Expected temporary Agent configuration during evaluation: $($configuration | ConvertTo-Json -Depth 10 -Compress)"
    }
    $backups = @(Get-ChildItem -LiteralPath $env:AZCLIPS_HOME -Filter 'config.json.evaluation-*.bak' -File)
    if ($backups.Count -eq 1) {
        Copy-Item -LiteralPath $backups[0].FullName -Destination (Join-Path $root 'backup-during-evaluation.bin')
    }
    if ($scenario.failRestore) {
        Remove-Item -LiteralPath $configPath
        New-Item -Path $configPath -ItemType Directory | Out-Null
    }
    if ($scenario.evaluationThrows) { throw 'Evaluation invocation failed' }
    if ($scenario.produceArtifact) {
        $json = if ($scenario.invalidArtifact) { '{invalid json' }
            elseif ($scenario.emptyArtifact) { '{"results":{"results":[]}}' }
            elseif ($scenario.judgeErrorArtifact) {
                '{"results":{"results":[{"success":true,"testIdx":0,"testCase":{"vars":{"case_id":"profile-show-active"}},"gradingResult":{"componentResults":[{"pass":true}]}},{"success":false,"failureReason":1,"testIdx":1,"testCase":{"vars":{"case_id":"vm-create-long-flags"}},"gradingResult":{"componentResults":[{"pass":true},{"pass":false,"metadata":{"graderError":true}}]}}]}}'
            }
            else { '{"results":{"results":[{"success":true}]}}' }
        $json | Set-Content -LiteralPath $output -Encoding utf8
    }
    $global:LASTEXITCODE = [int]$scenario.evaluationExitCode
}

function global:node {
    if ($args[0] -like '*judge-azure.js' -and $args[1] -eq '--check-config') {
        $global:LASTEXITCODE = if ($scenario.missingJudgeConfiguration) { 1 } else { 0 }
        return
    }
    Add-Content (Join-Path $root 'calls.txt') 'upload'
    '{"dashboardUrl":"https://evaluation.example/run"}'
    $global:LASTEXITCODE = 0
}

$parameters = @{
    AzclipsExe = (Join-Path $root 'package\az.ps1')
    TracePath = (Join-Path $root 'traces')
}
if (-not $scenario.defaultOutput) { $parameters.OutputPath = Join-Path $root 'result.json' }
if ($scenario.upload) { $parameters.Upload = $true }
if ($scenario.expectedPackageSha256) { $parameters.ExpectedPackageSha256 = $scenario.expectedPackageSha256 }
if ($scenario.recoveryMode) { $parameters.RecoveryMode = $scenario.recoveryMode }
if ($scenario.resourceCache) { $parameters.ResourceCache = $scenario.resourceCache }
if ($null -ne $scenario.warmupObservations) { $parameters.WarmupObservations = $scenario.warmupObservations }
if ($null -ne $scenario.repeat) { $parameters.Repeat = $scenario.repeat }
$exitCode = 0
$failure = $null
try {
    for ($i = 0; $i -lt $scenario.runs; $i++) {
        & $env:AZCLIPS_EVAL_TEST_WRAPPER @parameters
        $exitCode = $LASTEXITCODE
    }
}
catch {
    $failure = $_.Exception.Message
    $exitCode = 1
}
@{
    error = $failure
    executable = $env:AZCLIPS_EXE
    exitCode = $exitCode
    telemetry = $env:PROMPTFOO_DISABLE_TELEMETRY
    update = $env:PROMPTFOO_DISABLE_UPDATE
    signedIn = $env:AZCLIPS_EVALUATION_SIGNED_IN
    subscriptionId = $env:AZCLIPS_EVALUATION_SUBSCRIPTION_ID
    recoveryMode = $env:AZCLIPS_EVALUATION_RECOVERY_MODE
    target = $env:AZCLIPS_EVALUATION_TARGET
    resourceCache = $env:AZCLIPS_EVALUATION_RESOURCE_CACHE
    agentModeGuard = $env:AZCLIPS_IN_AGENT_MODE
} |
    ConvertTo-Json | Set-Content (Join-Path $root 'summary.json')
exit $exitCode
`;

function runWrapper(context, options = {}) {
  const directory = fs.realpathSync.native(fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-wrapper-test-')));
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const scenario = {
    produceArtifact: true,
    evaluationExitCode: 0,
    runs: 1,
    ...options,
  };
  const home = path.join(directory, 'home');
  const configurationPath = path.join(home, 'config.json');
  fs.mkdirSync(home);
  if (scenario.failBackup) {
    fs.mkdirSync(configurationPath);
  } else if (!scenario.missingConfiguration) {
    fs.writeFileSync(configurationPath, scenario.emptyConfiguration ? Buffer.alloc(0) : initialConfigurationBytes);
  }
  fs.writeFileSync(path.join(directory, 'scenario.json'), JSON.stringify(scenario));
  fs.mkdirSync(path.join(directory, 'package'));
  fs.writeFileSync(path.join(directory, 'package', 'az.ps1'), fakeAzclips);
  fs.writeFileSync(path.join(directory, 'driver.ps1'), driver);
  if (scenario.existingArtifact) {
    fs.writeFileSync(path.join(directory, 'result.json'), 'previous-result');
  }
  if (scenario.existingWarmupArtifact) {
    fs.writeFileSync(path.join(directory, 'result.warmup.json'), 'previous-warmup');
  }
  const result = spawnSync('pwsh', ['-NoLogo', '-NoProfile', '-File', path.join(directory, 'driver.ps1')], {
    encoding: 'utf8',
    timeout: 20_000,
    env: {
      ...process.env,
      AZCLIPS_HOME: scenario.homeOverride ?? home,
      AZCLIPS_EVAL_TEST_ROOT: directory,
      AZCLIPS_EVAL_TEST_WRAPPER: path.join(__dirname, 'run-evaluation.ps1'),
      TEMP: directory,
      TMP: directory,
    },
  });
  if (result.error) {
    throw new Error('PowerShell 7 is required to validate the evaluation wrapper', { cause: result.error });
  }
  const summaryPath = path.join(directory, 'summary.json');
  assert.ok(fs.existsSync(summaryPath), `${result.stdout}\n${result.stderr}`);
  const callsPath = path.join(directory, 'calls.txt');
  const configurationCallsPath = path.join(directory, 'configuration-calls.txt');
  const configurationBytes = fs.existsSync(configurationPath) && fs.statSync(configurationPath).isFile()
    ? fs.readFileSync(configurationPath)
    : null;
  const configurationText = configurationBytes?.toString('utf8').replace(/^\uFEFF/, '').trim();
  const backupDuringEvaluationPath = path.join(directory, 'backup-during-evaluation.bin');
  const resourceCallsPath = path.join(directory, 'resource-calls.txt');
  const runFactsPath = path.join(directory, 'run-facts.json');
  const readLines = (file) => (fs.existsSync(file) ? fs.readFileSync(file, 'utf8').trim().split(/\r?\n/) : []);
  return {
    ...result,
    directory,
    configurationPath,
    summary: JSON.parse(fs.readFileSync(summaryPath, 'utf8').replace(/^\uFEFF/, '')),
    configuration: configurationText ? JSON.parse(configurationText) : null,
    configurationBytes,
    warmupCalls: readLines(path.join(directory, 'warmup-calls.txt')),
    settingReads: readLines(path.join(directory, 'setting-reads.txt')),
    agentModeGuards: readLines(path.join(directory, 'agent-mode-guard.txt')),
    resourceCalls: fs.existsSync(resourceCallsPath)
      ? fs.readFileSync(resourceCallsPath, 'utf8').trim().split(/\r?\n/)
      : [],
    runFacts: fs.existsSync(runFactsPath)
      ? JSON.parse(fs.readFileSync(runFactsPath, 'utf8').replace(/^\uFEFF/, ''))
      : null,
    backups: fs.readdirSync(home)
      .filter((name) => /^config\.json\.evaluation-[a-f0-9]{32}\.bak$/.test(name))
      .map((name) => path.join(home, name)),
    backupDuringEvaluation: fs.existsSync(backupDuringEvaluationPath)
      ? fs.readFileSync(backupDuringEvaluationPath)
      : null,
    configurationCalls: fs.existsSync(configurationCallsPath)
      ? fs.readFileSync(configurationCallsPath, 'utf8').trim().split(/\r?\n/)
      : [],
    calls: fs.existsSync(callsPath)
      ? fs.readFileSync(callsPath, 'utf8').trim().split(/\r?\n/)
      : [],
  };
}

test('wrapper restores original configuration bytes, JSON types, and environment after evaluation', (context) => {
  const result = runWrapper(context);
  assert.equal(result.status, 0, result.summary.error);
  assert.equal(result.summary.executable, 'original-executable');
  assert.equal(result.summary.telemetry, 'false');
  assert.equal(result.summary.update, 'false');
  assert.equal(result.summary.signedIn, 'original-signed-in');
  assert.equal(result.summary.subscriptionId, 'original-subscription');
  assert.deepEqual(result.configuration, initialConfig);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
  assert.deepEqual(result.backupDuringEvaluation, initialConfigurationBytes);
  assert.deepEqual(result.backups, []);
  assert.equal(result.configurationCalls.length, 4);
  assert.ok(result.configurationCalls.every((call) => call.startsWith('config set ')));
  assert.equal(result.calls.length, 1);
  assert.ok(fs.existsSync(path.join(result.directory, 'result.json')));
  assert.equal(result.summary.target, 'original-target');
  assert.equal(result.summary.resourceCache, 'original-resource-cache');
});

test('wrapper refreshes the ARM cache and identifies the target package before changing configuration', (context) => {
  const result = runWrapper(context);
  assert.equal(result.status, 0, result.summary.error);
  assert.deepEqual(result.resourceCalls, [
    'agent tool resource refresh',
    'agent tool resource find --name azclips-test-rg --kind resourceGroup --subscription-id 11111111-1111-4111-8111-111111111111',
  ]);
  const target = JSON.parse(result.runFacts.target);
  assert.equal(target.version, '0.1.3+0123456789abcdef0123456789abcdef01234567');
  assert.match(target.packageSha256, /^[0-9a-f]{64}$/);
  assert.ok(target.packageFiles > 0);
  const cache = JSON.parse(result.runFacts.resourceCache);
  assert.equal(cache.mode, 'refresh');
  assert.equal(cache.resources, 42);
  assert.equal(cache.fixtureGroup, 'azclips-test-rg');
  assert.equal(cache.fixtureGroupFound, true);
  assert.ok(cache.refreshMs >= 0);
  assert.ok(!Number.isNaN(Date.parse(cache.refreshStartedAtUtc)));
});

function fakePackageFingerprint() {
  const fileHash = createHash('sha256').update(Buffer.from(fakeAzclips, 'utf8')).digest('hex');
  return createHash('sha256').update(`az.ps1\t${fileHash}`, 'utf8').digest('hex');
}

test('wrapper accepts the expected package fingerprint in either letter case', (context) => {
  const expected = fakePackageFingerprint();
  const result = runWrapper(context, { expectedPackageSha256: expected.toUpperCase() });
  assert.equal(result.status, 0, result.summary.error);
  const target = JSON.parse(result.runFacts.target);
  assert.equal(target.packageSha256, expected);
  assert.equal(target.packageFiles, 1);
});

test('wrapper stops before cache refresh, configuration changes or model calls when the package differs', (context) => {
  const result = runWrapper(context, { expectedPackageSha256: 'f'.repeat(64), upload: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, new RegExp(`fingerprint ${fakePackageFingerprint()} does not match`));
  assert.deepEqual(result.resourceCalls, []);
  assert.deepEqual(result.configurationCalls, []);
  assert.deepEqual(result.calls, []);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
});

test('wrapper stops before configuration changes or model calls when the cache cannot be refreshed', async (context) => {
  for (const [name, options, message] of [
    ['refresh process fails', { refreshFails: true }, /azclips exited with 5 while running: agent tool resource refresh/],
    ['refresh status is not ok', { refreshStatus: 'partial' }, /did not report status ok/],
    ['fixture group is absent', { fixtureMissing: true }, /does not contain fixture group 'azclips-test-rg'/],
  ]) {
    await context.test(name, (context) => {
      const result = runWrapper(context, { ...options, upload: true });
      assert.equal(result.status, 1);
      assert.match(result.summary.error, message);
      assert.deepEqual(result.configurationCalls, []);
      assert.deepEqual(result.calls, []);
      assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
      assert.deepEqual(result.backups, []);
      assert.ok(!result.resourceCalls.includes('after-configuration'));
    });
  }
});

test('existing cache mode records the choice without refreshing or querying the cache', (context) => {
  const result = runWrapper(context, { resourceCache: 'existing' });
  assert.equal(result.status, 0, result.summary.error);
  assert.deepEqual(result.resourceCalls, []);
  assert.deepEqual(JSON.parse(result.runFacts.resourceCache), { mode: 'existing' });
});

test('wrapper refuses an unidentified target before any side effect', (context) => {
  const result = runWrapper(context, { missingVersion: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /did not report azclipsVersion/);
  assert.deepEqual(result.resourceCalls, []);
  assert.deepEqual(result.configurationCalls, []);
  assert.deepEqual(result.calls, []);
});

test('wrapper refuses a relative AZCLIPS_HOME before any side effect', (context) => {
  const result = runWrapper(context, { homeOverride: 'relative-home' });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /AZCLIPS_HOME must be an absolute path, not 'relative-home'/);
  assert.deepEqual(result.resourceCalls, []);
  assert.deepEqual(result.configurationCalls, []);
  assert.deepEqual(result.calls, []);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
  assert.deepEqual(result.backups, []);
});

test('wrapper discards one warm-up observation and moves its traces before measuring', (context) => {
  const result = runWrapper(context, { repeat: 3 });
  assert.equal(result.status, 0, result.summary.error);
  const warmupOutput = path.join(result.directory, 'result.warmup.json');
  assert.deepEqual(result.warmupCalls, [`warmup|1|${warmupOutput}|1`]);
  assert.ok(fs.existsSync(warmupOutput));
  const warmupFolders = fs.readdirSync(path.join(result.directory, 'traces')).filter((name) => /^warmup-[0-9a-f]{32}$/.test(name));
  assert.equal(warmupFolders.length, 1);
  assert.ok(fs.existsSync(path.join(result.directory, 'traces', warmupFolders[0], 'warmup-trace.jsonl')));
  assert.equal(result.runFacts.tracesBeforeEvaluation, 0);
  assert.equal(result.runFacts.warmupObservations, '1');
  assert.deepEqual(result.calls, [`evaluate|${path.join(result.directory, 'result.json')}`]);
});

test('warm-up can be disabled explicitly and the choice is recorded', (context) => {
  const result = runWrapper(context, { warmupObservations: 0 });
  assert.equal(result.status, 0, result.summary.error);
  assert.deepEqual(result.warmupCalls, []);
  assert.equal(result.runFacts.warmupObservations, '0');
});

test('a failed warm-up stops before the measured evaluation and restores configuration', async (context) => {
  for (const [name, options] of [
    ['no warm-up artifact', { failWarmup: true }],
    ['warm-up process error', { warmupExitCode: 2 }],
  ]) {
    await context.test(name, (context) => {
      const result = runWrapper(context, { ...options, upload: true });
      assert.equal(result.status, 1);
      assert.match(result.summary.error, /discarded warm-up evaluation failed/);
      assert.deepEqual(result.calls, []);
      assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
      assert.deepEqual(result.backups, []);
      assert.equal(result.summary.target, 'original-target');
    });
  }
});

test('wrapper refuses an existing warm-up artifact before any side effect', (context) => {
  const result = runWrapper(context, { existingWarmupArtifact: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Warm-up output already exists/);
  assert.deepEqual(result.resourceCalls, []);
  assert.deepEqual(result.configurationCalls, []);
  assert.deepEqual(result.calls, []);
});

test('wrapper records the effective model provider settings and skips keys the target does not define', (context) => {
  const result = runWrapper(context, { recoveryMode: 'manual', unsupportedSettings: ['agent.recovery.style'] });
  assert.equal(result.status, 0, result.summary.error);
  const settings = JSON.parse(result.runFacts.agentSettings);
  assert.deepEqual(settings['agent.builtin.modelId'], { value: 'default-agent.builtin.modelId', source: 'default' });
  assert.deepEqual(settings['agent.recovery'], { value: 'manual', source: 'global config' });
  assert.equal(settings['agent.recovery.style'], undefined);
  assert.equal(settings['agent.trace.enabled'], undefined);
  assert.ok(result.settingReads.includes('agent.builtin.scope'));
  assert.ok(result.configurationCalls.every((call) => call.startsWith('config set ')));
});

test('wrapper removes credentials, queries and fragments from recorded setting URLs', (context) => {
  const result = runWrapper(context, {
    settingValues: {
      'agent.builtin.scope': 'https://user:hidden@example.openai.azure.com/openai/?api-key=hidden#hidden',
      'agent.provider': 'builtin',
    },
  });
  assert.equal(result.status, 0, result.summary.error);
  const settings = JSON.parse(result.runFacts.agentSettings);
  assert.deepEqual(settings['agent.builtin.scope'], {
    value: 'https://example.openai.azure.com/openai/',
    source: 'global config',
    redacted: true,
  });
  assert.deepEqual(settings['agent.provider'], { value: 'builtin', source: 'global config' });
  assert.doesNotMatch(result.runFacts.agentSettings, /hidden/);
});

test('wrapper records the same settings when the target prints config get output as JSON', (context) => {
  const scenario = {
    keyListingWarning: true,
    recoveryMode: 'manual',
    listedKeys: [
      'agent.provider', 'agent.recovery', 'agent.recovery.style', 'agent.builtin.modelId', 'agent.builtin.scope',
      'agent.interactive.verbose', 'core.http.maxRetries',
    ],
    settingValues: {
      'agent.builtin.scope': 'https://example.openai.azure.com/openai/?api-key=hidden',
      'agent.builtin.modelId': 'deployment-{run}',
      'agent.interactive.verbose': false,
      'core.http.maxRetries': 9,
    },
    nullSettings: ['agent.recovery.style'],
  };
  const result = runWrapper(context, { ...scenario, jsonConfigOutput: true });
  assert.equal(result.status, 0, result.summary.error);
  const settings = JSON.parse(result.runFacts.agentSettings);
  assert.deepEqual(settings['agent.provider'], { value: 'default-agent.provider', source: 'default' });
  assert.deepEqual(settings['agent.recovery'], { value: 'manual', source: 'global config' });
  assert.deepEqual(settings['agent.recovery.style'], { value: 'null', source: 'global config' });
  assert.deepEqual(settings['agent.builtin.modelId'], { value: 'deployment-{run}', source: 'global config' });
  assert.deepEqual(settings['agent.interactive.verbose'], { value: 'false', source: 'global config' });
  assert.deepEqual(settings['core.http.maxRetries'], { value: '9', source: 'global config' });
  assert.deepEqual(settings['agent.builtin.scope'], {
    value: 'https://example.openai.azure.com/openai/',
    source: 'global config',
    redacted: true,
  });
  assert.doesNotMatch(result.runFacts.agentSettings, /hidden|recoveryWarning/);

  const text = runWrapper(context, scenario);
  assert.equal(text.status, 0, text.summary.error);
  assert.deepEqual(settings, JSON.parse(text.runFacts.agentSettings));
});

test('wrapper records every agent, HTTP-retry and telemetry setting the target lists', (context) => {
  const result = runWrapper(context, {
    keyListingWarning: true,
    listedKeys: [
      'agent.provider', 'agent.reasoningEffort', 'agent.trace.enabled', 'agent.builtin.apiKey',
      'core.http.maxRetries', 'telemetry.enabled', 'clients.cli.output.format',
    ],
  });
  assert.equal(result.status, 0, result.summary.error);
  assert.deepEqual(Object.keys(JSON.parse(result.runFacts.agentSettings)).sort(), [
    'agent.provider', 'agent.reasoningEffort', 'core.http.maxRetries', 'telemetry.enabled',
  ]);
});

test('wrapper falls back to the default setting list when the target cannot list its keys', (context) => {
  const result = runWrapper(context, { noKeyListing: true });
  assert.equal(result.status, 0, result.summary.error);
  assert.deepEqual(Object.keys(JSON.parse(result.runFacts.agentSettings)).sort(), [
    'agent.builtin.apiVersion', 'agent.builtin.modelId', 'agent.builtin.scope',
    'agent.provider', 'agent.recovery', 'agent.recovery.style',
  ]);
});

test('wrapper setup commands cannot start AI recovery and leave the guard unset for Promptfoo', (context) => {
  const result = runWrapper(context, { unsupportedSettings: ['agent.recovery.style'] });
  assert.equal(result.status, 0, result.summary.error);
  const commands = result.agentModeGuards.map((line) => line.slice(0, line.lastIndexOf('|')));
  for (const expected of ['agent tool env', 'agent tool resource refresh', 'config set', 'config info --keys-only', 'config get']) {
    assert.ok(commands.some((command) => command.startsWith(expected)), `${expected} was not guarded`);
  }
  assert.ok(result.agentModeGuards.every((line) => line.endsWith('|1')), result.agentModeGuards.join('\n'));
  assert.equal(result.runFacts.agentModeGuard, null);
  assert.equal(result.summary.agentModeGuard, null);
});

test('wrapper restores an inherited agent-mode value after each setup command', (context) => {
  const result = runWrapper(context, { hostAgentMode: 'host-value' });
  assert.equal(result.status, 0, result.summary.error);
  assert.ok(result.agentModeGuards.length > 0);
  assert.ok(result.agentModeGuards.every((line) => line.endsWith('|1')), result.agentModeGuards.join('\n'));
  assert.equal(result.runFacts.agentModeGuard, 'host-value');
  assert.equal(result.summary.agentModeGuard, 'host-value');
});

test('wrapper uses a distinct default artifact path for each run', (context) => {
  const result = runWrapper(context, { defaultOutput: true, runs: 2 });
  assert.equal(result.status, 0, result.summary.error);
  const paths = result.calls.map((call) => call.slice('evaluate|'.length));
  assert.equal(new Set(paths).size, 2);
  assert.ok(paths.every((file) => fs.existsSync(file)));
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
  assert.deepEqual(result.backups, []);
});

test('wrapper forwards three independent repetitions and still restores configuration', (context) => {
  const result = runWrapper(context, { repeat: 3 });
  assert.equal(result.status, 0, result.summary.error);
  assert.equal(result.calls.length, 1);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
});

test('wrapper rejects an invalid repetition count before any side effect', (context) => {
  const result = runWrapper(context, { repeat: 0 });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Repeat/);
  assert.deepEqual(result.calls, []);
  assert.deepEqual(result.configurationCalls, []);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
});

test('wrapper stops before configuration changes or model calls when the semantic judge is not configured', (context) => {
  const result = runWrapper(context, { missingJudgeConfiguration: true, upload: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Semantic judge configuration is incomplete/);
  assert.deepEqual(result.configurationCalls, []);
  assert.deepEqual(result.calls, []);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
  assert.deepEqual(result.backups, []);
});

test('manual wrapper uses the new recovery policy and restores configuration and mode', (context) => {
  const result = runWrapper(context, { recoveryMode: 'manual', environmentWarning: true });
  assert.equal(result.status, 0, result.summary.error);
  assert.ok(result.configurationCalls.includes('config set --key agent.recovery --value manual'));
  assert.ok(!result.configurationCalls.some((call) => /--key agent\.(enabled|sessionStyle)\b/.test(call)));
  assert.equal(result.configurationCalls.length, 3);
  assert.equal(result.summary.recoveryMode, 'original-recovery-mode');
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
  assert.deepEqual(result.backups, []);
  assert.match(result.stderr, /Environment diagnostic on stderr/);
});

test('wrapper refuses to overwrite or upload a previous artifact', (context) => {
  const result = runWrapper(context, { existingArtifact: true, upload: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Output already exists/);
  assert.deepEqual(result.calls, []);
  assert.deepEqual(result.configuration, initialConfig);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
  assert.deepEqual(result.backups, []);
  assert.equal(fs.readFileSync(path.join(result.directory, 'result.json'), 'utf8'), 'previous-result');
});

test('wrapper fails without uploading when Promptfoo does not export an artifact', (context) => {
  const result = runWrapper(context, { produceArtifact: false, evaluationExitCode: 9, upload: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /did not produce an evaluation artifact \(exit code 9\)/);
  assert.equal(result.calls.includes('upload'), false);
  assert.deepEqual(result.configuration, initialConfig);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
  assert.deepEqual(result.backups, []);
  assert.equal(result.summary.executable, 'original-executable');
});

test('wrapper keeps but refuses to upload an artifact whose Judge calls failed', (context) => {
  const result = runWrapper(context, { judgeErrorArtifact: true, evaluationExitCode: 100, upload: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Judge infrastructure errors in 1 result\(s\): vm-create-long-flags#1/);
  assert.equal(result.calls.includes('upload'), false);
  assert.ok(fs.existsSync(path.join(result.directory, 'result.json')));
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
});

test('wrapper uploads fresh quality failures and preserves their nonzero exit code', (context) => {
  const result = runWrapper(context, { evaluationExitCode: 100, upload: true });
  assert.equal(result.status, 100, result.summary.error);
  assert.equal(result.calls.at(-1), 'upload');
  assert.deepEqual(result.configuration, initialConfig);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
  assert.deepEqual(result.backups, []);
});

for (const [option, message] of [
  ['invalidArtifact', /produced invalid JSON/],
  ['emptyArtifact', /contains no evaluation results/],
]) {
  test(`wrapper rejects ${option} before uploading`, (context) => {
    const result = runWrapper(context, { [option]: true, upload: true });
    assert.equal(result.status, 1);
    assert.match(result.summary.error, message);
    assert.equal(result.calls.includes('upload'), false);
    assert.deepEqual(result.configuration, initialConfig);
    assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
    assert.deepEqual(result.backups, []);
  });
}

test('wrapper restores prior settings after setup fails partway through', (context) => {
  const result = runWrapper(context, { failSetKey: 'agent.trace.enabled' });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Configuration set rejected/);
  assert.deepEqual(result.calls, []);
  assert.deepEqual(result.configuration, initialConfig);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
  assert.deepEqual(result.backups, []);
  assert.equal(result.summary.executable, 'original-executable');
});

test('wrapper restores original configuration after an evaluation exception', (context) => {
  const result = runWrapper(context, { evaluationThrows: true, upload: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Evaluation invocation failed/);
  assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
  assert.deepEqual(result.backups, []);
  assert.equal(result.summary.executable, 'original-executable');
  assert.equal(result.summary.telemetry, 'false');
  assert.equal(result.summary.update, 'false');
  assert.equal(result.summary.signedIn, 'original-signed-in');
  assert.equal(result.summary.subscriptionId, 'original-subscription');
  assert.equal(result.calls.includes('upload'), false);
});

for (const failSetKey of [undefined, 'agent.trace.enabled']) {
  test(`wrapper restores a missing configuration file after ${failSetKey ? 'setup failure' : 'evaluation'}`, (context) => {
    const result = runWrapper(context, { missingConfiguration: true, failSetKey });
    assert.equal(result.status, failSetKey ? 1 : 0, result.summary.error);
    if (failSetKey) { assert.match(result.summary.error, /Configuration set rejected/); }
    assert.equal(fs.existsSync(result.configurationPath), false);
    assert.deepEqual(result.backups, []);
    assert.equal(result.summary.executable, 'original-executable');
  });
}

test('wrapper restores an originally empty configuration file without rewriting it', (context) => {
  const result = runWrapper(context, { emptyConfiguration: true });
  assert.equal(result.status, 0, result.summary.error);
  assert.deepEqual(result.configurationBytes, Buffer.alloc(0));
  assert.deepEqual(result.backupDuringEvaluation, Buffer.alloc(0));
  assert.deepEqual(result.backups, []);
});

test('wrapper stops before changing configuration or evaluating if backup fails', (context) => {
  const result = runWrapper(context, { failBackup: true, upload: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Failed to back up Agent configuration/);
  assert.deepEqual(result.configurationCalls, []);
  assert.deepEqual(result.calls, []);
  assert.deepEqual(result.backups, []);
  assert.ok(fs.statSync(result.configurationPath).isDirectory());
  assert.equal(result.summary.executable, 'original-executable');
});

for (const evaluationThrows of [false, true]) {
  test(`wrapper retains a recoverable backup and blocks upload on restore failure${evaluationThrows ? ' after an evaluation exception' : ''}`, (context) => {
    const result = runWrapper(context, { failRestore: true, evaluationThrows, upload: true });
    assert.equal(result.status, 1);
    assert.match(result.summary.error, /Failed to restore Agent configuration/);
    if (evaluationThrows) { assert.match(result.summary.error, /Evaluation invocation failed/); }
    assert.equal(result.backups.length, 1);
    assert.ok(result.summary.error.includes(result.backups[0]), result.summary.error);
    assert.deepEqual(fs.readFileSync(result.backups[0]), initialConfigurationBytes);
    assert.equal(result.summary.executable, 'original-executable');
    assert.equal(result.summary.telemetry, 'false');
    assert.equal(result.summary.update, 'false');
    assert.equal(result.summary.signedIn, 'original-signed-in');
    assert.equal(result.summary.subscriptionId, 'original-subscription');
    assert.equal(result.calls.includes('upload'), false);

    fs.rmdirSync(result.configurationPath);
    fs.renameSync(result.backups[0], result.configurationPath);
    assert.deepEqual(fs.readFileSync(result.configurationPath), initialConfigurationBytes);
  });
}

for (const option of ['signedOut', 'missingSubscription']) {
  test(`wrapper rejects ${option} before changing configuration or calling a model`, (context) => {
    const result = runWrapper(context, { [option]: true });
    assert.equal(result.status, 1);
    assert.match(result.summary.error, /requires an Azure-signed-in active profile/);
    assert.deepEqual(result.configuration, initialConfig);
    assert.deepEqual(result.configurationBytes, initialConfigurationBytes);
    assert.deepEqual(result.backups, []);
    assert.deepEqual(result.calls, []);
  });
}
