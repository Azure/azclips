const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const JudgeAzureProvider = require('./judge-azure');
const { loadJudgeConfiguration, makeCredential, readJudgeConfiguration, tokenUsageFromResponse } = JudgeAzureProvider;

const configuration = {
  endpoint: 'https://test-judge.openai.azure.com',
  deployment: 'review-model',
  apiVersion: '2024-12-01-preview',
  timeoutMs: 1000,
};

function localConfigFile(context, content) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-judge-config-'));
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const configPath = path.join(directory, 'judge.config.local.json');
  if (content !== undefined) {
    fs.writeFileSync(configPath, typeof content === 'string' ? content : JSON.stringify(content));
  }
  return configPath;
}

test('loads persistent judge settings including a Windows UTF-8 BOM', (context) => {
  const configPath = localConfigFile(context, `\uFEFF${JSON.stringify(configuration)}`);
  assert.deepEqual(loadJudgeConfiguration({}, configPath), configuration);
});

test('environment overrides local judge settings without changing the local file', (context) => {
  const configPath = localConfigFile(context, configuration);
  const original = fs.readFileSync(configPath);
  const resolved = loadJudgeConfiguration({
    AZURE_OPENAI_ENDPOINT: 'https://override.openai.azure.com/',
    AZURE_OPENAI_DEPLOYMENT: 'override-deployment',
    AZURE_OPENAI_API_VERSION: '2025-01-01-preview',
  }, configPath);
  assert.deepEqual(resolved, {
    endpoint: 'https://override.openai.azure.com',
    deployment: 'override-deployment',
    apiVersion: '2025-01-01-preview',
    timeoutMs: configuration.timeoutMs,
  });
  assert.deepEqual(fs.readFileSync(configPath), original);
  assert.equal(loadJudgeConfiguration({
    AZURE_OPENAI_DEPLOYMENT: 'another-deployment',
  }, configPath).endpoint, configuration.endpoint);
});

test('environment-only configuration remains supported and missing settings still stop evaluation', (context) => {
  const missing = localConfigFile(context);
  assert.deepEqual(loadJudgeConfiguration({
    AZURE_OPENAI_ENDPOINT: configuration.endpoint,
    AZURE_OPENAI_DEPLOYMENT: configuration.deployment,
  }, missing), { ...configuration, timeoutMs: 60000 });
  assert.throws(() => loadJudgeConfiguration({}, missing), /judge\.config\.local\.json/);
  const empty = localConfigFile(context, { endpoint: '', deployment: '' });
  assert.throws(() => loadJudgeConfiguration({}, empty), /no judge will be selected automatically/);
});

test('invalid local judge files and credential fields fail without exposing their contents', (context) => {
  const configPath = localConfigFile(context);
  for (const [content, reason] of [
    ['{"endpoint":"private-value",', /Invalid JSON/],
    ['[]', /JSON object/],
    ['null', /JSON object/],
    [{ ...configuration, endpoint: 17 }, /endpoint must be a string/],
    [{ ...configuration, deployment: [] }, /deployment must be a string/],
    [{ ...configuration, apiVersion: null }, /apiVersion must be a string/],
    [{ ...configuration, timeoutMs: '1000' }, /timeoutMs/],
    [{ ...configuration, timeoutMs: null }, /timeoutMs/],
    [{ ...configuration, timeoutMs: -1 }, /timeoutMs/],
    [{ ...configuration, apiKey: 'private-value' }, /Do not store credentials/],
    [{ ...configuration, typo: 'private-value' }, /only supports/],
  ]) {
    fs.writeFileSync(configPath, typeof content === 'string' ? content : JSON.stringify(content));
    assert.throws(() => loadJudgeConfiguration({}, configPath), (error) => {
      assert.match(error.message, reason);
      assert.doesNotMatch(error.message, /private-value/);
      return true;
    });
  }
});

test('unreadable judge configuration is not silently replaced by environment settings', (context) => {
  context.mock.method(fs, 'readFileSync', () => {
    throw Object.assign(new Error('unreadable'), { code: 'EACCES' });
  });
  assert.throws(() => loadJudgeConfiguration({
    AZURE_OPENAI_ENDPOINT: configuration.endpoint,
    AZURE_OPENAI_DEPLOYMENT: configuration.deployment,
  }), /Unable to read judge configuration.*EACCES/);
});

