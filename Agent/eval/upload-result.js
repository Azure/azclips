const { createHash } = require('node:crypto');
const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const { LocalAzureCliCredential } = require('./local-azure-cli');

const DEFAULT_SERVICE_URL = 'https://azclis-evaluation-web-6b085460.azurewebsites.net';
const DEFAULT_API_SCOPE = 'api://d70aaaba-e3a4-4746-bbd3-15ebcd9cb454/.default';
const DEFAULT_LOCAL_API_SCOPE = 'api://d70aaaba-e3a4-4746-bbd3-15ebcd9cb454/access_as_user';
const PROJECT = 'azclips';
const PROJECT_SCHEMA_VERSION = 'agent-eval-v1';

const projectSchema = {
  project: PROJECT,
  version: PROJECT_SCHEMA_VERSION,
  fields: [
    { key: 'branch', label: 'Branch', valueType: 'string', filterControl: 'multi-select', order: 10, trendIdentity: true },
    { key: 'environment', label: 'Environment', valueType: 'string', filterControl: 'multi-select', order: 20, trendIdentity: true },
    { key: 'commit', label: 'Commit', valueType: 'string', filterControl: 'multi-select', order: 30 },
    { key: 'model', label: 'Model', valueType: 'string-list', filterControl: 'multi-select', order: 40, trendIdentity: true },
    { key: 'judgeModel', label: 'Judge Model', valueType: 'string-list', filterControl: 'multi-select', order: 50 },
  ],
  questionFields: [
    { key: 'scenario', label: 'Scenario', order: 10, sources: [{ path: 'description' }] },
    { key: 'submitted-command', label: 'Submitted command', order: 20, tone: 'subtle', sources: [{ path: 'command' }], emptyText: 'Not provided' },
    { key: 'execution-error', label: 'Execution error', order: 30, tone: 'error', selection: 'first-present', sources: [{ path: 'responseMetadata.executionError' }, { path: 'error' }], emptyText: 'No execution error' },
    { key: 'generated-plan', label: 'Generated plan', order: 40, format: 'json', sources: [{ path: 'responseMetadata.plan' }], emptyText: 'No structured plan' },
    { key: 'runtime-prompt', label: 'Runtime prompt', order: 50, sources: [{ path: 'prompt' }] },
    { key: 'tool-queries', label: 'Tool queries and results', order: 60, kind: 'tool-calls' },
  ],
};

function requiredEnvironment(env, name) {
  const value = env[name];
  if (!value || !value.trim()) throw new Error(`${name} is required`);
  return value.trim();
}

function defaultGit(args, cwd) {
  return execFileSync('git', args, {
    cwd,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'ignore'],
  }).trim();
}

function readGit(git, args, cwd) {
  try {
    return git(args, cwd).trim() || undefined;
  } catch {
    return undefined;
  }
}

