// Custom judge provider for promptfoo — keyless Azure OpenAI via @azure/identity.
//
// Why this exists: promptfoo's built-in `azure` provider only authenticates with
// an API key, an SP client secret, or the Azure CLI on PATH. We need keyless auth
// (no secret) WITHOUT hijacking PATH (that would shadow the azclips `az` under test).
// So this provider gets the token itself via @azure/identity and forwards the call.
//
// Auth (no secret, no classic-az dependency):
//   - local: classic Azure CLI (reuses the developer's interactive login)
//   - CI (Azure DevOps): AzurePipelinesCredential (Workload Identity Federation)
//
// promptfoo still owns the grading logic (llm-rubric) — this only supplies the
// token + endpoint, so grading quality tracks promptfoo's updates.
//
// Env:
//   AZURE_OPENAI_ENDPOINT   e.g. https://cli-copilot.openai.azure.com
//   AZURE_OPENAI_DEPLOYMENT e.g. gpt-5.4-mini
//   AZURE_OPENAI_API_VERSION e.g. 2024-12-01-preview
//   CI only: AZURE_TENANT_ID, AZURE_CLIENT_ID, AZURESUBSCRIPTION_SERVICE_CONNECTION_ID,
//            SYSTEM_ACCESSTOKEN

const OPENAI_SCOPE = 'https://cognitiveservices.azure.com/.default';
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

function tokenUsageFromResponse(usage) {
  if (!usage || typeof usage !== 'object') return undefined;
  const prompt = Number(usage.prompt_tokens || 0);
  const completion = Number(usage.completion_tokens || 0);
  const cached = Number(usage.prompt_tokens_details?.cached_tokens || 0);
  const reasoning = Number(usage.completion_tokens_details?.reasoning_tokens || 0);
  return {
    prompt,
    completion,
    cached,
    total: prompt + completion,
    numRequests: 1,
    completionDetails: {
      reasoning,
      cacheReadInputTokens: cached,
      cacheCreationInputTokens: 0,
    },
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
    const cfg = (options && options.config) || {};
    this.endpoint = (cfg.endpoint || process.env.AZURE_OPENAI_ENDPOINT || '').replace(/\/$/, '');
    this.deployment = cfg.deployment || process.env.AZURE_OPENAI_DEPLOYMENT || 'gpt-5.4-mini';
    this.apiVersion = cfg.apiVersion || process.env.AZURE_OPENAI_API_VERSION || '2024-12-01-preview';
  }

  id() { return `judge-azure:${this.deployment}`; }

  async _bearer() {
    return sharedBearer(require('@azure/identity'));
  }

  async callApi(prompt, _context) {
    const token = await this._bearer();
    const url = `${this.endpoint}/openai/deployments/${this.deployment}/chat/completions?api-version=${this.apiVersion}`;
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ messages: [{ role: 'user', content: prompt }] }),
    });
    if (!res.ok) {
      return { error: `judge HTTP ${res.status}: ${await res.text()}` };
    }
    const data = await res.json();
    const tokenUsage = tokenUsageFromResponse(data.usage);
    return {
      output: data.choices?.[0]?.message?.content ?? '',
      ...(tokenUsage && { tokenUsage }),
    };
  }
}

module.exports = JudgeAzureProvider;
module.exports.makeCredential = makeCredential;
module.exports.tokenUsageFromResponse = tokenUsageFromResponse;
