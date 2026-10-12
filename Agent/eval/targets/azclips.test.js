const assert = require('node:assert/strict');
const childProcess = require('node:child_process');
const { createHash } = require('node:crypto');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const AzclipsAgentProvider = require('./azclips');
const { judgeInput } = require('../judge/recovery-quality');
const {
  buildEvaluationEnvironment,
  removedHostVariables,
  execFileUnattended,
  findTracePath,
  findFailureId,
  originalCliError,
  findRuntimePromptPath,
  readRuntimePrompt,
  readModels,
  readJsonEnvironment,
  readPlanActivity,
  readStructuredPlan,
  readTiming,
  readToolCalls,
  readTokenUsage,
  splitAgentOutput,
  stripNoise,
  terminateProcessTree,
  toolTraceAvailable,
  PROCESS_TREE_GRACE_MS,
} = AzclipsAgentProvider;

function renderSpinnerFrame(frame, verb) {
  const line = `  ${frame} ${verb}`;
  return `${line}\r${' '.repeat(line.length)}\r`;
}

function createProviderFixture(context) {
  const directory = fs.mkdtempSync(path.join(__dirname, 'azclips-provider-test-'));
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const exe = path.join(directory, 'az.exe');
  const promptPath = path.join(directory, 'prompts', 'oneshot.md');
  const runtimePrompt = 'Recover the {{flavor}} command.';
  fs.mkdirSync(path.dirname(promptPath), { recursive: true });
  fs.writeFileSync(promptPath, runtimePrompt);
  const tracePath = path.join(directory, 'trace.jsonl');
  const plan = { steps: [{ cmd: 'az profile show --active' }] };
  fs.writeFileSync(tracePath, [
    {
      kind: 'agent_event',
      event: 'ToolStarted',
      payload: {
        Name: 'extension_index',
        ArgsJson: '{"query":"profile show"}',
        CallId: 'call-1',
      },
    },
    {
      kind: 'agent_event',
      event: 'ToolCompleted',
      payload: {
        FullResult: '{"command":"az profile show --active"}',
        CallId: 'call-1',
      },
    },
    {
      kind: 'agent_event',
      event: 'ToolStarted',
      payload: { Name: 'propose_plan', ArgsJson: JSON.stringify(plan) },
    },
    {
      kind: 'agent_event',
      event: 'LlmTurnCompleted',
      payload: { Model: 'gpt-test', InputTokens: 12, OutputTokens: 3, ReasoningTokens: 0, CacheWriteTokens: 0 },
    },
  ].map((entry) => JSON.stringify(entry)).join('\n'));
  const output = 'AI help\nNext action:\n1. az profile show --active';

  return {
    provider: new AzclipsAgentProvider({ config: { exe, timeoutMs: 2000, traceDir: directory } }),
    exe,
    prompt: 'Recover the azure cli command.',
    promptSha256: createHash('sha256').update(runtimePrompt).digest('hex'),
    tracePath,
    plan,
    output,
    stdout: `[ERROR] A profile name is required, or use --active.\n${output}\ntrace: ${tracePath}`,
    toolCalls: [{
      name: 'extension_index',
      arguments: { query: 'profile show' },
      callId: 'call-1',
      result: { command: 'az profile show --active' },
    }],
    tokenUsage: {
      prompt: 12,
      completion: 3,
      cached: 0,
      total: 15,
      numRequests: 1,
      completionDetails: {
        reasoning: 0,
        cacheReadInputTokens: 0,
        cacheCreationInputTokens: 0,
      },
    },
  };
}

function mockProcessResult(context, fixture, error, stdout = fixture.stdout, stderr = '') {
  let stdinClosed = false;
  context.mock.method(childProcess, 'execFile', (file, args, options, callback) => {
    assert.equal(file, fixture.exe);
    assert.deepEqual(args, ['profile', 'show']);
    assert.equal(options.timeout, 2000 + PROCESS_TREE_GRACE_MS);
    assert.equal(options.maxBuffer, 8 * 1024 * 1024);
    assert.equal(options.env.AZURECLI_FLAVOR, 'azure cli');
    queueMicrotask(() => callback(error, stdout, stderr));
    return { stdin: { end() { stdinClosed = true; } } };
  });
  context.after(() => assert.equal(stdinClosed, true));
}

test('resolves packaged and debug runtime prompt layouts', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-target-test-'));
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const packagedExe = path.join(directory, 'packaged', 'az.exe');
  const packagedPrompt = path.join(
    path.dirname(packagedExe),
    'Extensions',
    'Microsoft.Azure.Azclips.Extensions.Agent',
    'prompts',
    'oneshot.md',
  );
  fs.mkdirSync(path.dirname(packagedPrompt), { recursive: true });
  fs.writeFileSync(packagedExe, '');
  fs.writeFileSync(packagedPrompt, 'packaged prompt');

  const debugExe = path.join(directory, 'debug', 'Core.Cli', 'az.exe');
  const debugPrompt = path.join(directory, 'debug', 'Core.Agent', 'prompts', 'oneshot.md');
  fs.mkdirSync(path.dirname(debugExe), { recursive: true });
  fs.mkdirSync(path.dirname(debugPrompt), { recursive: true });
  fs.writeFileSync(debugExe, '');
  fs.writeFileSync(debugPrompt, 'debug prompt');

  assert.equal(findRuntimePromptPath(packagedExe), packagedPrompt);
  assert.equal(findRuntimePromptPath(debugExe), debugPrompt);
  assert.deepEqual(readRuntimePrompt(debugExe), {
    content: 'debug prompt',
    path: debugPrompt,
    sha256: createHash('sha256').update('debug prompt').digest('hex'),
  });
});

