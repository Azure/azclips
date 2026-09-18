const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const initialConfig = {
  'agent.enabled': 'false',
  'agent.sessionStyle': 'interactive',
  'agent.trace.path': 'original-traces',
};

const fakeAzclips = String.raw`
$ErrorActionPreference = 'Stop'
$root = $env:AZCLIPS_EVAL_TEST_ROOT
$scenario = Get-Content (Join-Path $root 'scenario.json') -Raw | ConvertFrom-Json
$statePath = Join-Path $root 'state.json'
$state = Get-Content $statePath -Raw | ConvertFrom-Json -AsHashtable
$operation = $args[1]
$key = $args[3]
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
    if ($scenario.failRestoreKey -eq $key -and $args[5] -eq 'false') {
        'Configuration restore rejected'
        $global:LASTEXITCODE = 9
        return
    }
    $state[$key] = [string]$args[5]
}
elseif ($operation -eq 'delete') {
    [void]$state.Remove($key)
}
else {
    throw "Unexpected fake command: $($args -join ' ')"
}
$state | ConvertTo-Json | Set-Content $statePath
$global:LASTEXITCODE = 0
`;

const driver = String.raw`
$ErrorActionPreference = 'Stop'
$root = $env:AZCLIPS_EVAL_TEST_ROOT
$scenario = Get-Content (Join-Path $root 'scenario.json') -Raw | ConvertFrom-Json
$env:AZCLIPS_EXE = 'original-executable'
$env:PROMPTFOO_DISABLE_TELEMETRY = 'false'
$env:PROMPTFOO_DISABLE_UPDATE = 'false'

function global:npx {
    if ($args[0] -ne '--no-install') { throw 'Expected package-local Promptfoo without installation' }
    if ($env:PROMPTFOO_DISABLE_TELEMETRY -ne 'true' -or $env:PROMPTFOO_DISABLE_UPDATE -ne 'true') {
        throw 'Promptfoo telemetry and update checks must be disabled during evaluation'
    }
    $outputIndex = [array]::IndexOf([object[]]$args, '--output') + 1
    $output = $args[$outputIndex]
    Add-Content (Join-Path $root 'calls.txt') "evaluate|$output"
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
} |
    ConvertTo-Json | Set-Content (Join-Path $root 'summary.json')
exit $exitCode
`;

function runWrapper(context, options = {}) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-wrapper-test-'));
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const scenario = {
    produceArtifact: true,
    evaluationExitCode: 0,
    runs: 1,
    ...options,
  };
  fs.writeFileSync(path.join(directory, 'scenario.json'), JSON.stringify(scenario));
  fs.writeFileSync(path.join(directory, 'state.json'), JSON.stringify(initialConfig));
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
  return {
    ...result,
    directory,
    summary: JSON.parse(fs.readFileSync(summaryPath, 'utf8').replace(/^\uFEFF/, '')),
    configuration: JSON.parse(fs.readFileSync(path.join(directory, 'state.json'), 'utf8').replace(/^\uFEFF/, '')),
    calls: fs.existsSync(callsPath)
      ? fs.readFileSync(callsPath, 'utf8').trim().split(/\r?\n/)
      : [],
  };
}

test('wrapper produces a fresh artifact and restores configuration and environment', (context) => {
  const result = runWrapper(context);
  assert.equal(result.status, 0, result.summary.error);
  assert.equal(result.summary.executable, 'original-executable');
  assert.equal(result.summary.telemetry, 'false');
  assert.equal(result.summary.update, 'false');
  assert.deepEqual(result.configuration, initialConfig);
  assert.equal(result.calls.length, 1);
  assert.ok(fs.existsSync(path.join(result.directory, 'result.json')));
});

test('wrapper uses a distinct default artifact path for each run', (context) => {
  const result = runWrapper(context, { defaultOutput: true, runs: 2 });
  assert.equal(result.status, 0, result.summary.error);
  const paths = result.calls.map((call) => call.slice('evaluate|'.length));
  assert.equal(new Set(paths).size, 2);
  assert.ok(paths.every((file) => fs.existsSync(file)));
});

test('wrapper refuses to overwrite or upload a previous artifact', (context) => {
  const result = runWrapper(context, { existingArtifact: true, upload: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Output already exists/);
  assert.deepEqual(result.calls, []);
  assert.deepEqual(result.configuration, initialConfig);
  assert.equal(fs.readFileSync(path.join(result.directory, 'result.json'), 'utf8'), 'previous-result');
});

test('wrapper fails without uploading when Promptfoo does not export an artifact', (context) => {
  const result = runWrapper(context, { produceArtifact: false, evaluationExitCode: 9, upload: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /did not produce an evaluation artifact \(exit code 9\)/);
  assert.equal(result.calls.includes('upload'), false);
  assert.deepEqual(result.configuration, initialConfig);
  assert.equal(result.summary.executable, 'original-executable');
});

test('wrapper uploads fresh quality failures and preserves their nonzero exit code', (context) => {
  const result = runWrapper(context, { evaluationExitCode: 100, upload: true });
  assert.equal(result.status, 100, result.summary.error);
  assert.equal(result.calls.at(-1), 'upload');
  assert.deepEqual(result.configuration, initialConfig);
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
  });
}

test('wrapper restores prior settings after setup fails partway through', (context) => {
  const result = runWrapper(context, { failSetKey: 'agent.trace.enabled' });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Configuration set rejected/);
  assert.deepEqual(result.calls, []);
  assert.deepEqual(result.configuration, initialConfig);
  assert.equal(result.summary.executable, 'original-executable');
});

test('wrapper attempts remaining restores and blocks upload after a restore failure', (context) => {
  const result = runWrapper(context, { failRestoreKey: 'agent.enabled', upload: true });
  assert.equal(result.status, 1);
  assert.match(result.summary.error, /Failed to restore Agent configuration: agent.enabled/);
  assert.equal(result.configuration['agent.sessionStyle'], initialConfig['agent.sessionStyle']);
  assert.equal(result.configuration['agent.trace.path'], initialConfig['agent.trace.path']);
  assert.equal(result.configuration['agent.trace.enabled'], undefined);
  assert.equal(result.summary.executable, 'original-executable');
  assert.equal(result.calls.includes('upload'), false);
});