test('real preflight loads the evaluation-local file from a different working directory', (context) => {
  const configPath = localConfigFile(context, configuration);
  const directory = path.dirname(configPath);
  const judgeDirectory = path.join(directory, 'judge');
  fs.mkdirSync(judgeDirectory);
  const entry = path.join(judgeDirectory, 'judge-azure.js');
  fs.copyFileSync(path.join(__dirname, 'judge-azure.js'), entry);
  fs.copyFileSync(path.join(__dirname, '..', 'local-azure-cli.js'), path.join(directory, 'local-azure-cli.js'));
  const env = { ...process.env };
  for (const name of ['AZURE_OPENAI_ENDPOINT', 'AZURE_OPENAI_DEPLOYMENT', 'AZURE_OPENAI_API_VERSION']) {
    delete env[name];
  }
  const result = spawnSync(process.execPath, [entry, '--check-config'], {
    cwd: os.tmpdir(), env, encoding: 'utf8', timeout: 15000,
  });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /inference access is verified by the grading call/);
  fs.writeFileSync(configPath, '{"endpoint":"","deployment":""}');
  const incomplete = spawnSync(process.execPath, [entry, '--check-config'], {
    cwd: os.tmpdir(), env, encoding: 'utf8', timeout: 15000,
  });
  assert.equal(incomplete.status, 1);
  assert.match(incomplete.stderr, /judge\.config\.local\.json/);
});

test('uses the local Azure CLI credential outside CI', () => {
  const credential = makeCredential(
    { AzurePipelinesCredential: class {} },
    { AZURE_TENANT_ID: 'tenant-1' },
  );

  assert.equal(credential.constructor.name, 'LocalAzureCliCredential');
});

test('maps Azure OpenAI usage to Promptfoo token semantics', () => {
  assert.deepEqual(tokenUsageFromResponse({
    prompt_tokens: 120,
    completion_tokens: 30,
    total_tokens: 150,
    prompt_tokens_details: { cached_tokens: 80 },
    completion_tokens_details: { reasoning_tokens: 12 },
  }), {
    prompt: 120,
    completion: 30,
    cached: 80,
    total: 150,
    numRequests: 1,
    completionDetails: {
      reasoning: 12,
      cacheReadInputTokens: 80,
    },
  });
});

test('keeps unreported judge token details absent and preserves reported zeros', () => {
  assert.deepEqual(tokenUsageFromResponse({ prompt_tokens: 40, completion_tokens: 12 }), {
    prompt: 40,
    completion: 12,
    total: 52,
    numRequests: 1,
  });
  assert.deepEqual(tokenUsageFromResponse({
    prompt_tokens: 40,
    completion_tokens: 12,
    prompt_tokens_details: { cached_tokens: 0 },
    completion_tokens_details: { reasoning_tokens: 0 },
  }).completionDetails, { reasoning: 0, cacheReadInputTokens: 0 });
});

test('requires an explicit judge endpoint and deployment rather than silently selecting a model', () => {
  assert.throws(() => readJudgeConfiguration({}, {}), /AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT/);
  assert.throws(() => readJudgeConfiguration({ endpoint: configuration.endpoint }, {}), /requires/);
  assert.deepEqual(readJudgeConfiguration(configuration, {}), configuration);
  assert.equal(readJudgeConfiguration({}, {
    AZURE_OPENAI_ENDPOINT: `${configuration.endpoint}/`,
    AZURE_OPENAI_DEPLOYMENT: configuration.deployment,
  }).endpoint, configuration.endpoint);
  for (const endpoint of ['http://test-judge.openai.azure.com', 'invalid', 'https://user:secret@example.com', `${configuration.endpoint}?token=secret`]) {
    assert.throws(() => readJudgeConfiguration({ ...configuration, endpoint }, {}), /HTTPS/);
  }
  assert.throws(() => readJudgeConfiguration({ ...configuration, timeoutMs: 0 }, {}), /timeoutMs/);
  assert.throws(() => readJudgeConfiguration({ ...configuration, timeoutMs: 0.5 }, {}), /timeoutMs/);
});

test('calls the configured Azure judge and returns real usage without changing its grading content', async (context) => {
  const provider = new JudgeAzureProvider({ config: configuration });
  context.mock.method(provider, '_bearer', async () => 'test-credential');
  const output = '{"pass":true,"score":1,"reason":"The installation is an optional alternative."}';
  context.mock.method(globalThis, 'fetch', async (url, options) => {
    assert.equal(url, `${configuration.endpoint}/openai/deployments/review-model/chat/completions?api-version=2024-12-01-preview`);
    assert.equal(options.headers.Authorization, 'Bearer test-credential');
    assert.deepEqual(JSON.parse(options.body).messages, [{ role: 'user', content: 'Review this response' }]);
    assert.ok(options.signal instanceof AbortSignal);
    return new Response(JSON.stringify({
      model: 'review-model-2026-01-01',
      choices: [{ message: { content: output } }],
      usage: { prompt_tokens: 40, completion_tokens: 12 },
    }), { status: 200 });
  });
  const result = await provider.callApi('Review this response');
  assert.equal(result.output, output);
  assert.equal(result.tokenUsage.total, 52);
  assert.equal(result.tokenUsage.numRequests, 1);
  assert.equal(result.error, undefined);
  assert.equal(result.metadata.judgeDeployment, 'review-model');
  assert.equal(result.metadata.judgeModel, 'review-model-2026-01-01');
  assert.ok(result.metadata.judgeLatencyMs >= 0);
});

