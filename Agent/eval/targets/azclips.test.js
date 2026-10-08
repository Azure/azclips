const assert = require('node:assert/strict');
const childProcess = require('node:child_process');
const { createHash } = require('node:crypto');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const AzclipsAgentProvider = require('./azclips');
const {
  buildEvaluationEnvironment,
  execFileUnattended,
  findTracePath,
  findFailureId,
  findRuntimePromptPath,
  readRuntimePrompt,
  readModels,
  readStructuredPlan,
  readToolCalls,
  readTokenUsage,
  splitAgentOutput,
  stripNoise,
  toolTraceAvailable,
} = AzclipsAgentProvider;

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
      payload: { Model: 'gpt-test', InputTokens: 12, OutputTokens: 3 },
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

function mockProcessResult(context, fixture, error, stdout = fixture.stdout) {
  let stdinClosed = false;
  context.mock.method(childProcess, 'execFile', (file, args, options, callback) => {
    assert.equal(file, fixture.exe);
    assert.deepEqual(args, ['profile', 'show']);
    assert.equal(options.timeout, 2000);
    assert.equal(options.maxBuffer, 8 * 1024 * 1024);
    assert.equal(options.env.AZURECLI_FLAVOR, 'azure cli');
    queueMicrotask(() => callback(error, stdout, ''));
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
  };

  const result = buildEvaluationEnvironment(parent);

  assert.deepEqual(parent, {
    KEEP_ME: 'value',
  });
  assert.equal(result.KEEP_ME, 'value');
  assert.equal(result.AZURECLI_FLAVOR, 'azure cli');
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

test('manual recovery records the failure then analyzes its exact ID with closed stdin', async (context) => {
  const fixture = createProviderFixture(context);
  const provider = new AzclipsAgentProvider({
    config: { exe: fixture.exe, timeoutMs: 2000, recoveryMode: 'manual' },
  });
  const id = '1234-2026-1008-0000-0001';
  const calls = [];
  let closed = 0;
  context.mock.method(childProcess, 'execFile', (file, args, options, callback) => {
    assert.equal(file, fixture.exe);
    assert.ok(options.timeout > 0 && options.timeout <= 2000);
    calls.push(args);
    queueMicrotask(() => {
      if (calls.length === 1) {
        callback(Object.assign(new Error('Command failed'), { code: 2 }), '',
          JSON.stringify({ recovery: { id, command: `az troubleshoot --id ${id}` } }));
      } else {
        callback(null, `${fixture.output}\ntrace: ${fixture.tracePath}`, '');
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
  assert.equal(response.metadata.failureId, id);
  assert.equal(response.metadata.failureExitCode, 2);
  assert.equal(response.metadata.recoveryMode, 'manual');
  assert.ok(response.metadata.failureCaptureDurationMs >= 0);
  assert.ok(response.metadata.troubleshootDurationMs >= 0);
  assert.deepEqual(response.metadata.plan, fixture.plan);
  assert.deepEqual(response.tokenUsage, fixture.tokenUsage);
});

test('manual recovery does not run AI for successful stimuli or missing failure IDs', async (context) => {
  for (const error of [null, Object.assign(new Error('Command failed'), { code: 2 })]) {
    await context.test(error ? 'missing ID' : 'successful stimulus', async (context) => {
      const fixture = createProviderFixture(context);
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
});

test('distinguishes an available zero-call trace from a missing trace', (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-target-test-'));
  const tracePath = path.join(directory, 'trace.jsonl');
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  fs.writeFileSync(tracePath, JSON.stringify({ kind: 'trace_start' }));

  assert.equal(toolTraceAvailable(tracePath), true);
  assert.deepEqual(readToolCalls(tracePath), []);
});