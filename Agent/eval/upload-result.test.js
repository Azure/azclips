const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const {
  acquireAuthorization,
  registerEvaluationArtifact,
  sourceContext,
} = require('./upload-result');

const pipelineEnvironment = {
  SYSTEM_COLLECTIONURI: 'https://dev.azure.com/azclitools/',
  SYSTEM_TEAMPROJECT: 'public',
  BUILD_BUILDID: '42',
  SYSTEM_PULLREQUEST_SOURCEBRANCH: 'refs/heads/eval/promptfoo-poc',
  SYSTEM_PULLREQUEST_SOURCECOMMITID: '6832555fb1234567890abcdef1234567890abcd',
  SYSTEM_PULLREQUEST_PULLREQUESTNUMBER: '123',
};

test('builds PR source metadata from Azure Pipelines variables', () => {
  assert.deepEqual(
    sourceContext(pipelineEnvironment, () => new Date('2026-08-14T00:00:00Z')),
    {
      project: 'azclips',
      branch: 'eval/promptfoo-poc',
      commit: '6832555fb1234567890abcdef1234567890abcd',
      pullRequest: 123,
      environment: 'pull-request',
      ciProvider: 'azure-pipelines',
      ciRunId: '42',
      ciRunUrl: 'https://dev.azure.com/azclitools/public/_build/results?buildId=42',
      runAt: '2026-08-14T00:00:00.000Z',
    },
  );
});

test('builds local source metadata from Git', () => {
  const calls = [];
  const git = (args, cwd) => {
    calls.push([args, cwd]);
    return args.includes('--abbrev-ref')
      ? 'eval/promptfoo-poc'
      : '6832555fb1234567890abcdef1234567890abcd';
  };

  assert.deepEqual(
    sourceContext(
      {},
      () => new Date('2026-08-14T00:00:00Z'),
      git,
      'C:\\Code\\cirrus',
    ),
    {
      project: 'azclips',
      branch: 'eval/promptfoo-poc',
      commit: '6832555fb1234567890abcdef1234567890abcd',
      environment: 'local',
      ciProvider: 'local',
      runAt: '2026-08-14T00:00:00.000Z',
    },
  );
  assert.equal(calls.length, 2);
});

for (const [reason, environment] of [
  ['IndividualCI', 'ci'],
  ['BatchedCI', 'ci'],
  ['Manual', 'manual'],
  ['Schedule', 'scheduled'],
  ['ResourceTrigger', 'ci'],
]) {
  test(`builds ${reason} source metadata without PR variables`, () => {
    const source = sourceContext({
      TF_BUILD: 'True',
      BUILD_REASON: reason,
      BUILD_BUILDID: '42',
      BUILD_SOURCEBRANCH: 'refs/heads/main',
      BUILD_SOURCEVERSION: '6c0576acd4a61aecd275de6c309f19f3443b8e6e',
      SYSTEM_COLLECTIONURI: 'https://dev.azure.com/azclitools/',
      SYSTEM_TEAMPROJECT: 'public',
    }, () => new Date('2026-09-18T00:00:00Z'));

    assert.equal(source.branch, 'main');
    assert.equal(source.commit, '6c0576acd4a61aecd275de6c309f19f3443b8e6e');
    assert.equal(source.environment, environment);
    assert.equal(source.ciProvider, 'azure-pipelines');
    assert.equal(source.pullRequest, undefined);
    assert.equal(source.ciRunUrl, 'https://dev.azure.com/azclitools/public/_build/results?buildId=42');
  });
}

test('uses PR source metadata rather than the merge commit', () => {
  const source = sourceContext({
    ...pipelineEnvironment,
    BUILD_REASON: 'PullRequest',
    BUILD_SOURCEBRANCH: 'refs/pull/123/merge',
    BUILD_SOURCEVERSION: 'merge-commit',
  });

  assert.equal(source.branch, 'eval/promptfoo-poc');
  assert.equal(source.commit, pipelineEnvironment.SYSTEM_PULLREQUEST_SOURCECOMMITID);
  assert.equal(source.pullRequest, 123);
});

test('does not attach stale PR metadata to an explicitly non-PR build', () => {
  const source = sourceContext({
    ...pipelineEnvironment,
    BUILD_REASON: 'Manual',
    BUILD_SOURCEBRANCH: 'refs/heads/main',
    BUILD_SOURCEVERSION: 'manual-commit',
  });

  assert.equal(source.branch, 'main');
  assert.equal(source.commit, 'manual-commit');
  assert.equal(source.pullRequest, undefined);
  assert.equal(source.environment, 'manual');
});