test('builds an isolated flavor environment without mutating the parent', () => {
  const parent = {
    KEEP_ME: 'value',
    Path: 'C:\\tools',
    AZCLIPS_HOME: 'C:\\private-home',
    AZURE_TENANT_ID: 'tenant',
    COPILOT_SDK_AUTH_TOKEN: 'host-token',
    COPILOT_ENABLE_BUILTIN_GITHUB_MCP: 'true',
    copilot_home: 'C:\\host\\.copilot',
    RUBBER_DUCK_AGENT: 'true',
    AI_AGENT: 'host-agent',
    CLAUDE_AGENT_SDK_VERSION: '1',
    AZCLIPS_EVALUATION_TARGET: '{}',
    AZCLIPS_IN_AGENT_MODE: '1',
    NODE_OPTIONS: '--require hook.js',
    VSCODE_GIT_IPC_HANDLE: 'pipe',
  };
  const snapshot = { ...parent };

  const result = buildEvaluationEnvironment(parent);

  assert.deepEqual(parent, snapshot);
  assert.deepEqual(result, {
    KEEP_ME: 'value',
    Path: 'C:\\tools',
    AZCLIPS_HOME: 'C:\\private-home',
    AZURE_TENANT_ID: 'tenant',
    AZURECLI_FLAVOR: 'azure cli',
  });
  assert.deepEqual(removedHostVariables(parent), [
    'AI_AGENT', 'AZCLIPS_EVALUATION_TARGET', 'AZCLIPS_IN_AGENT_MODE', 'CLAUDE_AGENT_SDK_VERSION',
    'COPILOT_ENABLE_BUILTIN_GITHUB_MCP', 'COPILOT_SDK_AUTH_TOKEN', 'NODE_OPTIONS', 'RUBBER_DUCK_AGENT',
    'VSCODE_GIT_IPC_HANDLE', 'copilot_home',
  ]);
});

test('closes child stdin so unattended permission prompts cannot block', async () => {
  const output = await new Promise((resolve, reject) => {
    execFileUnattended(
      process.execPath,
      [
        '-e',
        "process.stdin.on('end', () => process.stdout.write('stdin-closed')); process.stdin.resume();",
      ],
      { timeout: 2000 },
      (error, stdout) => {
        if (error) reject(error);
        else resolve(stdout);
      },
    );
  });

  assert.equal(output, 'stdin-closed');
});

test('returns provider errors for process failures despite a usable partial plan', async (context) => {
  const scenarios = [
    {
      name: 'timeout or killed process',
      properties: { code: null, killed: true, signal: 'SIGTERM' },
      reason: 'terminated',
      detail: /timeoutMs/,
    },
    {
      name: 'signal takes precedence over a numeric exit code',
      properties: { code: 1, killed: false, signal: 'SIGKILL' },
      reason: 'terminated',
      detail: /SIGKILL/,
    },
    {
      name: 'killed process without a signal',
      properties: { code: 1, killed: true },
      reason: 'terminated',
      detail: /terminated/,
    },
    {
      name: 'missing executable',
      properties: { code: 'ENOENT', syscall: 'spawn private-process-secret' },
      reason: 'spawn',
      detail: /AZCLIPS_EXE/,
    },
    {
      name: 'spawn permission failure',
      properties: { code: 'EACCES' },
      reason: 'spawn',
      detail: /EACCES/,
    },
    {
      name: 'max-buffer failure takes precedence over termination',
      properties: { code: 'ERR_CHILD_PROCESS_STDIO_MAXBUFFER', killed: true, signal: 'SIGTERM' },
      reason: 'max-buffer',
      detail: /maxBuffer/,
    },
    {
      name: 'aborted process',
      properties: { code: 'ABORT_ERR' },
      reason: 'process',
      detail: /ABORT_ERR/,
    },
    {
      name: 'other process failure',
      properties: { code: 'EIO' },
      reason: 'process',
      detail: /EIO/,
    },
    {
      name: 'error without a code',
      properties: {},
      reason: 'process',
      detail: /process/,
    },
    {
      name: 'string codes are not normal CLI exit codes',
      properties: { code: '1' },
      reason: 'process',
      detail: /process/,
    },
    {
      name: 'an error with code zero is not a successful exit',
      properties: { code: 0 },
      reason: 'process',
      detail: /process/,
    },
  ];

  for (const scenario of scenarios) {
    await context.test(scenario.name, async (context) => {
      const fixture = createProviderFixture(context);
      const error = Object.assign(
        new Error('Command failed: az --password private-process-secret'),
        scenario.properties,
        { path: 'private-process-secret', spawnargs: ['--password', 'private-process-secret'] },
      );
      mockProcessResult(context, fixture, error, `${fixture.stdout}\nprivate-process-secret`);

      const response = await fixture.provider.callApi(fixture.prompt, {
        vars: { command: 'profile show', flavor: 'azure cli' },
      });

      assert.match(response.error, scenario.detail);
      assert.equal(Object.hasOwn(response, 'output'), false);
      assert.equal(Object.hasOwn(response.metadata, 'plan'), false);
      assert.equal(response.metadata.executionError, undefined);
      assert.equal(response.metadata.processFailure.type, 'infrastructure');
      assert.equal(response.metadata.processFailure.reason, scenario.reason);
      assert.equal(response.metadata.processFailure.killed, Boolean(error.killed));
      assert.equal(response.metadata.processFailure.signal, error.signal || null);
      if (scenario.reason === 'terminated') {
        assert.equal(response.metadata.processFailure.timeoutMs, 2000);
      }
      if (scenario.reason === 'max-buffer') {
        assert.equal(response.metadata.processFailure.maxBufferBytes, 8 * 1024 * 1024);
      }
      assert.equal(response.prompt, fixture.prompt);
      assert.equal(response.metadata.runtimePromptSha256, fixture.promptSha256);
      assert.deepEqual(response.metadata.models, ['gpt-test']);
      assert.deepEqual(response.metadata.toolCalls, fixture.toolCalls);
      assert.equal(response.metadata.toolTraceAvailable, true);
      assert.deepEqual(response.tokenUsage, fixture.tokenUsage);
      assert.doesNotMatch(JSON.stringify(response), /private-process-secret/);
    });
  }
});

