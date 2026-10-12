// Custom judge provider for promptfoo — keyless Azure OpenAI via @azure/identity.
//
// Why this exists: promptfoo's built-in `azure` provider only authenticates with
// an API key, an SP client secret, or the Azure CLI on PATH. We need keyless auth
// (no secret) WITHOUT hijacking PATH (that would shadow the azclips `az` under test).
// So this provider gets the token itself via @azure/identity and forwards the call.
//
// Auth (no secret in configuration):
//   - local: classic Azure CLI (reuses the developer's interactive login)
//   - CI (Azure DevOps): AzurePipelinesCredential (Workload Identity Federation)
//
// promptfoo still owns the grading logic (llm-rubric) — this only supplies the
// token + endpoint, so grading quality tracks promptfoo's updates.
//
// Local file: Agent/eval/judge.config.local.json (git-ignored).
// Env overrides the corresponding local file values:
//   AZURE_OPENAI_ENDPOINT   e.g. https://cli-copilot.openai.azure.com
//   AZURE_OPENAI_DEPLOYMENT e.g. gpt-5.4-mini
//   AZURE_OPENAI_API_VERSION e.g. 2024-12-01-preview
//   CI only: AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURESUBSCRIPTION_SERVICE_CONNECTION_ID,
//            SYSTEM_ACCESSTOKEN

const OPENAI_SCOPE = 'https://cognitiveservices.azure.com/.default';
const fs = require('node:fs');
const path = require('node:path');
const { LocalAzureCliCredential } = require('../local-azure-cli');

function makeCredential(identity, env = process.env) {
  const {
    AzurePipelinesCredential,
  } = identity;

  // CI = Azure DevOps: Workload Identity Federation, no secret, no interaction.
  if (env.SYSTEM_ACCESSTOKEN && env.AZURESUBSCRIPTION_SERVICE_CONNECTION_ID) {
    return new AzurePipelinesCredential(
      env.AZURE_TENANT_ID,
      env.AZURE_CLIENT_ID,
      env.AZURESUBSCRIPTION_SERVICE_CONNECTION_ID,
      env.SYSTEM_ACCESSTOKEN,
    );
  }

  return new LocalAzureCliCredential(env);
}

// Global single-acquire: one getToken() shared across ALL judge instances in this
// process, so parallel cases reuse one token.
let s_tokenPromise = null;
let s_token = null;

function readJudgeConfiguration(cfg = {}, env = process.env) {
  const endpoint = (cfg.endpoint || env.AZURE_OPENAI_ENDPOINT || '').trim().replace(/\/+$/, '');
  const deployment = (cfg.deployment || env.AZURE_OPENAI_DEPLOYMENT || '').trim();
  const apiVersion = (cfg.apiVersion || env.AZURE_OPENAI_API_VERSION || '2024-12-01-preview').trim();
  const timeoutMs = cfg.timeoutMs ?? 60000;
  if (!endpoint || !deployment) {
    throw new Error('Semantic grading requires AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT, or endpoint/deployment in Agent/eval/judge.config.local.json; no judge will be selected automatically.');
  }
  let parsed;
  try {
    parsed = new URL(endpoint);
  } catch {
    throw new Error('The Azure judge endpoint must be an absolute HTTPS URL.');
  }
  if (parsed.protocol !== 'https:' || parsed.username || parsed.password || parsed.search || parsed.hash) {
    throw new Error('The Azure judge endpoint must use HTTPS without embedded credentials, query parameters, or a fragment.');
  }
  if (!apiVersion || !Number.isSafeInteger(timeoutMs) || timeoutMs <= 0) {
    throw new Error('The Azure judge requires a nonempty API version and a positive integer timeoutMs.');
  }
  return { endpoint, deployment, apiVersion, timeoutMs };
}

function loadJudgeConfiguration(
  env = process.env,
  configPath = path.join(__dirname, '..', 'judge.config.local.json'),
) {
  let content;
  try {
    content = fs.readFileSync(configPath, 'utf8');
  } catch (error) {
    if (error.code !== 'ENOENT') {
      throw new Error(`Unable to read judge configuration "${configPath}" (${error.code || 'read failed'}).`);
    }
  }
  let local = {};
  if (content !== undefined) {
    try {
      local = JSON.parse(content.replace(/^\uFEFF/, ''));
    } catch (error) {
      if (!(error instanceof SyntaxError)) throw error;
      throw new Error(`Invalid JSON in judge configuration "${configPath}".`);
    }
    if (!local || typeof local !== 'object' || Array.isArray(local)) {
      throw new Error(`Judge configuration "${configPath}" must be a JSON object.`);
    }
    const allowed = ['endpoint', 'deployment', 'apiVersion', 'timeoutMs'];
    if (Object.keys(local).some((key) => !allowed.includes(key))) {
      throw new Error(`Judge configuration "${configPath}" only supports ${allowed.join(', ')}. Do not store credentials in this file; authentication uses Entra.`);
    }
    for (const key of ['endpoint', 'deployment', 'apiVersion']) {
      if (local[key] !== undefined && typeof local[key] !== 'string') {
        throw new Error(`Judge configuration "${configPath}": ${key} must be a string.`);
      }
    }
    if (local.timeoutMs !== undefined && (!Number.isSafeInteger(local.timeoutMs) || local.timeoutMs <= 0)) {
      throw new Error(`Judge configuration "${configPath}": timeoutMs must be a positive integer.`);
    }
  }
  return readJudgeConfiguration({
    ...local,
    endpoint: env.AZURE_OPENAI_ENDPOINT || local.endpoint,
    deployment: env.AZURE_OPENAI_DEPLOYMENT || local.deployment,
    apiVersion: env.AZURE_OPENAI_API_VERSION || local.apiVersion,
  }, env);
}