function sourceContext(
  env = process.env,
  now = () => new Date(),
  git = defaultGit,
  cwd = process.cwd(),
) {
  const isAzurePipelines = env.TF_BUILD?.toLowerCase() === 'true' || Boolean(
    env.BUILD_BUILDID &&
    (env.SYSTEM_PULLREQUEST_SOURCEBRANCH || env.BUILD_SOURCEBRANCH),
  );
  if (!isAzurePipelines) {
    const branch = env.AZCLIPS_EVALUATION_BRANCH ||
      readGit(git, ['rev-parse', '--abbrev-ref', 'HEAD'], cwd);
    const commit = env.AZCLIPS_EVALUATION_COMMIT ||
      readGit(git, ['rev-parse', 'HEAD'], cwd);
    if (!branch) throw new Error('Unable to resolve the local Git branch');
    if (!commit) throw new Error('Unable to resolve the local Git commit');
    const pullRequest = Number(env.AZCLIPS_EVALUATION_PULL_REQUEST);
    return {
      project: PROJECT,
      branch,
      commit,
      ...(Number.isInteger(pullRequest) && pullRequest > 0 ? { pullRequest } : {}),
      environment: env.AZCLIPS_EVALUATION_ENVIRONMENT || 'local',
      ciProvider: 'local',
      runAt: now().toISOString(),
    };
  }

  const isPullRequest = env.BUILD_REASON
    ? env.BUILD_REASON === 'PullRequest'
    : Boolean(env.SYSTEM_PULLREQUEST_SOURCEBRANCH || env.SYSTEM_PULLREQUEST_SOURCECOMMITID);
  const pullRequestValue = isPullRequest
    ? env.SYSTEM_PULLREQUEST_PULLREQUESTNUMBER || env.SYSTEM_PULLREQUEST_PULLREQUESTID
    : undefined;
  const pullRequest = Number(pullRequestValue);
  const collectionUri = requiredEnvironment(env, 'SYSTEM_COLLECTIONURI');
  const project = requiredEnvironment(env, 'SYSTEM_TEAMPROJECT');
  const buildId = requiredEnvironment(env, 'BUILD_BUILDID');
  const branchVariable = isPullRequest ? 'SYSTEM_PULLREQUEST_SOURCEBRANCH' : 'BUILD_SOURCEBRANCH';
  const commitVariable = isPullRequest ? 'SYSTEM_PULLREQUEST_SOURCECOMMITID' : 'BUILD_SOURCEVERSION';
  const environment = isPullRequest
    ? 'pull-request'
    : env.BUILD_REASON === 'Manual'
      ? 'manual'
      : env.BUILD_REASON === 'Schedule' ? 'scheduled' : 'ci';
  return {
    project: PROJECT,
    branch: requiredEnvironment(env, branchVariable).replace(/^refs\/heads\//, ''),
    commit: requiredEnvironment(env, commitVariable),
    ...(Number.isInteger(pullRequest) && pullRequest > 0 ? { pullRequest } : {}),
    environment,
    ciProvider: 'azure-pipelines',
    ciRunId: buildId,
    ciRunUrl: new URL(
      `${project.split('/').map(encodeURIComponent).join('/')}/_build/results?buildId=${encodeURIComponent(buildId)}`,
      collectionUri.endsWith('/') ? collectionUri : `${collectionUri}/`,
    ).toString(),
    runAt: now().toISOString(),
  };
}

function uniqueStrings(values) {
  return [...new Set(values.filter((value) => typeof value === 'string' && value.trim())
    .map((value) => value.trim()))].sort();
}

function modelDimensions(artifact, env = process.env) {
  const results = artifact.results?.results || [];
  const models = uniqueStrings(results.flatMap((result) =>
    Array.isArray(result.response?.metadata?.models)
      ? result.response.metadata.models
      : [result.response?.metadata?.model],
  ));
  const judgeModels = uniqueStrings([
    env.EVAL_JUDGE_MODEL || 'gpt-5.4-mini',
  ]);
  return { models, judgeModels };
}

function suiteVersion(artifact) {
  const prompts = (artifact.results?.prompts || []).map((prompt) => ({
    raw: prompt.raw || '',
    label: prompt.label || '',
    provider: prompt.provider || '',
  }));
  return createHash('sha256')
    .update(JSON.stringify({ tests: artifact.config?.tests || [], prompts }))
    .digest('hex');
}

async function acquireAuthorization(env = process.env, credentialFactory) {
  const isAzurePipelines = Boolean(
    env.SYSTEM_ACCESSTOKEN && env.AZURESUBSCRIPTION_SERVICE_CONNECTION_ID,
  );
  let credential;
  if (isAzurePipelines) {
    const createCredential = credentialFactory || ((...args) => {
      const { AzurePipelinesCredential } = require('@azure/identity');
      return new AzurePipelinesCredential(...args);
    });
    credential = createCredential(
      requiredEnvironment(env, 'AZURE_TENANT_ID'),
      requiredEnvironment(env, 'AZURE_CLIENT_ID'),
      requiredEnvironment(env, 'AZURESUBSCRIPTION_SERVICE_CONNECTION_ID'),
      requiredEnvironment(env, 'SYSTEM_ACCESSTOKEN'),
    );
  } else {
    const createCredential = credentialFactory || (() => new LocalAzureCliCredential(env));
    credential = createCredential();
  }
  const accessToken = await credential.getToken(
    env.AZCLIPS_EVALUATION_API_SCOPE ||
      (isAzurePipelines ? DEFAULT_API_SCOPE : DEFAULT_LOCAL_API_SCOPE),
  );
  if (!accessToken?.token) throw new Error('Azure credential returned no access token');
  return `Bearer ${accessToken.token}`;
}

async function checkedFetch(fetchImpl, url, options, operation) {
  const response = await fetchImpl(url, options);
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(`${operation} failed with HTTP ${response.status}: ${detail}`);
  }
  return response;
}