test('returns a provider error for a synchronous spawn failure', async (context) => {
  for (const code of ['ERR_INVALID_ARG_TYPE', 1, undefined]) {
    await context.test(`code ${code}`, async (context) => {
      const fixture = createProviderFixture(context);
      context.mock.method(childProcess, 'execFile', () => {
        throw code === undefined
          ? undefined
          : Object.assign(new Error('private-process-secret'), { code });
      });

      const response = await fixture.provider.callApi(fixture.prompt, {
        vars: { command: 'profile show', flavor: 'azure cli' },
      });

      assert.match(response.error, /could not be started/);
      assert.equal(response.metadata.processFailure.type, 'infrastructure');
      assert.equal(response.metadata.processFailure.reason, 'spawn');
      assert.equal(response.metadata.processFailure.code, code ?? null);
      assert.equal(response.output, undefined);
      assert.equal(response.metadata.plan, undefined);
      assert.equal(response.metadata.toolTraceAvailable, false);
      assert.deepEqual(response.metadata.models, []);
      assert.deepEqual(response.metadata.toolCalls, []);
      assert.equal(response.tokenUsage, undefined);
      assert.doesNotMatch(JSON.stringify(response), /private-process-secret/);
    });
  }
});

test('keeps a process failure as a provider error when its partial trace is unreadable', async (context) => {
  const fixture = createProviderFixture(context);
  fs.rmSync(fixture.tracePath);
  fs.mkdirSync(fixture.tracePath);
  mockProcessResult(context, fixture, Object.assign(new Error('Timed out'), {
    code: null,
    killed: true,
    signal: 'SIGTERM',
  }));

  const response = await fixture.provider.callApi(fixture.prompt, {
    vars: { command: 'profile show', flavor: 'azure cli' },
  });

  assert.match(response.error, /terminated/);
  assert.equal(response.metadata.processFailure.reason, 'terminated');
  assert.match(response.metadata.traceExtractionError, /trace/i);
  assert.equal(response.output, undefined);
  assert.equal(response.metadata.plan, undefined);
});

test('scores completed CLI responses including numeric nonzero recovery', async (context) => {
  for (const exitCode of [0, 1, 42]) {
    await context.test(`exit code ${exitCode}`, async (context) => {
      const fixture = createProviderFixture(context);
      const error = exitCode ? Object.assign(new Error('Command failed'), {
        code: exitCode,
        killed: false,
        signal: null,
      }) : null;
      mockProcessResult(context, fixture, error, exitCode ? fixture.stdout : (
        `${fixture.output}\ntrace: ${fixture.tracePath}`
      ));

      const response = await fixture.provider.callApi(fixture.prompt, {
        vars: { command: 'profile show', flavor: 'azure cli' },
      });

      assert.equal(response.error, undefined);
      assert.equal(response.output, fixture.output);
      assert.equal(response.prompt, fixture.prompt);
      assert.equal(response.metadata.processFailure, undefined);
      assert.equal(response.metadata.exitCode, exitCode);
      assert.equal(response.metadata.resultType, 'azclips.plan.v1');
      assert.equal(response.metadata.runtimePromptSha256, fixture.promptSha256);
      assert.deepEqual(response.metadata.plan, fixture.plan);
      assert.deepEqual(response.metadata.models, ['gpt-test']);
      assert.deepEqual(response.metadata.toolCalls, fixture.toolCalls);
      assert.equal(response.metadata.toolTraceAvailable, true);
      assert.deepEqual(response.tokenUsage, fixture.tokenUsage);
      assert.deepEqual(response.metadata.executionError, exitCode ? {
        source: 'azclips',
        exitCode,
        message: 'A profile name is required, or use --active.',
      } : undefined);
      assert.deepEqual(response.metadata.originalError, response.metadata.executionError);
    });
  }
});

test('finds the final trace path from mixed output', () => {
  const output = [
    'trace: C:\\traces\\first.jsonl',
    'other output',
    '   trace: C:\\traces\\final.jsonl',
  ].join('\n');

  assert.equal(findTracePath(output), 'C:\\traces\\final.jsonl');
});

test('keeps stderr diagnostics out of the response while preserving stderr CLI errors', async (context) => {
  const fixture = createProviderFixture(context);
  const stderr = [
    '[ERROR] A profile name is required, or use --active.',
    'VERBOSE: [ProfileService] Loading profiles from: C:\\private\\profiles.json',
    `trace: ${fixture.tracePath}`,
  ].join('\n');
  const answer = `${fixture.output}\nVERBOSE is a logging level mentioned in this response.`;
  mockProcessResult(context, fixture, Object.assign(new Error('CLI failure'), { code: 1 }), answer, stderr);
  const response = await fixture.provider.callApi(fixture.prompt, { vars: { command: 'profile show' } });
  assert.equal(response.output, answer);
  assert.equal(response.metadata.originalError.message, 'A profile name is required, or use --active.');
  assert.deepEqual(response.metadata.models, ['gpt-test']);
  assert.doesNotMatch(response.output, /C:\\private|Loading profiles from/);
});

test('does not substitute stderr diagnostics for an absent rendered response', async (context) => {
  const fixture = createProviderFixture(context);
  mockProcessResult(context, fixture, null, '', `VERBOSE: startup diagnostic\ntrace: ${fixture.tracePath}`);
  const response = await fixture.provider.callApi(fixture.prompt, { vars: { command: 'profile show' } });
  assert.equal(response.output, '');
  assert.deepEqual(response.metadata.plan, fixture.plan);
});

test('manual recovery extracts one exact failure ID from structured or human hints', () => {
  const id = '1234-2026-1008-0000-0001';
  const structured = JSON.stringify({ recovery: { id, command: `az troubleshoot --id ${id}` } });
  const human = `To troubleshoot this error with AI, run: az troubleshoot --id ${id}\n`;
  assert.equal(findFailureId(structured), id);
  assert.equal(findFailureId(human), id);
  assert.equal(findFailureId(`${structured}\n${human}`), id);
  assert.throws(() => findFailureId('az troubleshoot'), /one exact failure ID/);
  assert.throws(() => findFailureId(`${human}az troubleshoot --id 1234-2026-1008-0000-0002`),
    /one exact failure ID/);
  assert.throws(() => findFailureId('az troubleshoot --id --list'), /one exact failure ID/);
});