function tokenUsageFromResponse(usage) {
  if (!usage || typeof usage !== 'object') return undefined;
  const prompt = Number(usage.prompt_tokens || 0);
  const completion = Number(usage.completion_tokens || 0);
  // Keep unreported optional counts absent instead of recording a measured zero.
  const reported = (value) => (value === undefined || value === null ? undefined : Number(value));
  const cached = reported(usage.prompt_tokens_details?.cached_tokens);
  const reasoning = reported(usage.completion_tokens_details?.reasoning_tokens);
  const completionDetails = {
    ...(reasoning !== undefined && { reasoning }),
    ...(cached !== undefined && { cacheReadInputTokens: cached }),
  };
  return {
    prompt,
    completion,
    ...(cached !== undefined && { cached }),
    total: prompt + completion,
    numRequests: 1,
    ...(Object.keys(completionDetails).length && { completionDetails }),
  };
}

async function sharedBearer(identity) {
  if (s_token && s_token.expiresOnTimestamp - Date.now() > 60_000) {
    return s_token.token;
  }
  if (!s_tokenPromise) {
    const cred = makeCredential(identity);
    const canInteract = typeof cred.authenticate === 'function'; // false for CI (AzurePipelinesCredential)
    s_tokenPromise = (async () => {
      try {
        const t = await cred.getToken(OPENAI_SCOPE);
        s_token = t;
        return t;
      } catch (e) {
        // Retain support for credentials that can explicitly prompt, while
        // Azure CLI and CI credentials fail with their actionable auth errors.
        if (!canInteract) throw e;
        await cred.authenticate(OPENAI_SCOPE);
        const t = await cred.getToken(OPENAI_SCOPE);
        s_token = t;
        return t;
      }
    })().finally(() => { s_tokenPromise = null; });
  }
  const t = await s_tokenPromise;
  return t.token;
}

class JudgeAzureProvider {
  constructor(options) {
    Object.assign(this, readJudgeConfiguration(options?.config));
  }

  id() { return `judge-azure:${this.deployment}`; }

  async _bearer() {
    return sharedBearer(require('@azure/identity'));
  }

  async callApi(prompt, _context) {
    let token;
    try {
      token = await this._bearer();
    } catch (error) {
      return { error: `Azure judge authentication failed: ${error.message}` };
    }
    const url = `${this.endpoint}/openai/deployments/${encodeURIComponent(this.deployment)}/chat/completions?api-version=${encodeURIComponent(this.apiVersion)}`;
    const requestStartedAt = Date.now();
    let res;
    try {
      res = await fetch(url, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ messages: [{ role: 'user', content: prompt }] }),
        signal: AbortSignal.timeout(this.timeoutMs),
      });
    } catch (error) {
      return { error: error.name === 'TimeoutError' || error.name === 'AbortError'
        ? `Azure judge request timed out after ${this.timeoutMs} ms.`
        : `Azure judge request failed: ${error.message}` };
    }
    if (!res.ok) {
      return { error: `Azure judge returned HTTP ${res.status}. Check the deployment, API version, and Cognitive Services inference permissions.` };
    }
    let data;
    try {
      data = await res.json();
    } catch {
      return { error: 'Azure judge returned invalid JSON instead of a completion.' };
    }
    const output = data?.choices?.[0]?.message?.content;
    if (typeof output !== 'string' || !output.trim()) {
      return { error: 'Azure judge returned no grading content; the semantic check was not completed.' };
    }
    const tokenUsage = tokenUsageFromResponse(data.usage);
    // Promptfoo keeps grader response metadata on the grading result. The deployment name alone does
    // not show which model version answered.
    return {
      output,
      ...(tokenUsage && { tokenUsage }),
      metadata: {
        judgeDeployment: this.deployment,
        ...(typeof data.model === 'string' && data.model.trim() && { judgeModel: data.model.trim() }),
        judgeLatencyMs: Date.now() - requestStartedAt,
      },
    };
  }
}

module.exports = JudgeAzureProvider;
module.exports.makeCredential = makeCredential;
module.exports.tokenUsageFromResponse = tokenUsageFromResponse;
module.exports.readJudgeConfiguration = readJudgeConfiguration;
module.exports.loadJudgeConfiguration = loadJudgeConfiguration;

if (require.main === module) {
  try {
    loadJudgeConfiguration();
    process.stdout.write('Semantic judge configuration is present; inference access is verified by the grading call.\n');
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