async function registerEvaluationArtifact({
  artifactPath,
  serviceUrl = DEFAULT_SERVICE_URL,
  authorization,
  env = process.env,
  fetchImpl = fetch,
  now = () => new Date(),
}) {
  const artifact = fs.readFileSync(artifactPath);
  const parsed = JSON.parse(artifact.toString('utf8'));
  const source = sourceContext(env, now);
  source.runAt = new Date(parsed.results?.timestamp || source.runAt).toISOString();
  const { models, judgeModels } = modelDimensions(parsed, env);
  const metadata = {
    ...source,
    engineVersion: parsed.metadata?.promptfooVersion,
    suiteVersion: suiteVersion(parsed),
    projectSchemaVersion: PROJECT_SCHEMA_VERSION,
    dimensions: {
      branch: source.branch,
      environment: source.environment,
      commit: source.commit,
      model: models,
      judgeModel: judgeModels,
    },
    artifactSha256: createHash('sha256').update(artifact).digest('hex'),
    artifactSizeBytes: artifact.byteLength,
  };
  if (!metadata.engineVersion) throw new Error('Artifact metadata.promptfooVersion is required');

  const headers = { 'Content-Type': 'application/json', Authorization: authorization };
  await checkedFetch(
    fetchImpl,
    new URL('/api/v1/projects', serviceUrl),
    { method: 'PUT', headers, body: JSON.stringify(projectSchema) },
    'Registering project schema',
  );
  const createResponse = await checkedFetch(
    fetchImpl,
    new URL('/api/v1/runs', serviceUrl),
    { method: 'POST', headers, body: JSON.stringify(metadata) },
    'Creating evaluation run',
  );
  const created = await createResponse.json();
  await checkedFetch(
    fetchImpl,
    new URL(created.uploadUrl, serviceUrl),
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', Authorization: authorization },
      body: artifact,
    },
    'Uploading evaluation artifact',
  );
  return {
    runId: created.runId,
    dashboardUrl: new URL(
      `/evaluations?project=${encodeURIComponent(PROJECT)}&evalId=${encodeURIComponent(created.runId)}`,
      serviceUrl,
    ).toString(),
  };
}

function option(args, name) {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : undefined;
}

async function main(args = process.argv.slice(2), env = process.env) {
  const artifactPath = option(args, '--artifact');
  if (!artifactPath) throw new Error('--artifact is required');
  const serviceUrl = option(args, '--service-url') ||
    env.AZCLIPS_EVALUATION_URL ||
    DEFAULT_SERVICE_URL;
  const authorization = await acquireAuthorization(env);
  const result = await registerEvaluationArtifact({
    artifactPath: path.resolve(artifactPath),
    serviceUrl,
    authorization,
    env,
  });
  process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
}

if (require.main === module) {
  main().catch((error) => {
    process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`);
    process.exitCode = 1;
  });
}

module.exports = {
  acquireAuthorization,
  modelDimensions,
  projectSchema,
  registerEvaluationArtifact,
  sourceContext,
  suiteVersion,
};