test('extracts the original structured CLI error separately from diagnostics and the recovery hint', () => {
  const diagnostic = JSON.stringify({ recoveryWarning: 'An obsolete setting is present.' });
  const error = {
    error: {
      code: 'UsageError',
      message: 'Required parameter "--resource-group" is missing; value may contain {braces}.',
    },
    recovery: { command: 'az troubleshoot --id 1234-2026-1008-0000-0001' },
  };
  for (const stderr of [JSON.stringify(error), `${diagnostic}\n${JSON.stringify(error, null, 2)}\n`]) {
    assert.deepEqual(originalCliError('', stderr, 2), {
      source: 'azclips',
      exitCode: 2,
      code: 'UsageError',
      message: error.error.message,
    });
  }
});

test('captures plaintext CLI errors without confusing recovery hints or model output with the input error', () => {
  assert.deepEqual(originalCliError('', [
    '[ERROR] The requested resource group was not found.',
    'To troubleshoot this error with AI, run: az troubleshoot --id 1234-2026-1008-0000-0001',
  ].join('\n'), 1), {
    source: 'azclips',
    exitCode: 1,
    message: 'The requested resource group was not found.',
  });
  assert.equal(originalCliError('', '', 1).message, 'The CLI exited with code 1 without an error message.');
});

test('redacts credential-shaped values from captured original error messages', () => {
  const error = originalCliError('', JSON.stringify({
    error: { code: 'Unauthorized', message: 'Bearer private-token at https://example.test/?sig=private-signature' },
  }), 1);
  assert.doesNotMatch(JSON.stringify(error), /private-token|private-signature/);
  assert.match(error.message, /\[REDACTED\]/);
});

test('manual recovery records the failure then analyzes its exact ID with closed stdin', async (context) => {
  const fixture = createProviderFixture(context);
  const answer = `${fixture.output}\nRunning this command will display the active profile.`;
  const provider = new AzclipsAgentProvider({
    config: { exe: fixture.exe, timeoutMs: 2000, recoveryMode: 'manual' },
  });
  const id = '1234-2026-1008-0000-0001';
  const calls = [];
  let closed = 0;
  context.mock.method(childProcess, 'execFile', (file, args, options, callback) => {
    assert.equal(file, fixture.exe);
    assert.ok(options.timeout > 0 && options.timeout <= 2000 + PROCESS_TREE_GRACE_MS);
    calls.push(args);
    queueMicrotask(() => {
      if (calls.length === 1) {
        callback(Object.assign(new Error('Command failed'), { code: 2 }), '',
          JSON.stringify({
            error: { code: 'UsageError', message: 'The requested profile was not found.' },
            recovery: { id, command: `az troubleshoot --id ${id}` },
          }));
      } else {
        callback(null, `${renderSpinnerFrame('⠋', 'planning a fix')}${answer}\ntrace: ${fixture.tracePath}`,
          'VERBOSE: [ProfileService] Loading profiles from: C:\\private\\profiles.json');
      }
    });
    return { stdin: { end() { closed += 1; } } };
  });
  const response = await provider.callApi(fixture.prompt, { vars: { command: 'profile show' } });
  assert.deepEqual(calls, [
    ['profile', 'show'],
    ['troubleshoot', '--id', id, '--style', 'oneshot'],
  ]);
  assert.equal(closed, 2);
  assert.equal(response.error, undefined);
  assert.equal(response.output, answer);
  assert.equal(response.metadata.failureId, id);
  assert.equal(response.metadata.failureExitCode, 2);
  assert.equal(response.metadata.recoveryMode, 'manual');
  assert.deepEqual(response.metadata.originalError, {
    source: 'azclips',
    exitCode: 2,
    code: 'UsageError',
    message: 'The requested profile was not found.',
  });
  assert.ok(response.metadata.failureCaptureDurationMs >= 0);
  assert.ok(response.metadata.troubleshootDurationMs >= 0);
  assert.ok(!Number.isNaN(Date.parse(response.metadata.observationStartedAt)));
  assert.deepEqual(response.metadata.plan, fixture.plan);
  assert.deepEqual(response.tokenUsage, fixture.tokenUsage);
});

test('manual recovery does not run AI for successful stimuli or missing failure IDs', async (context) => {
  for (const error of [null, Object.assign(new Error('Command failed'), { code: 2 })]) {
    await context.test(error ? 'missing ID' : 'successful stimulus', async (context) => {
      const fixture = createProviderFixture(context);
      const target = { version: '0.1.3+abc', packageSha256: 'a'.repeat(64), packageFiles: 3 };
      withEnvironment(context, {
        AZCLIPS_EVALUATION_TARGET: JSON.stringify(target),
        AZCLIPS_EVALUATION_WARMUP_OBSERVATIONS: '1',
      });
      const provider = new AzclipsAgentProvider({ config: { exe: fixture.exe, recoveryMode: 'manual' } });
      let calls = 0;
      context.mock.method(childProcess, 'execFile', (_file, _args, _options, callback) => {
        calls += 1;
        queueMicrotask(() => callback(error, 'No recovery hint', ''));
        return { stdin: { end() {} } };
      });
      const response = await provider.callApi(fixture.prompt, { vars: { command: 'profile show' } });
      assert.equal(calls, 1);
      assert.equal(response.metadata.processFailure.reason, 'failure-capture');
      assert.deepEqual(response.metadata.target, target);
      assert.equal(response.metadata.warmupObservations, 1);
      assert.ok(response.metadata.removedHostVariables.includes('AZCLIPS_EVALUATION_TARGET'));
      assert.ok(response.error);
      assert.equal(response.output, undefined);
    });
  }
});

test('rejects unsupported recovery modes', () => {
  assert.throws(() => new AzclipsAgentProvider({ config: { recoveryMode: 'latest' } }),
    /recoveryMode must be automatic or manual/);
});

