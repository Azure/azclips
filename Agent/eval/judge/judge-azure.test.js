const assert = require('node:assert/strict');
const test = require('node:test');
const { makeCredential, tokenUsageFromResponse } = require('./judge-azure');

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