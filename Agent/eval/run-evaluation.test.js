const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
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
if ($args[0] -eq 'agent' -and $args[1] -eq 'tool' -and $args[2] -eq 'env') {
    if ($scenario.environmentWarning) { Write-Error 'Environment diagnostic on stderr' -ErrorAction Continue }
    $signedIn = $scenario.signedOut -ne $true
    $subscription = if ($scenario.missingSubscription) { '' } else { '11111111-1111-4111-8111-111111111111' }
    @{ signedIn = $signedIn; subscriptionId = $subscription } | ConvertTo-Json -Compress
    $global:LASTEXITCODE = 0
    return
}
$statePath = Join-Path $env:AZCLIPS_HOME 'config.json'
$json = if (Test-Path -LiteralPath $statePath) { Get-Content -LiteralPath $statePath -Raw }
$state = if ([string]::IsNullOrWhiteSpace($json)) { @{} } else { $json | ConvertFrom-Json -AsHashtable }
$operation = $args[1]
$key = $args[3]
Add-Content (Join-Path $root 'configuration-calls.txt') ($args -join ' ')
if ($operation -eq 'get') {
    $value = if ($state.ContainsKey($key)) { $state[$key] } else { 'false' }
    $source = if ($state.ContainsKey($key)) { 'config' } else { 'Default' }
    @{ Value = $value; Source = $source } | ConvertTo-Json -Compress
    $global:LASTEXITCODE = 0
    return
}
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

function global:npx {
    if ($args[0] -ne '--no-install') { throw 'Expected package-local Promptfoo without installation' }
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
            else { '{"results":{"results":[{"success":true}]}}' }
        $json | Set-Content -LiteralPath $output -Encoding utf8
    }
    $global:LASTEXITCODE = [int]$scenario.evaluationExitCode
}

function global:node {
    Add-Content (Join-Path $root 'calls.txt') 'upload'
    '{"dashboardUrl":"https://evaluation.example/run"}'
    $global:LASTEXITCODE = 0
}

$parameters = @{
    AzclipsExe = (Join-Path $root 'az.ps1')
    TracePath = (Join-Path $root 'traces')
}
if (-not $scenario.defaultOutput) { $parameters.OutputPath = Join-Path $root 'result.json' }
if ($scenario.upload) { $parameters.Upload = $true }
if ($scenario.recoveryMode) { $parameters.RecoveryMode = $scenario.recoveryMode }
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
  fs.writeFileSync(path.join(directory, 'az.ps1'), fakeAzclips);
  fs.writeFileSync(path.join(directory, 'driver.ps1'), driver);
  if (scenario.existingArtifact) {
    fs.writeFileSync(path.join(directory, 'result.json'), 'previous-result');
  }
  const result = spawnSync('pwsh', ['-NoLogo', '-NoProfile', '-File', path.join(directory, 'driver.ps1')], {
    encoding: 'utf8',
    timeout: 20_000,
    env: {
      ...process.env,
      AZCLIPS_HOME: home,
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
  return {
    ...result,
    directory,
    configurationPath,
    summary: JSON.parse(fs.readFileSync(summaryPath, 'utf8').replace(/^\uFEFF/, '')),
    configuration: configurationText ? JSON.parse(configurationText) : null,
    configurationBytes,
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