test('a failed troubleshoot process is an execution error even when it produced a partial plan', async (context) => {
  const fixture = createProviderFixture(context);
  const provider = new AzclipsAgentProvider({ config: { exe: fixture.exe, recoveryMode: 'manual' } });
  const id = '1234-2026-1008-0000-0001';
  let calls = 0;
  context.mock.method(childProcess, 'execFile', (_file, _args, _options, callback) => {
    const recording = ++calls === 1;
    queueMicrotask(() => callback(
      Object.assign(new Error('private-process-secret'), { code: recording ? 2 : 9 }),
      recording ? `az troubleshoot --id ${id}` : fixture.stdout,
      '',
    ));
    return { stdin: { end() {} } };
  });
  const response = await provider.callApi(fixture.prompt, { vars: { command: 'profile show' } });
  assert.equal(calls, 2);
  assert.equal(response.metadata.processFailure.reason, 'troubleshoot');
  assert.equal(response.metadata.failureId, id);
  assert.equal(response.metadata.exitCode, 9);
  assert.match(response.error, /exit code 9/);
  assert.equal(response.output, undefined);
  assert.equal(response.metadata.plan, undefined);
  assert.deepEqual(response.tokenUsage, fixture.tokenUsage);
  assert.doesNotMatch(JSON.stringify(response), /private-process-secret/);
});

test('removes local trace paths from provider output', () => {
  assert.equal(
    stripNoise('Plan output\ntrace: C:\\private\\trace.jsonl'),
    'Plan output',
  );
});

test('preserves ordinary response text and Braille characters instead of guessing status words', () => {
  const answer = [
    'AI help',
    'Analyzing the error shows that --location is missing.',
    '  Running this command will list the installed extensions.',
    'Thinking about the requested scope does not require changing subscriptions.',
    'Querying the group did not confirm that it exists.',
    'Planning the fix requires choosing the intended resource group.',
    'Wrapping this value in quotes preserves its spaces.',
    'Literal Braille characters: ⠋ ⣿ ⠀',
    '  ⠋ Running is part of this quoted terminal example, not an erased frame.',
  ].join('\n');
  assert.equal(stripNoise(answer), answer);
});

test('removes the renderer frame-and-erase protocol without dropping the following answer', () => {
  const frames = ['⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏'];
  const answer = 'Planning the fix preserves the requested group.\nRunning this command needs a location.';
  const progress = frames.map((frame, index) =>
    renderSpinnerFrame(frame, index % 2 ? 'reading file' : 'Analyzing error root cause using AI')).join('');
  assert.equal(stripNoise(`${progress}${answer}`), answer);
  assert.equal(stripNoise(`AI help\r\n${progress}${answer}\r\n${renderSpinnerFrame('⠏', 'querying ARM')}`),
    `AI help\n${answer}`);
  assert.equal(stripNoise(`${renderSpinnerFrame('⠋', 'custom tool 🔍')}${answer}`), answer);
  assert.equal(stripNoise(progress), '');
});

test('preserves text that does not match the exact renderer frame boundary and erase width', () => {
  const samples = [
    `Example: ${renderSpinnerFrame('⠋', 'thinking')}keep this text`,
    `AI help\n${renderSpinnerFrame('⣿', 'thinking')}keep this text`,
    'AI help\n  ⠋ thinking\r \rkeep this text',
    'AI help\n ⠋ thinking\r           \rkeep this text',
    'AI help\n  ⠋ thinking\nkeep this text',
  ];
  for (const sample of samples) assert.equal(stripNoise(sample), sample);
});

test('preserves plaintext CLI diagnostics that begin with status-like words', () => {
  const message = 'Running the command failed because the profile is unavailable.';
  assert.deepEqual(originalCliError('', `${renderSpinnerFrame('⠋', 'thinking')}${message}`, 2), {
    source: 'azclips',
    exitCode: 2,
    message,
  });
});

test('passes the complete filtered response through the provider and semantic Judge input', async (context) => {
  const fixture = createProviderFixture(context);
  const answer = `${fixture.output}\nPlanning the fix keeps the current profile.\nRunning this command shows it.`;
  const stdout = [
    `${renderSpinnerFrame('⠋', 'Analyzing error root cause using AI')}[ERROR] A profile name is required, or use --active.`,
    `${renderSpinnerFrame('⠙', 'reading file')}${answer}`,
    `trace: ${fixture.tracePath}`,
  ].join('\n');
  mockProcessResult(context, fixture, Object.assign(new Error('CLI failure'), { code: 1 }), stdout);
  const vars = { command: 'profile show', goal: 'Show the active profile.' };
  const response = await fixture.provider.callApi(fixture.prompt, { vars });
  assert.equal(response.error, undefined);
  assert.equal(response.output, answer);
  assert.deepEqual(response.metadata.plan, fixture.plan);
  assert.equal(JSON.parse(judgeInput(response.output, { vars, metadata: response.metadata })).response, answer);
});

test('separates the real AZCLIPS error from the complete AI plan', () => {
  assert.deepEqual(
    splitAgentOutput([
      '[ERROR] A profile name is required, or use --active.',
      'AI help',
      '───────',
      'What went wrong:',
      '• A profile name or --active is required.',
      'Next action:',
      '1. az profile show --active',
    ].join('\n'), 1),
    {
      output: [
        'AI help',
        '───────',
        'What went wrong:',
        '• A profile name or --active is required.',
        'Next action:',
        '1. az profile show --active',
      ].join('\n'),
      executionError: {
        source: 'azclips',
        exitCode: 1,
        message: 'A profile name is required, or use --active.',
      },
    },
  );
});

test('reads the final structured plan and ignores malformed trace lines', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-target-test-'));
  const tracePath = path.join(directory, 'trace.jsonl');
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));

  const entries = [
    'not-json',
    JSON.stringify({
      kind: 'agent_event',
      event: 'ToolStarted',
      payload: {
        Name: 'propose_plan',
        ArgsJson: JSON.stringify({ steps: [{ cmd: 'az group create --name first' }] }),
      },
    }),
    JSON.stringify({
      kind: 'agent_event',
      event: 'ToolStarted',
      payload: {
        Name: 'propose_plan',
        ArgsJson: JSON.stringify({ steps: [{ cmd: 'az vm create --name final' }] }),
      },
    }),
  ];
  fs.writeFileSync(tracePath, entries.join('\n'));

  assert.deepEqual(readStructuredPlan(tracePath), {
    steps: [{ cmd: 'az vm create --name final' }],
  });
});

