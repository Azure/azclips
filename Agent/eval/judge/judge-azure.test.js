const assert = require('node:assert/strict');
const test = require('node:test');
const JudgeAzureProvider = require('./judge-azure');
const { makeCredential, readJudgeConfiguration, tokenUsageFromResponse } = JudgeAzureProvider;

const configuration = {
  endpoint: 'https://test-judge.openai.azure.com',
  deployment: 'review-model',
  apiVersion: '2024-12-01-preview',
  timeoutMs: 1000,
};

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
      cacheCreationInputTokens: 0,
    },
  });
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
      choices: [{ message: { content: output } }],
      usage: { prompt_tokens: 40, completion_tokens: 12 },
    }), { status: 200 });
  });
  const result = await provider.callApi('Review this response');
  assert.equal(result.output, output);
  assert.equal(result.tokenUsage.total, 52);
  assert.equal(result.tokenUsage.numRequests, 1);
  assert.equal(result.error, undefined);
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