test('does not invent the answering judge model when the response omits it', async (context) => {
  const provider = new JudgeAzureProvider({ config: configuration });
  context.mock.method(provider, '_bearer', async () => 'test-credential');
  context.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify({
    choices: [{ message: { content: '{"pass":true,"score":1,"reason":"ok"}' } }],
  }), { status: 200 }));
  const result = await provider.callApi('Review this response');
  assert.equal(result.metadata.judgeModel, undefined);
  assert.equal(result.metadata.judgeDeployment, 'review-model');
});

test('retries a transient judge failure without changing the graded content and records the attempts', async (context) => {
  const provider = new JudgeAzureProvider({ config: configuration });
  context.mock.method(provider, '_bearer', async () => 'test-credential');
  const delays = [];
  context.mock.method(provider, '_sleep', async (milliseconds) => { delays.push(milliseconds); });
  const output = '{"pass":true,"score":1,"reason":"ok"}';
  let calls = 0;
  context.mock.method(globalThis, 'fetch', async () => {
    calls += 1;
    return calls === 1
      ? new Response('server error', { status: 500 })
      : new Response(JSON.stringify({ model: 'review-model-2026-01-01', choices: [{ message: { content: output } }] }), { status: 200 });
  });
  const result = await provider.callApi('Review this response');
  assert.equal(result.output, output);
  assert.equal(result.error, undefined);
  assert.equal(result.metadata.judgeAttempts, 2);
  assert.deepEqual(delays, [2000]);
});

test('honors Retry-After and reports a persistent transient failure after three attempts', async (context) => {
  const provider = new JudgeAzureProvider({ config: configuration });
  context.mock.method(provider, '_bearer', async () => 'test-credential');
  const delays = [];
  context.mock.method(provider, '_sleep', async (milliseconds) => { delays.push(milliseconds); });
  let calls = 0;
  context.mock.method(globalThis, 'fetch', async () => {
    calls += 1;
    return new Response('busy', { status: 429, headers: { 'retry-after': '7' } });
  });
  const result = await provider.callApi('Review this response');
  assert.match(result.error, /HTTP 429 after 3 attempts/);
  assert.equal(calls, 3);
  assert.deepEqual(delays, [7000, 7000]);
});

test('does not retry authorization or configuration errors', async (context) => {
  const provider = new JudgeAzureProvider({ config: configuration });
  context.mock.method(provider, '_bearer', async () => 'test-credential');
  context.mock.method(provider, '_sleep', async () => assert.fail('No retry delay expected'));
  let calls = 0;
  context.mock.method(globalThis, 'fetch', async () => {
    calls += 1;
    return new Response('forbidden', { status: 403 });
  });
  const result = await provider.callApi('Review this response');
  assert.match(result.error, /HTTP 403 after 1 attempt\./);
  assert.equal(calls, 1);
});

test('judge authentication, transport, and malformed-response failures are explicit errors, not successful grades', async (context) => {
  for (const scenario of [
    { name: 'authorization', response: () => new Response('private-response', { status: 403 }), reason: /HTTP 403/ },
    { name: 'invalid JSON', response: () => new Response('not-json'), reason: /invalid JSON/ },
    { name: 'empty completion', response: () => new Response(JSON.stringify({ choices: [] })), reason: /no grading content/ },
    { name: 'null completion payload', response: () => new Response('null'), reason: /no grading content/ },
    { name: 'blank completion', response: () => new Response(JSON.stringify({ choices: [{ message: { content: ' ' } }] })), reason: /no grading content/ },
    { name: 'timeout', response: () => { throw new DOMException('timeout', 'TimeoutError'); }, reason: /timed out/ },
  ]) {
    await context.test(scenario.name, async (context) => {
      const provider = new JudgeAzureProvider({ config: configuration });
      context.mock.method(provider, '_bearer', async () => 'test-credential');
      context.mock.method(provider, '_sleep', async () => {});
      context.mock.method(globalThis, 'fetch', scenario.response);
      const result = await provider.callApi('Review this response');
      assert.match(result.error, scenario.reason);
      assert.equal(result.output, undefined);
      assert.doesNotMatch(JSON.stringify(result), /private-response|test-credential/);
    });
  }
  await context.test('token acquisition', async (context) => {
    const provider = new JudgeAzureProvider({ config: configuration });
    context.mock.method(provider, '_bearer', async () => { throw new Error('Interactive Azure sign-in is required'); });
    context.mock.method(globalThis, 'fetch', () => assert.fail('No request should be sent without a credential'));
    const result = await provider.callApi('Review this response');
    assert.match(result.error, /authentication failed.*sign-in is required/);
    assert.equal(result.output, undefined);
  });
});