test('aggregates all LLM usage events using Promptfoo token semantics', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-target-test-'));
  const tracePath = path.join(directory, 'trace.jsonl');
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));

  const entries = [
    JSON.stringify({
      kind: 'agent_event',
      event: 'LlmTurnCompleted',
      payload: {
        Model: 'gpt-test',
        InputTokens: 150,
        OutputTokens: 20,
        CachedTokens: 100,
        ReasoningTokens: 5,
        CacheWriteTokens: 10,
      },
    }),
    'not-json',
    JSON.stringify({
      kind: 'agent_event',
      event: 'LlmTurnCompleted',
      payload: {
        Model: 'gpt-test',
        InputTokens: 250,
        OutputTokens: 30,
        CachedTokens: 150,
        ReasoningTokens: 7,
        CacheWriteTokens: 15,
      },
    }),
  ];
  fs.writeFileSync(tracePath, entries.join('\n'));

  assert.deepEqual(readTokenUsage(tracePath), {
    prompt: 400,
    completion: 50,
    cached: 250,
    total: 450,
    numRequests: 2,
    completionDetails: {
      reasoning: 12,
      cacheReadInputTokens: 250,
      cacheCreationInputTokens: 25,
    },
  });
  assert.deepEqual(readModels(tracePath), ['gpt-test']);
});

test('does not fabricate zero token usage when a trace has no usage event', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-target-test-'));
  const tracePath = path.join(directory, 'trace.jsonl');
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  fs.writeFileSync(tracePath, JSON.stringify({ kind: 'trace_start' }));

  assert.equal(readTokenUsage(tracePath), undefined);
});

test('distinguishes unreported optional token totals from reported zero', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-usage-test-'));
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const tracePath = path.join(directory, 'trace.jsonl');
  const base = { Model: 'gpt-test', InputTokens: 10, OutputTokens: 2, CachedTokens: 0 };
  const writeUsage = (payloads) => fs.writeFileSync(tracePath, payloads.map(payload =>
    JSON.stringify({ kind: 'agent_event', event: 'LlmTurnCompleted', payload })).join('\n'));
  for (const missing of [undefined, null]) {
    writeUsage([
      { ...base, ReasoningTokens: 5, CacheWriteTokens: 10 },
      { ...base, ReasoningTokens: missing, CacheWriteTokens: missing },
    ]);
    const usage = readTokenUsage(tracePath);
    assert.equal(usage.total, 24);
    assert.equal(usage.numRequests, 2);
    assert.equal(Object.hasOwn(usage.completionDetails, 'reasoning'), false);
    assert.equal(Object.hasOwn(usage.completionDetails, 'cacheCreationInputTokens'), false);
  }
  writeUsage([{ ...base, ReasoningTokens: 0, CacheWriteTokens: 0 }]);
  assert.equal(readTokenUsage(tracePath).completionDetails.reasoning, 0);
  assert.equal(readTokenUsage(tracePath).completionDetails.cacheCreationInputTokens, 0);
});

test('model identity comes only from observed usage events, not unknown placeholders', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-model-test-'));
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const tracePath = path.join(directory, 'trace.jsonl');
  const rows = [undefined, null, '', 'unknown', ' UNKNOWN ', ' gpt-test ', 'gpt-test'];
  fs.writeFileSync(tracePath, rows.map(Model =>
    JSON.stringify({ kind: 'agent_event', event: 'LlmTurnCompleted', payload: { Model } })).join('\n'));
  assert.deepEqual(readModels(tracePath), ['gpt-test']);
});

test('pairs tool arguments with complete results and failures', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-target-test-'));
  const tracePath = path.join(directory, 'trace.jsonl');
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));

  const entries = [
    'not-json',
    JSON.stringify({
      kind: 'agent_event',
      event: 'ToolStarted',
      payload: {
        Name: 'extension_index',
        ArgsJson: JSON.stringify({ query: 'deployment group create' }),
        CallId: 'call-1',
        At: '2026-07-30T08:00:00.000Z',
      },
    }),
    JSON.stringify({
      kind: 'agent_event',
      event: 'ToolStarted',
      payload: {
        Name: 'propose_plan',
        ArgsJson: JSON.stringify({ steps: [] }),
        CallId: 'internal-call',
      },
    }),
    JSON.stringify({
      kind: 'agent_event',
      event: 'ToolCompleted',
      payload: {
        FullResult: JSON.stringify({
          command: 'az deployment group create',
          apiKey: 'secret-value',
        }),
        IsError: false,
        Elapsed: '00:00:00.0180000',
        CallId: 'call-1',
      },
    }),
    JSON.stringify({
      kind: 'agent_event',
      event: 'ToolStarted',
      payload: {
        Name: 'powershell',
        ArgsJson: '{"command":"bad',
        CallId: 'call-2',
        ParentCallId: 'parent-1',
      },
    }),
    JSON.stringify({
      kind: 'agent_event',
      event: 'ToolFailed',
      payload: {
        Error: 'Command failed',
        Elapsed: '00:00:00.0420000',
        CallId: 'call-2',
      },
    }),
  ];
  fs.writeFileSync(tracePath, entries.join('\n'));

  assert.deepEqual(readToolCalls(tracePath), [
    {
      name: 'extension_index',
      arguments: { query: 'deployment group create' },
      callId: 'call-1',
      at: '2026-07-30T08:00:00.000Z',
      elapsedMs: 18,
      result: { command: 'az deployment group create', apiKey: '[REDACTED]' },
    },
    {
      name: 'powershell',
      arguments: '{"command":"bad',
      callId: 'call-2',
      parentCallId: 'parent-1',
      elapsedMs: 42,
      error: 'Command failed',
    },
  ]);
});