test('rejects missing source revisions instead of silently mixing PR and CI metadata', () => {
  assert.throws(() => sourceContext({
    ...pipelineEnvironment,
    BUILD_REASON: 'PullRequest',
    SYSTEM_PULLREQUEST_SOURCECOMMITID: undefined,
    BUILD_SOURCEVERSION: 'merge-commit',
  }), /SYSTEM_PULLREQUEST_SOURCECOMMITID is required/);

  assert.throws(() => sourceContext({
    ...pipelineEnvironment,
    BUILD_REASON: 'Manual',
    BUILD_SOURCEBRANCH: 'refs/heads/main',
  }), /BUILD_SOURCEVERSION is required/);
});

test('acquires the API token through AzurePipelinesCredential inputs', async () => {
  const calls = [];
  const environment = {
    AZURE_TENANT_ID: 'tenant-1',
    AZURE_CLIENT_ID: 'client-1',
    AZURESUBSCRIPTION_SERVICE_CONNECTION_ID: 'connection-1',
    SYSTEM_ACCESSTOKEN: 'system-token',
  };

  const authorization = await acquireAuthorization(environment, (...args) => {
    calls.push(args);
    return { getToken: async (scope) => ({ token: `token-for:${scope}` }) };
  });

  assert.deepEqual(calls, [[
    'tenant-1',
    'client-1',
    'connection-1',
    'system-token',
  ]]);
  assert.equal(
    authorization,
    'Bearer token-for:api://d70aaaba-e3a4-4746-bbd3-15ebcd9cb454/.default',
  );
});

test('acquires the API token through a local credential', async () => {
  const calls = [];
  const authorization = await acquireAuthorization({ AZURE_TENANT_ID: 'tenant-1' }, (...args) => {
    calls.push(args);
    return { getToken: async (scope) => ({ token: `local-token-for:${scope}` }) };
  });

  assert.deepEqual(calls, [[]]);
  assert.equal(
    authorization,
    'Bearer local-token-for:api://d70aaaba-e3a4-4746-bbd3-15ebcd9cb454/access_as_user',
  );
});

test('registers schema, run metadata, and artifact in order', async (context) => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'azclips-upload-test-'));
  context.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const artifactPath = path.join(directory, 'artifact.json');
  fs.writeFileSync(artifactPath, JSON.stringify({
    evalId: 'eval-test',
    results: {
      version: 3,
      timestamp: '2026-08-14T01:02:03.000Z',
      prompts: [{ raw: 'prompt', label: 'runtime prompt', provider: 'azclips-agent' }],
      results: [{ response: { metadata: { models: ['gpt-5.4-mini'] } } }],
    },
    config: { tests: [{ description: 'case' }] },
    metadata: {
      promptfooVersion: '0.121.18',
      exportedAt: '2026-08-14T01:03:00.000Z',
    },
  }));
  const requests = [];
  const fetchImpl = async (url, options) => {
    requests.push({ url: url.toString(), options });
    return {
      ok: true,
      status: options.method === 'POST' ? 201 : 200,
      text: async () => '',
      json: async () => ({ runId: '00000000-0000-4000-8000-000000000123', uploadUrl: '/api/v1/runs/00000000-0000-4000-8000-000000000123/artifact' }),
    };
  };

  const result = await registerEvaluationArtifact({
    artifactPath,
    serviceUrl: 'https://evaluation.example',
    authorization: 'Bearer test-token',
    env: pipelineEnvironment,
    fetchImpl,
  });

  assert.deepEqual(requests.map((request) => [request.options.method, request.url]), [
    ['PUT', 'https://evaluation.example/api/v1/projects'],
    ['POST', 'https://evaluation.example/api/v1/runs'],
    ['PUT', 'https://evaluation.example/api/v1/runs/00000000-0000-4000-8000-000000000123/artifact'],
  ]);
  const metadata = JSON.parse(requests[1].options.body);
  assert.equal(metadata.pullRequest, 123);
  assert.equal(metadata.commit, pipelineEnvironment.SYSTEM_PULLREQUEST_SOURCECOMMITID);
  assert.equal(metadata.runAt, '2026-08-14T01:02:03.000Z');
  assert.deepEqual(metadata.dimensions.model, ['gpt-5.4-mini']);
  assert.equal(
    result.dashboardUrl,
    'https://evaluation.example/evaluations?project=azclips&evalId=00000000-0000-4000-8000-000000000123',
  );
});