test('returns unavailable values when the trace file is missing', () => {
  const missing = path.join(os.tmpdir(), 'azclips-missing-trace.jsonl');
  fs.rmSync(missing, { force: true });

  assert.equal(readStructuredPlan(missing), undefined);
  assert.equal(readTokenUsage(missing), undefined);
  assert.deepEqual(readModels(missing), []);
  assert.deepEqual(readToolCalls(missing), []);
  assert.equal(toolTraceAvailable(missing), false);
  assert.equal(readTiming(missing, 1000), undefined);
});

test('distinguishes an available zero-call trace from a missing trace', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-target-test-'));
  const tracePath = path.join(directory, 'trace.jsonl');
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  fs.writeFileSync(tracePath, JSON.stringify({ kind: 'trace_start' }));

  assert.equal(toolTraceAvailable(tracePath), true);
  assert.deepEqual(readToolCalls(tracePath), []);
});

function timedTrace() {
  const at = (seconds) => new Date(Date.UTC(2026, 9, 12, 7, 38, 0) + seconds * 1000).toISOString();
  return [
    { kind: 'trace_start', ts: at(0) },
    { kind: 'session_start', ts: at(3) },
    { kind: 'agent_event', event: 'LlmTurnCompleted', ts: at(20), payload: { Elapsed: '00:00:12.0000000', At: at(20) } },
    { kind: 'agent_event', event: 'ToolStarted', ts: at(20), payload: { Name: 'find_resources', CallId: 'a', At: at(20) } },
    { kind: 'agent_event', event: 'ToolStarted', ts: at(20), payload: { Name: 'get_command', CallId: 'b', At: at(20) } },
    { kind: 'agent_event', event: 'ToolCompleted', ts: at(23), payload: { CallId: 'a', Elapsed: '00:00:03', At: at(23) } },
    { kind: 'agent_event', event: 'ToolCompleted', ts: at(24), payload: { CallId: 'b', Elapsed: '00:00:04.0000000', At: at(24) } },
    { kind: 'agent_event', event: 'ToolCompleted', ts: at(33), payload: { Name: 'propose_plan', CallId: 'p', Elapsed: '00:00:01', At: at(33), IsError: false } },
    { kind: 'agent_event', event: 'LlmTurnCompleted', ts: at(34), payload: { Elapsed: '00:00:10', At: at(34) } },
    { kind: 'agent_event', event: 'ToolFailed', ts: at(35.5), payload: { CallId: 'c', Elapsed: '00:00:01.5', At: at(35.5) } },
    { kind: 'trace_end', ts: at(36) },
  ];
}

test('splits Agent process time into model, tool and remaining host phases from the trace', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-target-test-'));
  const tracePath = path.join(directory, 'trace.jsonl');
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  fs.writeFileSync(tracePath, [...timedTrace().map((entry) => JSON.stringify(entry)), '{malformed'].join('\n'));

  assert.deepEqual(readTiming(tracePath, 38000), {
    processMs: 38000,
    traceMs: 36000,
    sessionStartMs: 3000,
    planProposedMs: 33000,
    firstLlmRequestMs: 5000,
    llmMs: 22000,
    llmTurns: 2,
    toolMs: 6500,
    hostMs: 10500,
  });
});

test('omits timing phases that the trace does not report instead of recording zero', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-target-test-'));
  const tracePath = path.join(directory, 'trace.jsonl');
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));

  fs.writeFileSync(tracePath, JSON.stringify({ kind: 'agent_event', event: 'LlmTurnCompleted', payload: { Model: 'gpt-test' } }));
  assert.equal(readTiming(tracePath, 1000), undefined);

  fs.writeFileSync(tracePath, [
    { kind: 'trace_start', ts: '2026-10-12T07:38:00.0000000+00:00' },
    { kind: 'trace_end', ts: '2026-10-12T07:38:01.2500000+00:00' },
  ].map((entry) => JSON.stringify(entry)).join('\n'));
  assert.deepEqual(readTiming(tracePath, undefined), { traceMs: 1250 });
});

test('reads run-level JSON facts only from well-formed objects', () => {
  assert.deepEqual(readJsonEnvironment('FACTS', { FACTS: '{"version":"0.1.3+abc"}' }), { version: '0.1.3+abc' });
  for (const value of [undefined, '', '  ', 'not json', '[1]', '"text"', 'null']) {
    assert.equal(readJsonEnvironment('FACTS', { FACTS: value }), undefined);
  }
});

function withEnvironment(context, values) {
  const previous = Object.fromEntries(Object.keys(values).map((name) => [
    name, Object.prototype.hasOwnProperty.call(process.env, name) ? process.env[name] : undefined,
  ]));
  const apply = (entries) => {
    for (const [name, value] of Object.entries(entries)) {
      if (value === undefined) delete process.env[name];
      else process.env[name] = value;
    }
  };
  apply(values);
  context.after(() => apply(previous));
}

test('records the run target, cache state and phase timing on every manual observation', async (context) => {
  const fixture = createProviderFixture(context);
  fs.writeFileSync(fixture.tracePath, [
    ...timedTrace().map((entry) => JSON.stringify(entry)),
    JSON.stringify({ kind: 'agent_event', event: 'ToolStarted', payload: { Name: 'propose_plan', ArgsJson: JSON.stringify(fixture.plan) } }),
  ].join('\n'));
  const target = { version: '0.1.3+abc', packageSha256: 'a'.repeat(64), packageFiles: 3 };
  const resourceCache = { mode: 'refresh', resources: 42, fixtureGroup: 'azclips-test-rg', fixtureGroupFound: true };
  withEnvironment(context, {
    AZCLIPS_EVALUATION_TARGET: JSON.stringify(target),
    AZCLIPS_EVALUATION_RESOURCE_CACHE: JSON.stringify(resourceCache),
    AZCLIPS_EVALUATION_AGENT_SETTINGS: JSON.stringify({ 'agent.builtin.modelId': { value: 'gpt-test', source: 'default' } }),
    AZCLIPS_EVALUATION_WARMUP_OBSERVATIONS: '1',
  });
  const provider = new AzclipsAgentProvider({
    config: { exe: fixture.exe, timeoutMs: 2000, recoveryMode: 'manual' },
  });
  const id = '1234-2026-1008-0000-0001';
  let call = 0;
  context.mock.method(childProcess, 'execFile', (file, args, options, callback) => {
    call += 1;
    queueMicrotask(() => {
      if (call === 1) {
        callback(Object.assign(new Error('Command failed'), { code: 2 }), '', `az troubleshoot --id ${id}`);
      } else {
        callback(null, `${fixture.output}\ntrace: ${fixture.tracePath}`, '');
      }
    });
    return { stdin: { end() {} } };
  });

  const response = await provider.callApi(fixture.prompt, { vars: { command: 'profile show' } });

  assert.equal(response.error, undefined);
  assert.deepEqual(response.metadata.target, target);
  assert.deepEqual(response.metadata.resourceCache, resourceCache);
  assert.equal(response.metadata.timing.processMs, response.metadata.troubleshootDurationMs);
  assert.equal(response.metadata.timing.llmMs, 22000);
  assert.equal(response.metadata.timing.llmTurns, 2);
  assert.equal(response.metadata.timing.toolMs, 6500);
  assert.equal(response.metadata.timingExtractionError, undefined);
  assert.deepEqual(response.metadata.agentSettings, { 'agent.builtin.modelId': { value: 'gpt-test', source: 'default' } });
  assert.equal(response.metadata.warmupObservations, 1);
  assert.deepEqual(response.metadata.planActivity, { attempts: 1, rejections: 0, rejectionReasons: [] });
});

test('does not invent run facts when the wrapper did not record them', async (context) => {
  const fixture = createProviderFixture(context);
  withEnvironment(context, {
    AZCLIPS_EVALUATION_TARGET: undefined,
    AZCLIPS_EVALUATION_RESOURCE_CACHE: 'not json',
    AZCLIPS_EVALUATION_AGENT_SETTINGS: undefined,
    AZCLIPS_EVALUATION_WARMUP_OBSERVATIONS: '1.5',
  });
  const provider = new AzclipsAgentProvider({ config: { exe: fixture.exe, timeoutMs: 2000 } });
  mockProcessResult(context, fixture, null, `${fixture.output}\ntrace: ${fixture.tracePath}`);

  const response = await provider.callApi(fixture.prompt, { vars: { command: 'profile show', flavor: 'azure cli' } });

  assert.equal(response.metadata.target, undefined);
  assert.equal(response.metadata.resourceCache, undefined);
  assert.equal(response.metadata.agentSettings, undefined);
  assert.equal(response.metadata.warmupObservations, undefined);
  assert.equal(response.metadata.timing, undefined);
});

test('counts plans rejected by the host and keeps their reasons', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-target-test-'));
  const tracePath = path.join(directory, 'trace.jsonl');
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const reason = 'Denied by preToolUse hook: Invalid propose_plan arguments: steps must have at least one entry';
  fs.writeFileSync(tracePath, [
    { kind: 'agent_event', event: 'ToolStarted', payload: { Name: 'propose_plan', CallId: 'p1' } },
    { kind: 'agent_event', event: 'ToolFailed', payload: { Name: 'propose_plan', CallId: 'p1', Error: reason } },
    { kind: 'agent_event', event: 'ToolStarted', payload: { Name: 'propose_plan', CallId: 'p2' } },
    { kind: 'agent_event', event: 'ToolCompleted', payload: { Name: 'propose_plan', CallId: 'p2', IsError: false } },
    { kind: 'agent_event', event: 'ToolStarted', payload: { Name: 'get_command', CallId: 'c1' } },
  ].map((entry) => JSON.stringify(entry)).join('\n'));

  assert.deepEqual(readPlanActivity(tracePath), { attempts: 2, rejections: 1, rejectionReasons: [reason] });
  assert.equal(readPlanActivity(path.join(directory, 'missing.jsonl')), undefined);
});

test('ends the whole Agent process tree, not only az.exe', (context) => {
  const calls = [];
  context.mock.method(childProcess, 'spawnSync', (file, args, options) => {
    calls.push([file, args, options.windowsHide]);
    return { status: 0 };
  });
  let killed;
  terminateProcessTree({ pid: 4321, kill(signal) { killed = signal; } }, 'win32');
  assert.deepEqual(calls, [['taskkill', ['/PID', '4321', '/T', '/F'], true]]);
  assert.equal(killed, undefined);

  terminateProcessTree({ pid: 4321, kill(signal) { killed = signal; } }, 'linux');
  assert.equal(killed, 'SIGKILL');
  assert.equal(calls.length, 1);
  terminateProcessTree(undefined, 'win32');
  assert.equal(calls.length, 1);
});

test('a timed-out observation ends its process tree and is reported as a termination', async (context) => {
  const fixture = createProviderFixture(context);
  const provider = new AzclipsAgentProvider({ config: { exe: fixture.exe, timeoutMs: 20 } });
  let finish;
  let treeEnded = false;
  context.mock.method(childProcess, 'spawnSync', () => {
    treeEnded = true;
    queueMicrotask(() => finish(Object.assign(new Error('Command failed'), { code: 1 }), '', ''));
    return { status: 0 };
  });
  context.mock.method(childProcess, 'execFile', (file, args, options, callback) => {
    assert.equal(options.timeout, 20 + PROCESS_TREE_GRACE_MS);
    finish = callback;
    return {
      pid: 4321,
      stdin: { end() {} },
      kill() {
        treeEnded = true;
        queueMicrotask(() => finish(Object.assign(new Error('Command failed'), { code: null, signal: 'SIGKILL' }), '', ''));
      },
    };
  });

  const response = await provider.callApi(fixture.prompt, { vars: { command: 'profile show', flavor: 'azure cli' } });

  assert.equal(treeEnded, true);
  assert.equal(response.metadata.processFailure.reason, 'terminated');
  assert.match(response.error, /terminated/);
});